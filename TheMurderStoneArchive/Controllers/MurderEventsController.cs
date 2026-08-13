using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Security.Claims;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Helpers;
using TheMurderStoneArchive.Models;
using TheMurderStoneArchive.Services;

namespace TheMurderStoneArchive.Controllers
{
    // Controller-level Admin lock removed so individual actions can declare their own requirements.
    public class MurderEventsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MurderEventsController> _logger;
        private readonly IMurderEventService _murderEventService;
        private readonly UserManager<ApplicationUser> _userManager;

        public MurderEventsController(
            ApplicationDbContext context, 
            IWebHostEnvironment env, 
            Microsoft.Extensions.Configuration.IConfiguration configuration, 
            System.Net.Http.IHttpClientFactory httpClientFactory,
            ILogger<MurderEventsController> logger,
            IMurderEventService murderEventService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _env = env;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _murderEventService = murderEventService;
            _userManager = userManager;
        }

        // GET: MurderEvents
        [AllowAnonymous] // Allows public visitors to see the list/index if needed
        public async Task<IActionResult> Index(string? searchTerm, string sortOrder = AppConstants.SortOrderTitle, int page = AppConstants.DefaultPage)
        {
            var currentUserId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;

            var (events, totalEvents) = await _murderEventService.GetEventsAsync(
                searchTerm: searchTerm,
                sortOrder: sortOrder,
                page: page,
                pageSize: AppConstants.DefaultPageSize,
                currentUserId: currentUserId);

            // Create and populate the view model
            var viewModel = new MurderEventsIndexViewModel
            {
                Events = events,
                SearchTerm = searchTerm,
                SortOrder = sortOrder,
                CurrentPage = page,
                PageSize = AppConstants.DefaultPageSize,
                TotalEvents = totalEvents
            };

            return View(viewModel);
        }

        // API: Get Murder Events as JSON
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetEvents(string? searchTerm, string sortOrder = "title", int page = 1)
        {
            var currentUserId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
            const int pageSize = 10;

            // Delegate to the shared service so search/sort/pagination logic isn't duplicated
            var (events, totalEvents) = await _murderEventService.GetEventsAsync(
                searchTerm: searchTerm,
                sortOrder: sortOrder,
                page: page,
                pageSize: pageSize,
                currentUserId: currentUserId);

            var eventDtos = events.Select(m => new MurderEventDto
            {
                Id = m.Id,
                Title = m.Title,
                Year = m.Year,
                Description = m.Description,
                Location = m.Location == null ? null : new LocationDto
                {
                    Id = m.Location.Id,
                    Name = m.Location.Name,
                    Latitude = m.Location.Latitude,
                    Longitude = m.Location.Longitude
                }
            });

            // Create and populate the API view model (DTO)
            var apiViewModel = new MurderEventsIndexApiViewModel
            {
                Events = eventDtos,
                SearchTerm = searchTerm, // Return the original search term, not the lowercased version
                SortOrder = sortOrder,
                CurrentPage = page,
                PageSize = pageSize,
                TotalEvents = totalEvents
            };

            return Json(apiViewModel);
        }

        // GET: MurderEvents/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var murderEvent = await _murderEventService.GetEventByIdAsync(id.Value, userId);

            if (murderEvent == null) return NotFound();

            if (!User.IsInRole(AppConstants.AdminRole) && !murderEvent.IsApproved && murderEvent.CreatedById != userId)
                return Forbid();

            return View(murderEvent);
        }

        // GET: MurderEvents/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var murderEvent = await _context.MurderEvents
                .Include(m => m.Location)
                .Include(m => m.Photos)
                .Include(m => m.Videos)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (murderEvent == null) return NotFound();

            // Only the creator or an admin can edit
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && murderEvent.CreatedById != userId)
                return Forbid();

            return View(murderEvent);
        }

        // POST: MurderEvents/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(int id, MurderEvent murderEvent, List<IFormFile>? Photos, List<string>? PhotoAttributions, List<string>? YouTubeLinks, List<int>? DeletedPhotoIds, Dictionary<string, string>? ExistingPhotoAttributions)
        {
            if (id != murderEvent.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Ensure only creator or admin can edit
                    var existing = await _context.MurderEvents.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
                    if (existing == null) return NotFound();
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (!User.IsInRole("Admin") && existing.CreatedById != userId)
                        return Forbid();

                    // Preserve creator info in case it wasn't in the posted model
                    murderEvent.CreatedById = existing.CreatedById;

                    // Handle uploaded photos (ensure max 10 total)
                    // Compute existing photo count excluding any photos the user marked for deletion
                    var existingCount = await _context.MurderEventPhotos.CountAsync(p => p.MurderEventId == id && (DeletedPhotoIds == null || !DeletedPhotoIds.Contains(p.Id)));
                    if (Photos != null && (existingCount + Photos.Count) > PhotoValidationConstants.MaxFiles)
                    {
                        ModelState.AddModelError("Photos", $"Total photos cannot exceed {PhotoValidationConstants.MaxFiles}. You already have {existingCount}.");
                        return View(murderEvent);
                    }

                    // Validate files server-side before committing update
                    if (Photos != null && Photos.Count > 0)
                    {
                        PhotoValidationHelper.ValidatePhotoFiles(Photos, ModelState);
                        if (!ModelState.IsValid)
                            return View(murderEvent);
                    }

                    // If user marked existing photos for deletion, mark them for removal in the context
                    if (DeletedPhotoIds != null && DeletedPhotoIds.Count > 0)
                    {
                        var toDelete = _context.MurderEventPhotos.Where(p => DeletedPhotoIds.Contains(p.Id) && p.MurderEventId == id);
                        _context.MurderEventPhotos.RemoveRange(toDelete);
                    }

                    // Update attribution text on existing photos that weren't deleted
                    if (ExistingPhotoAttributions != null && ExistingPhotoAttributions.Count > 0)
                    {
                        // Keys arrive as strings from the form ("ExistingPhotoAttributions[5]=text")
                        var parsedAttributions = ExistingPhotoAttributions
                            .Where(kv => int.TryParse(kv.Key, out _))
                            .ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value);

                        var existingPhotos = await _context.MurderEventPhotos
                            .Where(p => p.MurderEventId == id && parsedAttributions.Keys.Contains(p.Id))
                            .ToListAsync();
                        foreach (var photo in existingPhotos)
                        {
                            if (parsedAttributions.TryGetValue(photo.Id, out var attribution))
                                photo.Attribution = string.IsNullOrWhiteSpace(attribution) ? null : attribution.Trim();
                        }
                    }

                    _context.Update(murderEvent);

                    // Add new photos
                    if (Photos != null && Photos.Count > 0)
                    {
                        for (int i = 0; i < Photos.Count; i++)
                        {
                            var file = Photos[i];
                            if (file.Length == 0) continue;

                            using var ms = new MemoryStream();
                            await file.CopyToAsync(ms);
                            var bytes = ms.ToArray();

                            _context.MurderEventPhotos.Add(new MurderEventPhoto
                            {
                                MurderEventId = murderEvent.Id,
                                FileName = file.FileName,
                                FilePath = string.Empty,
                                ContentType = file.ContentType,
                                FileSize = file.Length,
                                Data = bytes,
                                Attribution = PhotoAttributions != null && i < PhotoAttributions.Count && !string.IsNullOrWhiteSpace(PhotoAttributions[i])
                                    ? PhotoAttributions[i].Trim()
                                    : null
                            });
                        }
                    }

                    // Replace existing YouTube video links if the edit form posted them
                    if (YouTubeLinks != null)
                    {
                        var youtubeList = YouTubeLinks.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                        await YouTubeVideoHelper.ProcessYouTubeLinksAsync(_context, id, youtubeList, _murderEventService.ExtractYouTubeId);
                    }

                    // Persist all pending changes (deletions, updates, new photos) in a single transaction.
                    // SaveChangesAsync inside ProcessYouTubeLinksAsync may not fire when no video changes
                    // are needed, so we always save here to guarantee all other changes are committed.
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.MurderEvents.Any(e => e.Id == murderEvent.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Details), new { id = murderEvent.Id });
            }
            return View(murderEvent);
        }

        // GET: MurderEvents/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var murderEvent = await _context.MurderEvents
                .Include(m => m.Location)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (murderEvent == null) return NotFound();

            // Only the creator or an admin can delete
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && murderEvent.CreatedById != userId)
                return Forbid();

            return View(murderEvent);
        }

        // POST: MurderEvents/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var murderEvent = await _context.MurderEvents.FindAsync(id);
            if (murderEvent != null)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!User.IsInRole("Admin") && murderEvent.CreatedById != userId)
                    return Forbid();

                _context.MurderEvents.Remove(murderEvent);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: MurderEvents/Submit (Require authenticated users to submit)
        [Authorize]
        public IActionResult Submit()
        {
            return View();
        }

        // POST: MurderEvents/Submit
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(MurderEvent murderEvent, List<IFormFile>? Photos, List<string>? PhotoAttributions, List<string>? YouTubeLinks)
        {
            // Verify reCAPTCHA v3 token (action: submit)
            var token = Request.Form["g-recaptcha-response"].ToString();
            if (!_env.IsDevelopment() && (string.IsNullOrEmpty(token) || !await _murderEventService.VerifyReCaptchaAsync(token, "submit")))
            {
                ModelState.AddModelError(string.Empty, "Captcha verification failed. Please try again.");
                return View(murderEvent);
            }
            // Force public submissions to require moderation
            murderEvent.IsApproved = User.IsInRole("Admin");

            const int MaxFiles = 10;
            const long MaxFileSize = 25L * 1024L * 1024L; // 25 MB per file
            var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

            if (Photos != null && Photos.Count > MaxFiles)
                ModelState.AddModelError("Photos", $"You may upload up to {MaxFiles} photos.");

            if (ModelState.IsValid)
            {
                // Attach creator id
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId)) murderEvent.CreatedById = userId;

                if (murderEvent.ConfirmRightsAndTerms) murderEvent.ConsentDateUtc = DateTime.UtcNow;

                _context.Add(murderEvent);
                await _context.SaveChangesAsync();

                if (Photos != null && Photos.Count > 0)
                {
                    for (int i = 0; i < Photos.Count; i++)
                    {
                        var file = Photos[i];
                        if (file.Length == 0) continue;
                        if (file.Length > MaxFileSize)
                        {
                            ModelState.AddModelError("Photos", $"File {file.FileName} exceeds the {MaxFileSize / (1024*1024)} MB limit.");
                            continue;
                        }

                        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                        {
                            ModelState.AddModelError("Photos", $"File {file.FileName} is not a valid image.");
                            continue;
                        }

                        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                        if (!allowedExt.Contains(ext))
                        {
                            ModelState.AddModelError("Photos", $"File {file.FileName} has an unsupported extension.");
                            continue;
                        }

                        // Read file into memory and store in the database rather than writing to disk
                        using var ms = new MemoryStream();
                        await file.CopyToAsync(ms);
                        var bytes = ms.ToArray();

                        _context.MurderEventPhotos.Add(new MurderEventPhoto
                        {
                            MurderEventId = murderEvent.Id,
                            FileName = file.FileName,
                            FilePath = string.Empty,
                            ContentType = file.ContentType,
                            FileSize = file.Length,
                            Data = bytes,
                            Attribution = PhotoAttributions != null && i < PhotoAttributions.Count && !string.IsNullOrWhiteSpace(PhotoAttributions[i])
                                ? PhotoAttributions[i].Trim()
                                : null
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                // Process YouTube links using helper
                if (YouTubeLinks != null && YouTubeLinks.Count > 0)
                {
                    await YouTubeVideoHelper.ProcessYouTubeLinksAsync(_context, murderEvent.Id, YouTubeLinks, _murderEventService.ExtractYouTubeId);
                }

                return RedirectToAction(nameof(SubmissionThankYou));
            }
            return View(murderEvent);
        }

        // GET: MurderEvents/Photo/5
        [AllowAnonymous]
        public async Task<IActionResult> Photo(int id)
        {
            var photo = await _context.MurderEventPhotos.FindAsync(id);
            if (photo == null) return NotFound();

            if (photo.Data != null && photo.Data.Length > 0)
                return File(photo.Data, photo.ContentType);

            // Fallback to filesystem if a FilePath is present
            if (!string.IsNullOrEmpty(photo.FilePath))
            {
                var physical = Path.Combine(_env.WebRootPath, photo.FilePath.Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(physical))
                    return PhysicalFile(physical, photo.ContentType);
            }
            return NotFound();
        }

        [AllowAnonymous]
        public IActionResult SubmissionThankYou()
        {
            // Redirect back to the index and show a client-side confirmation toast via TempData
            TempData["SubmissionSuccessMessage"] = "Thank you! Your submission has been received and is pending admin review.";
            return RedirectToAction(nameof(Index));
        }

        // GET: MurderEvents/Pending
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Pending()
        {
            var pendingEvents = await _context.MurderEvents
                .Include(m => m.Location)
                .Where(m => !m.IsApproved)
                .ToListAsync();
            return View(pendingEvents);
        }

        // POST: MurderEvents/Approve/5
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(int id)
        {
            var murderEvent = await _context.MurderEvents.FindAsync(id);
            if (murderEvent != null)
            {
                murderEvent.IsApproved = true;
                _context.Update(murderEvent);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Pending));
        }

        // POST: MurderEvents/AddComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AddComment(int murderEventId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["CommentError"] = "Comment cannot be empty.";
                return RedirectToAction(nameof(Details), new { id = murderEventId });
            }

            var eventExists = await _context.MurderEvents.AnyAsync(m => m.Id == murderEventId);
            if (!eventExists) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Forbid();

            _context.MurderEventComments.Add(new MurderEventComment
            {
                MurderEventId = murderEventId,
                Content = content.Trim(),
                UserId = userId,
                CreatedUtc = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = murderEventId });
        }

        // GET: MurderEvents/SuggestEdit/5
        [Authorize]
        public async Task<IActionResult> SuggestEdit(int? id)
        {
            if (id == null) return NotFound();

            var murderEvent = await _context.MurderEvents
                .Include(m => m.Location)
                .Include(m => m.Photos)
                .Include(m => m.Videos)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (murderEvent == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // Only non-owners and non-admins go through the suggestion workflow;
            // owners/admins should use the direct Edit action instead.
            if (User.IsInRole(AppConstants.AdminRole) || murderEvent.CreatedById == userId)
                return RedirectToAction(nameof(Edit), new { id });

            var suggestion = new MurderEventEditSuggestion
            {
                MurderEventId = murderEvent.Id,
                ProposedTitle = murderEvent.Title,
                ProposedYear = murderEvent.Year,
                ProposedDescription = murderEvent.Description,
                ProposedCategory = murderEvent.Category,
                ProposedIsProtected = murderEvent.IsProtected,
                ProposedIsLost = murderEvent.IsLost,
                ProposedLocationName = murderEvent.Location?.Name,
                ProposedLatitude = murderEvent.Location?.Latitude ?? 0,
                ProposedLongitude = murderEvent.Location?.Longitude ?? 0
            };

            ViewBag.MurderEvent = murderEvent;
            return View(suggestion);
        }

        // POST: MurderEvents/SuggestEdit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SuggestEdit(int id, MurderEventEditSuggestion suggestion, List<IFormFile>? Photos, List<string>? PhotoAttributions, List<string>? YouTubeLinks, List<int>? DeletedPhotoIds)
        {
            var murderEvent = await _context.MurderEvents
                .Include(m => m.Location)
                .Include(m => m.Photos)
                .Include(m => m.Videos)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (murderEvent == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (User.IsInRole(AppConstants.AdminRole) || murderEvent.CreatedById == userId)
                return RedirectToAction(nameof(Edit), new { id });

            if (string.IsNullOrEmpty(userId)) return Forbid();

            // Validate proposed total photo count (existing minus proposed-deleted, plus newly proposed)
            var existingCount = murderEvent.Photos.Count(p => DeletedPhotoIds == null || !DeletedPhotoIds.Contains(p.Id));
            if (Photos != null && (existingCount + Photos.Count) > PhotoValidationConstants.MaxFiles)
            {
                ModelState.AddModelError("Photos", $"Total photos cannot exceed {PhotoValidationConstants.MaxFiles}. You already have {existingCount}.");
            }

            if (Photos != null && Photos.Count > 0)
            {
                PhotoValidationHelper.ValidatePhotoFiles(Photos, ModelState);
            }

            if (!ModelState.IsValid)
            {
                ViewBag.MurderEvent = murderEvent;
                return View(suggestion);
            }

            var entry = new MurderEventEditSuggestion
            {
                MurderEventId = murderEvent.Id,
                SubmittedById = userId,
                SubmittedUtc = DateTime.UtcNow,
                Status = EditSuggestionStatus.Pending,
                ProposedTitle = suggestion.ProposedTitle,
                ProposedYear = suggestion.ProposedYear,
                ProposedDescription = suggestion.ProposedDescription,
                ProposedCategory = suggestion.ProposedCategory,
                ProposedIsProtected = suggestion.ProposedIsProtected,
                ProposedIsLost = suggestion.ProposedIsLost,
                ProposedLocationName = suggestion.ProposedLocationName,
                ProposedLatitude = suggestion.ProposedLatitude,
                ProposedLongitude = suggestion.ProposedLongitude,
                ProposedDeletedPhotoIds = DeletedPhotoIds != null && DeletedPhotoIds.Count > 0 ? string.Join(",", DeletedPhotoIds) : null,
                SubmissionNote = suggestion.SubmissionNote
            };

            if (Photos != null && Photos.Count > 0)
            {
                for (int i = 0; i < Photos.Count; i++)
                {
                    var file = Photos[i];
                    if (file.Length == 0) continue;

                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    var bytes = ms.ToArray();

                    entry.ProposedPhotos.Add(new MurderEventEditSuggestionPhoto
                    {
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        FileSize = file.Length,
                        Data = bytes,
                        Attribution = PhotoAttributions != null && i < PhotoAttributions.Count && !string.IsNullOrWhiteSpace(PhotoAttributions[i])
                            ? PhotoAttributions[i].Trim()
                            : null
                    });
                }
            }

            if (YouTubeLinks != null)
            {
                foreach (var link in YouTubeLinks.Where(l => !string.IsNullOrWhiteSpace(l)))
                {
                    var videoId = _murderEventService.ExtractYouTubeId(link);
                    if (videoId == null) continue;

                    entry.ProposedVideos.Add(new MurderEventEditSuggestionVideo
                    {
                        Url = link,
                        VideoId = videoId
                    });
                }
            }

            _context.MurderEventEditSuggestions.Add(entry);
            await _context.SaveChangesAsync();

            TempData["SubmissionSuccessMessage"] = "Thank you! Your suggested edit has been submitted and is pending admin review.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: MurderEvents/SuggestionPhoto/5
        [AllowAnonymous]
        public async Task<IActionResult> SuggestionPhoto(int id)
        {
            var photo = await _context.MurderEventEditSuggestionPhotos.FindAsync(id);
            if (photo == null) return NotFound();

            if (photo.Data != null && photo.Data.Length > 0)
                return File(photo.Data, photo.ContentType);

            return NotFound();
        }

        // GET: MurderEvents/PendingSuggestions
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PendingSuggestions()
        {
            var suggestions = await _context.MurderEventEditSuggestions
                .Include(s => s.MurderEvent)
                .Include(s => s.SubmittedBy)
                .Include(s => s.ProposedPhotos)
                .Include(s => s.ProposedVideos)
                .Where(s => s.Status == EditSuggestionStatus.Pending)
                .OrderBy(s => s.SubmittedUtc)
                .ToListAsync();
            return View(suggestions);
        }

        // POST: MurderEvents/ApproveSuggestion/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApproveSuggestion(int id)
        {
            var suggestion = await _context.MurderEventEditSuggestions
                .Include(s => s.MurderEvent).ThenInclude(m => m.Location)
                .Include(s => s.ProposedPhotos)
                .Include(s => s.ProposedVideos)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (suggestion == null) return NotFound();
            if (suggestion.Status != EditSuggestionStatus.Pending) return RedirectToAction(nameof(PendingSuggestions));

            var murderEvent = suggestion.MurderEvent;
            var changes = new List<string>();

            if (murderEvent.Title != suggestion.ProposedTitle)
                changes.Add($"Title: '{murderEvent.Title}' -> '{suggestion.ProposedTitle}'");
            if (murderEvent.Year != suggestion.ProposedYear)
                changes.Add($"Year: {murderEvent.Year} -> {suggestion.ProposedYear}");
            if (murderEvent.Description != suggestion.ProposedDescription)
                changes.Add("Description updated");
            if (murderEvent.Category != suggestion.ProposedCategory)
                changes.Add($"Category: {murderEvent.Category} -> {suggestion.ProposedCategory}");
            if (murderEvent.IsProtected != suggestion.ProposedIsProtected)
                changes.Add($"Protected: {murderEvent.IsProtected} -> {suggestion.ProposedIsProtected}");
            if (murderEvent.IsLost != suggestion.ProposedIsLost)
                changes.Add($"Lost: {murderEvent.IsLost} -> {suggestion.ProposedIsLost}");

            murderEvent.Title = suggestion.ProposedTitle;
            murderEvent.Year = suggestion.ProposedYear;
            murderEvent.Description = suggestion.ProposedDescription;
            murderEvent.Category = suggestion.ProposedCategory;
            murderEvent.IsProtected = suggestion.ProposedIsProtected;
            murderEvent.IsLost = suggestion.ProposedIsLost;
            murderEvent.ModifiedUtc = DateTime.UtcNow;

            if (murderEvent.Location != null && !string.IsNullOrEmpty(suggestion.ProposedLocationName))
            {
                if (murderEvent.Location.Name != suggestion.ProposedLocationName ||
                    murderEvent.Location.Latitude != suggestion.ProposedLatitude ||
                    murderEvent.Location.Longitude != suggestion.ProposedLongitude)
                {
                    changes.Add("Location updated");
                }
                murderEvent.Location.Name = suggestion.ProposedLocationName;
                murderEvent.Location.Latitude = suggestion.ProposedLatitude;
                murderEvent.Location.Longitude = suggestion.ProposedLongitude;
            }

            // Apply proposed photo deletions
            if (!string.IsNullOrEmpty(suggestion.ProposedDeletedPhotoIds))
            {
                var deletedIds = suggestion.ProposedDeletedPhotoIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s, out var pid) ? pid : (int?)null)
                    .Where(pid => pid.HasValue)
                    .Select(pid => pid!.Value)
                    .ToList();
                if (deletedIds.Count > 0)
                {
                    var toDelete = _context.MurderEventPhotos.Where(p => deletedIds.Contains(p.Id) && p.MurderEventId == murderEvent.Id);
                    _context.MurderEventPhotos.RemoveRange(toDelete);
                    changes.Add($"{deletedIds.Count} photo(s) removed");
                }
            }

            // Copy proposed photos into real photos
            if (suggestion.ProposedPhotos.Count > 0)
            {
                foreach (var proposedPhoto in suggestion.ProposedPhotos)
                {
                    _context.MurderEventPhotos.Add(new MurderEventPhoto
                    {
                        MurderEventId = murderEvent.Id,
                        FileName = proposedPhoto.FileName,
                        FilePath = string.Empty,
                        ContentType = proposedPhoto.ContentType,
                        FileSize = proposedPhoto.FileSize,
                        Data = proposedPhoto.Data,
                        Attribution = proposedPhoto.Attribution
                    });
                }
                changes.Add($"{suggestion.ProposedPhotos.Count} photo(s) added");
            }

            // Replace videos with proposed set, if any were proposed
            if (suggestion.ProposedVideos.Count > 0)
            {
                var youtubeList = suggestion.ProposedVideos.Select(v => v.Url).ToList();
                await YouTubeVideoHelper.ProcessYouTubeLinksAsync(_context, murderEvent.Id, youtubeList, _murderEventService.ExtractYouTubeId);
                changes.Add("Videos updated");
            }

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            murderEvent.ModifiedById = adminId;

            suggestion.Status = EditSuggestionStatus.Approved;
            suggestion.ReviewedById = adminId;
            suggestion.ReviewedUtc = DateTime.UtcNow;

            _context.MurderEventChangeLogEntries.Add(new MurderEventChangeLogEntry
            {
                MurderEventId = murderEvent.Id,
                ContributorId = suggestion.SubmittedById,
                ApprovedById = adminId ?? string.Empty,
                ChangeUtc = DateTime.UtcNow,
                Summary = changes.Count > 0 ? string.Join("; ", changes) : "No field changes detected"
            });

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(PendingSuggestions));
        }

        // POST: MurderEvents/RejectSuggestion/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RejectSuggestion(int id, string? reviewNotes)
        {
            var suggestion = await _context.MurderEventEditSuggestions.FindAsync(id);
            if (suggestion == null) return NotFound();
            if (suggestion.Status != EditSuggestionStatus.Pending) return RedirectToAction(nameof(PendingSuggestions));

            suggestion.Status = EditSuggestionStatus.Rejected;
            suggestion.ReviewedById = User.FindFirstValue(ClaimTypes.NameIdentifier);
            suggestion.ReviewedUtc = DateTime.UtcNow;
            suggestion.ReviewNotes = reviewNotes;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(PendingSuggestions));
        }

        // GET: MurderEvents/ChangeLog/5
        [AllowAnonymous]
        public async Task<IActionResult> ChangeLog(int id)
        {
            var murderEvent = await _context.MurderEvents.FirstOrDefaultAsync(m => m.Id == id);
            if (murderEvent == null) return NotFound();

            var entries = await _context.MurderEventChangeLogEntries
                .Include(e => e.Contributor)
                .Include(e => e.ApprovedBy)
                .Where(e => e.MurderEventId == id)
                .OrderByDescending(e => e.ChangeUtc)
                .ToListAsync();

            ViewBag.MurderEvent = murderEvent;
            return View(entries);
        }
    }
}