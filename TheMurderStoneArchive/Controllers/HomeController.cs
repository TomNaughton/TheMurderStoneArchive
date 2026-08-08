using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TheMurderStoneArchive.Data;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index()
    {
        // Fetch all events including the location for the map markers
        var events = await _context.MurderEvents
        .Include(m => m.Location)
        .Where(m => m.IsApproved && !m.IsLost) // Hide unapproved submissions and lost stones from public view
        .ToListAsync();

        return View(events);
    }

    [HttpGet]
    [Route("sitemap.xml")]
    public async Task<IActionResult> Sitemap()
    {
        var items = await _context.MurderEvents
            .Where(m => m.IsApproved && !m.IsLost)
            .OrderByDescending(m => m.Year)
            .ToListAsync();

        var hostname = Request.Scheme + "://" + Request.Host.Value.TrimEnd('/');

        var xml = new System.Text.StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");

        // Add homepage
        xml.AppendLine("  <url>");
        xml.AppendLine($"    <loc>{hostname}{Url.Action("Index", "Home")}</loc>");
        xml.AppendLine("  </url>");

        foreach (var it in items)
        {
            var loc = hostname + Url.Action("Details", "MurderEvents", new { id = it.Id });
            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{System.Security.SecurityElement.Escape(loc)}</loc>");
            xml.AppendLine($"    <lastmod>{(it.Year > 0 ? new DateTime(it.Year,1,1).ToString("yyyy-MM-dd") : DateTime.UtcNow.ToString("yyyy-MM-dd"))}</lastmod>");
            xml.AppendLine("  </url>");
        }

        xml.AppendLine("</urlset>");

        return Content(xml.ToString(), "application/xml");
    }
}