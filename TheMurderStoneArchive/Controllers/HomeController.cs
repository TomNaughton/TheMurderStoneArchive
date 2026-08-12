using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Helpers;
using TheMurderStoneArchive.Models;
using TheMurderStoneArchive.Services;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HomeController> _logger;
    private readonly IPdfDocumentService _pdfDocumentService;
    private readonly IStripePaymentService _stripePaymentService;
    private readonly IPatreonWebhookService _patreonWebhookService;
    private readonly IFourthwallWebhookService _fourthwallWebhookService;
    private readonly DonationOptions _donationOptions;

    public HomeController(
        ApplicationDbContext context,
        ILogger<HomeController> logger,
        IPdfDocumentService pdfDocumentService,
        IStripePaymentService stripePaymentService,
        IPatreonWebhookService patreonWebhookService,
        IFourthwallWebhookService fourthwallWebhookService,
        IOptions<DonationOptions> donationOptions)
    {
        _context = context;
        _logger = logger;
        _pdfDocumentService = pdfDocumentService;
        _stripePaymentService = stripePaymentService;
        _patreonWebhookService = patreonWebhookService;
        _fourthwallWebhookService = fourthwallWebhookService;
        _donationOptions = donationOptions.Value;
    }

    public async Task<IActionResult> Index()
    {
        var events = await _context.MurderEvents
            .WithLocation()
            .ApprovedAndNotLost()
            .ToListAsync();

        return View(events);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Terms()
    {
        return View();
    }

    public IActionResult Donate()
    {
        ViewBag.UsePatreon = _donationOptions.UsePatreon;
        ViewBag.PatreonCampaignUrl = GetPatreonDonationUrl();
        ViewBag.UseFourthwall = _donationOptions.UseFourthwall;
        ViewBag.FourthwallOneTimePaymentUrl = GetFourthwallDonationUrl();
        return View();
    }

    public IActionResult ApiDocs()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Campaign()
    {
        var campaign = await EnsureDefaultCampaignAsync();

        var recent = await _context.MonetaryContributions
            .AsNoTracking()
            .Where(c => c.DonationCampaignId == campaign.Id && c.IsCountedInTotal)
            .OrderByDescending(c => c.SubmittedAtUtc)
            .Take(15)
            .Select(c => new CampaignContributionViewModel
            {
                AmountGbp = c.AmountGbp,
                Source = c.Source,
                SubmittedAtUtc = c.SubmittedAtUtc
            })
            .ToListAsync();

        var raised = await _context.MonetaryContributions
            .AsNoTracking()
            .Where(c => c.DonationCampaignId == campaign.Id && c.IsCountedInTotal)
            .SumAsync(c => (decimal?)c.AmountGbp) ?? 0m;

        var vm = new CampaignViewModel
        {
            CampaignId = campaign.Id,
            Name = campaign.Name,
            Description = campaign.Description,
            TargetAmountGbp = campaign.TargetAmountGbp,
            RaisedAmountGbp = raised,
            ProgressPercentage = campaign.TargetAmountGbp <= 0 ? 0 : Math.Min(100, decimal.Round((raised / campaign.TargetAmountGbp) * 100m, 2)),
            EndsAtUtc = campaign.EndsAtUtc,
            PaymentProvider = _donationOptions.Provider,
            PatreonCampaignUrl = GetPatreonDonationUrl(),
            FourthwallOneTimePaymentUrl = GetFourthwallDonationUrl(),
            UsePatreon = _donationOptions.UsePatreon,
            UseFourthwall = _donationOptions.UseFourthwall,
            RecentContributions = recent
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartCheckout([FromForm] decimal amountGbp, [FromForm] long? campaignId, CancellationToken cancellationToken)
    {
        if (amountGbp <= 0)
        {
            return BadRequest("Amount must be greater than zero.");
        }

        var campaign = campaignId.HasValue
            ? await _context.DonationCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId.Value, cancellationToken)
            : await EnsureDefaultCampaignAsync(cancellationToken);

        if (campaign == null)
        {
            return NotFound();
        }

        if (_donationOptions.UsePatreon)
        {
            var patreonDonationUrl = GetPatreonDonationUrl();
            if (string.IsNullOrWhiteSpace(patreonDonationUrl))
            {
                _logger.LogWarning("Patreon provider is enabled but no Patreon donation URL is configured");
                TempData["CampaignError"] = "Donations are temporarily unavailable. Please try again shortly.";
                return RedirectToAction(nameof(Campaign));
            }

            return Redirect(patreonDonationUrl);
        }

        if (_donationOptions.UseFourthwall)
        {
            var fourthwallDonationUrl = GetFourthwallDonationUrl();
            if (string.IsNullOrWhiteSpace(fourthwallDonationUrl))
            {
                _logger.LogWarning("Fourthwall provider is enabled but no one-time payment URL is configured");
                TempData["CampaignError"] = "Donations are temporarily unavailable. Please try again shortly.";
                return RedirectToAction(nameof(Campaign));
            }

            return Redirect(fourthwallDonationUrl);
        }

        var successUrl = $"{Request.Scheme}://{Request.Host}/Home/Campaign?payment=success";
        var cancelUrl = $"{Request.Scheme}://{Request.Host}/Home/Campaign?payment=cancel";

        try
        {
            var checkoutUrl = await _stripePaymentService.CreateCheckoutSessionUrlAsync(
                amountGbp,
                campaign.Name,
                successUrl,
                cancelUrl,
                campaign.Id,
                cancellationToken);

            return Redirect(checkoutUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stripe checkout creation failed for campaign {CampaignId}", campaign.Id);
            TempData["CampaignError"] = "Unable to start payment right now. Please try again shortly.";
            return RedirectToAction(nameof(Campaign));
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> StripeWebhook(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        var signature = Request.Headers["Stripe-Signature"].ToString();

        try
        {
            await _stripePaymentService.HandleWebhookAsync(payload, signature, cancellationToken);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stripe webhook handling failed");
            return BadRequest();
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PatreonWebhook(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        var signature = Request.Headers["X-Patreon-Signature"].ToString();
        var eventType = Request.Headers["X-Patreon-Event"].ToString();

        try
        {
            var campaign = await EnsureDefaultCampaignAsync(cancellationToken);
            await _patreonWebhookService.HandleWebhookAsync(payload, signature, eventType, campaign.Id, cancellationToken);
            return Ok();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Patreon webhook signature validation failed");
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Patreon webhook handling issue. Keeping last known donation totals.");
            return Ok();
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> FourthwallWebhook(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        var signature = Request.Headers["X-Fourthwall-Hmac-Sha256"].ToString();
        if (string.IsNullOrWhiteSpace(signature))
        {
            signature = Request.Headers["X-Fourthwall-Hmac-Apps-SHA256"].ToString();
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            signature = Request.Headers["X-Fourthwall-Signature"].ToString();
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            signature = Request.Headers["X-Signature"].ToString();
        }

        var eventType = Request.Headers["X-Fourthwall-Event"].ToString();

        try
        {
            var campaign = await EnsureDefaultCampaignAsync(cancellationToken);
            await _fourthwallWebhookService.HandleWebhookAsync(payload, signature, eventType, campaign.Id, cancellationToken);
            return Ok();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Fourthwall webhook signature validation failed. HeaderLength={HeaderLength}; EventType={EventType}", signature?.Length ?? 0, eventType);
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fourthwall webhook handling issue. Keeping last known donation totals.");
            return Ok();
        }
    }

    [Authorize(Roles = AppConstants.AdminRole)]
    [HttpGet]
    public async Task<IActionResult> CampaignAdmin(CancellationToken cancellationToken)
    {
        var campaign = await EnsureDefaultCampaignAsync(cancellationToken);

        var contributions = await _context.MonetaryContributions
            .AsNoTracking()
            .Where(c => c.DonationCampaignId == campaign.Id)
            .OrderByDescending(c => c.SubmittedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        var raised = contributions.Where(c => c.IsCountedInTotal).Sum(c => c.AmountGbp);

        var subscriptions = await _context.Subscriptions
            .AsNoTracking()
            .OrderByDescending(s => s.StartedAtUtc)
            .ToListAsync(cancellationToken);

        // TotalAmountGbp is a computed property (not stored), so sum in-process
        var subscriptionTotal = subscriptions.Sum(s => s.TotalAmountGbp);

        var vm = new CampaignAdminViewModel
        {
            CampaignId = campaign.Id,
            CampaignName = campaign.Name,
            TargetAmountGbp = campaign.TargetAmountGbp,
            RaisedAmountGbp = raised,
            SubscriptionTotalAmountGbp = subscriptionTotal,
            Contributions = contributions,
            Subscriptions = subscriptions,
            ManualContribution = new ManualContributionInput
            {
                Source = "Manual"
            }
        };

        return View(vm);
    }

    [Authorize(Roles = AppConstants.AdminRole)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddManualContribution([FromForm] CampaignAdminViewModel model, CancellationToken cancellationToken)
    {
        var campaign = await _context.DonationCampaigns.FirstOrDefaultAsync(c => c.Id == model.CampaignId, cancellationToken);
        if (campaign == null)
        {
            return NotFound();
        }

        if (model.ManualContribution.AmountGbp <= 0)
        {
            TempData["CampaignError"] = "Amount must be greater than zero.";
            return RedirectToAction(nameof(CampaignAdmin));
        }

        var contribution = new MonetaryContribution
        {
            DonationCampaignId = campaign.Id,
            AmountGbp = model.ManualContribution.AmountGbp,
            Currency = "GBP",
            Source = string.IsNullOrWhiteSpace(model.ManualContribution.Source) ? "Manual" : model.ManualContribution.Source.Trim(),
            ContributorName = model.ManualContribution.ContributorName,
            ContributorEmail = model.ManualContribution.ContributorEmail,
            Note = model.ManualContribution.Note,
            IsCountedInTotal = true,
            IsManualEntry = true,
            Status = "Submitted",
            SubmittedAtUtc = DateTime.UtcNow,
            ReceivedAtUtc = DateTime.UtcNow
        };

        _context.MonetaryContributions.Add(contribution);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["CampaignSuccess"] = "Contribution added.";
        return RedirectToAction(nameof(CampaignAdmin));
    }

    public IActionResult ResearchPack()
    {
        return View();
    }

    public async Task<IActionResult> Funding()
    {
        var publishedEventCount = await _context.MurderEvents
            .ApprovedAndNotLost()
            .CountAsync();

        var locationCount = await _context.Locations.CountAsync();

        var totalCtaClicks = await _context.CtaClickEvents.CountAsync();

        var supportCtaClicks = await _context.CtaClickEvents
            .Where(e => e.CtaKey.StartsWith("donate") || e.CtaKey.StartsWith("sponsor") || e.CtaKey.StartsWith("funding"))
            .CountAsync();

        var vm = new FundingViewModel
        {
            PublishedEventCount = publishedEventCount,
            LocationCount = locationCount,
            TotalCtaClicks = totalCtaClicks,
            SupportCtaClicks = supportCtaClicks
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ProjectBriefPdf(CancellationToken cancellationToken)
    {
        var bytes = await _pdfDocumentService.GenerateProjectBriefPdfAsync(cancellationToken);
        return File(bytes, "application/pdf", "the-murder-stone-archive-project-brief.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ResearchPackOverviewPdf(CancellationToken cancellationToken)
    {
        var bytes = await _pdfDocumentService.GenerateResearchPackOverviewPdfAsync(cancellationToken);
        return File(bytes, "application/pdf", "research-pack-overview.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ResearchPackTimelinePdf(CancellationToken cancellationToken)
    {
        var bytes = await _pdfDocumentService.GenerateResearchPackTimelinePdfAsync(cancellationToken);
        return File(bytes, "application/pdf", "research-pack-timeline.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ResearchPackNotesPdf(CancellationToken cancellationToken)
    {
        var bytes = await _pdfDocumentService.GenerateResearchPackNotesPdfAsync(cancellationToken);
        return File(bytes, "application/pdf", "research-pack-notes.pdf");
    }

    [Authorize(Roles = AppConstants.AdminRole)]
    public async Task<IActionResult> Analytics()
    {
        var totalsByCta = await _context.CtaClickEvents
            .AsNoTracking()
            .GroupBy(e => e.CtaKey)
            .Select(g => new CtaTotalItem
            {
                CtaKey = g.Key,
                Clicks = g.Count()
            })
            .OrderByDescending(x => x.Clicks)
            .ToListAsync();

        var trendStartDate = DateTime.UtcNow.Date.AddDays(-29);
        var trendByDate = await _context.CtaClickEvents
            .AsNoTracking()
            .Where(e => e.ClickedAtUtc >= trendStartDate)
            .GroupBy(e => e.ClickedAtUtc.Date)
            .Select(g => new { Date = g.Key, Clicks = g.Count() })
            .ToDictionaryAsync(x => DateOnly.FromDateTime(x.Date), x => x.Clicks);

        var dailyTrend = Enumerable.Range(0, 30)
            .Select(offset => DateOnly.FromDateTime(trendStartDate.AddDays(offset)))
            .Select(day => new CtaDailyTrendItem
            {
                Date = day,
                Clicks = trendByDate.TryGetValue(day, out var count) ? count : 0
            })
            .ToList();

        var recentEvents = await _context.CtaClickEvents
            .AsNoTracking()
            .OrderByDescending(e => e.ClickedAtUtc)
            .Take(100)
            .Select(e => new CtaRecentEventItem
            {
                CtaKey = e.CtaKey,
                ClickedAtUtc = e.ClickedAtUtc,
                Path = e.Path,
                Referrer = e.Referrer,
                UserId = e.UserId
            })
            .ToListAsync();

        var vm = new CtaAnalyticsViewModel
        {
            TotalsByCta = totalsByCta,
            DailyTrend = dailyTrend,
            RecentEvents = recentEvents
        };

        return View(vm);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> TrackCta([FromBody] CtaTrackingRequest? request)
    {
        var ctaKey = request?.Cta?.Trim();
        if (string.IsNullOrWhiteSpace(ctaKey))
        {
            return NoContent();
        }

        if (ctaKey.Length > 120)
        {
            ctaKey = ctaKey[..120];
        }

        var clickEvent = new CtaClickEvent
        {
            CtaKey = ctaKey,
            ClickedAtUtc = DateTime.UtcNow,
            Path = request?.Path,
            Referrer = request?.Referrer,
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        };

        _context.CtaClickEvents.Add(clickEvent);
        await _context.SaveChangesAsync();

        _logger.LogInformation("CTA_CLICK: {Cta} path={Path}", clickEvent.CtaKey, clickEvent.Path);
        return NoContent();
    }

    public IActionResult DMCA()
    {
        return View();
    }

    [HttpGet]
    [Route("sitemap.xml")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Sitemap()
    {
        var items = await _context.MurderEvents
            .ApprovedAndNotLost()
            .OrderByDescending(m => m.Year)
            .ToListAsync();

        var hostname = Request.Scheme + "://" + Request.Host.Value.TrimEnd('/');

        var xml = new System.Text.StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");

        xml.AppendLine("  <url>");
        xml.AppendLine($"    <loc>{hostname}{Url.Action("Index", "Home")}</loc>");
        xml.AppendLine("  </url>");

        foreach (var it in items)
        {
            var loc = hostname + Url.Action("Details", "MurderEvents", new { id = it.Id });
            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{System.Security.SecurityElement.Escape(loc)}</loc>");
            xml.AppendLine($"    <lastmod>{(it.Year > 0 ? new DateTime(it.Year, 1, 1).ToString("yyyy-MM-dd") : DateTime.UtcNow.ToString("yyyy-MM-dd"))}</lastmod>");
            xml.AppendLine("  </url>");
        }

        xml.AppendLine("</urlset>");

        return Content(xml.ToString(), "application/xml");
    }

    private string? GetPatreonDonationUrl()
    {
        if (!string.IsNullOrWhiteSpace(_donationOptions.PatreonOneTimePaymentUrl))
        {
            return _donationOptions.PatreonOneTimePaymentUrl;
        }

        if (!string.IsNullOrWhiteSpace(_donationOptions.PatreonCampaignUrl))
        {
            return _donationOptions.PatreonCampaignUrl;
        }

        return null;
    }

    private string? GetFourthwallDonationUrl()
    {
        if (!string.IsNullOrWhiteSpace(_donationOptions.FourthwallOneTimePaymentUrl))
        {
            return _donationOptions.FourthwallOneTimePaymentUrl;
        }

        return null;
    }

    private async Task<DonationCampaign> EnsureDefaultCampaignAsync(CancellationToken cancellationToken = default)
    {
        var campaign = await _context.DonationCampaigns
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (campaign != null)
        {
            return campaign;
        }

        campaign = new DonationCampaign
        {
            Name = "Keep The Murder Stone Archive Running",
            Slug = "archive-sustainability",
            Description = "Support hosting, moderation, and research publication work for The Murder Stone Archive.",
            TargetAmountGbp = 750m,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            EndsAtUtc = DateTime.UtcNow.AddYears(1)
        };

        _context.DonationCampaigns.Add(campaign);
        await _context.SaveChangesAsync(cancellationToken);

        return campaign;
    }

    public sealed class CtaTrackingRequest
    {
        public string? Cta { get; set; }

        public string? Path { get; set; }

        public string? Referrer { get; set; }
    }
}
