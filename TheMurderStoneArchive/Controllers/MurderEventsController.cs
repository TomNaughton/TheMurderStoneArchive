using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;
using System;
using System.Linq;

namespace TheMurderStoneArchive.Controllers
{
    // Controller-level Admin lock removed so individual actions can declare their own requirements.
    public class MurderEventsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;

        public MurderEventsController(ApplicationDbContext context, IWebHostEnvironment env, Microsoft.Extensions.Configuration.IConfiguration configuration, System.Net.Http.IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _env = env;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        // GET: MurderEvents
        [AllowAnonymous] // Allows public visitors to see the list/index if needed
        public async Task<IActionResult> Index()
        {
            var events = await _context.MurderEvents
                .Include(m => m.Location)
                .ToListAsync();
            return View(events);
        }

        private async Task<bool> VerifyReCaptchaAsync(string token, string expectedAction = null, double minScore = 0.5)
        {
            try
            {
                var secret = _configuration["ReCaptcha:SecretKey"];
                if (string.IsNullOrEmpty(secret)) return false;
                var client = _httpClientFactory.CreateClient();
                var values = new System.Collections.Generic.Dictionary<string, string>
                {
                    {"secret", secret},
                    {"response", token}
                };
                var content = new System.Net.Http.FormUrlEncodedContent(values);
                var resp = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);
                if (!resp.IsSuccessStatusCode) return false;
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("success", out var success) || !success.GetBoolean())
                    return false;

                double score = 0.0;
                if (doc.RootElement.TryGetProperty("score", out var scoreElem) && scoreElem.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    score = scoreElem.GetDouble();
                }

                if (score < minScore)
                    return false;

                if (!string.IsNullOrEmpty(expectedAction))
                {
                    if (doc.RootElement.TryGetProperty("action", out var actionElem))
                    {
                        var action = actionElem.GetString();
                        if (!string.Equals(action, expectedAction, StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        // GET: MurderEvents/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var murderEvent = await _context.MurderEvents
                .Include(m => m.Location)
                .Include(m => m.Monuments)
                .Include(m => m.Perpetrators)
                .Include(m => m.Photos)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (murderEvent == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && !murderEvent.IsApproved && murderEvent.CreatedById != userId)
                return Forbid();

            return View(murderEvent);
        }

        // GET: MurderEvents/Create
        [Authorize]
        public IActionResult Create()
        {
            return View();
        }

        // POST: MurderEvents/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create(MurderEvent murderEvent, List<IFormFile>? Photos)
        {
            const int MaxFiles = 10;
            const long MaxFileSize = 25L * 1024L * 1024L; // 25 MB per file
            var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

            if (Photos != null && Photos.Count > MaxFiles)
                ModelState.AddModelError("Photos", $"You may upload up to {MaxFiles} photos.");

            if (!murderEvent.ConfirmRightsAndTerms)
            {
                ModelState.AddModelError("ConfirmRightsAndTerms", "You must confirm you have the rights to upload these photos and accept the Terms and Privacy policy.");
            }

            // Validate files server-side before saving
            if (Photos != null && Photos.Count > 0)
            {
                foreach (var file in Photos)
                {
                    if (file.Length == 0) continue;
                    if (file.Length > MaxFileSize)
                    {
                        ModelState.AddModelError("Photos", $"File {file.FileName} exceeds the {MaxFileSize / (1024*1024)} MB limit.");
                        break;
                    }
                    if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError("Photos", $"File {file.FileName} is not a valid image.");
                        break;
                    }
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!allowedExt.Contains(ext))
                    {
                        ModelState.AddModelError("Photos", $"File {file.FileName} has an unsupported extension.");
                        break;
                    }
                }
            }

            // Validate files server-side before saving
            if (Photos != null && Photos.Count > 0)
            {
                foreach (var file in Photos)
                {
                    if (file.Length == 0) continue;
                    if (file.Length > MaxFileSize)
                    {
                        ModelState.AddModelError("Photos", $"File {file.FileName} exceeds the {MaxFileSize / (1024*1024)} MB limit.");
                        break;
                    }
                    if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError("Photos", $"File {file.FileName} is not a valid image.");
                        break;
                    }
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!allowedExt.Contains(ext))
                    {
                        ModelState.AddModelError("Photos", $"File {file.FileName} has an unsupported extension.");
                        break;
                    }
                }
            }

            if (!murderEvent.ConfirmRightsAndTerms)
            {
                ModelState.AddModelError("ConfirmRightsAndTerms", "You must confirm you have the rights to upload these photos and accept the Terms and Privacy policy.");
            }

            if (ModelState.IsValid)
            {
                // link to creator if authenticated
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId)) murderEvent.CreatedById = userId;

                // record consent timestamp
                if (murderEvent.ConfirmRightsAndTerms) murderEvent.ConsentDateUtc = DateTime.UtcNow;

                _context.Add(murderEvent);
                await _context.SaveChangesAsync();

                // Process uploaded photos after saving to get an Id
                if (Photos != null && Photos.Count > 0)
                {
                    foreach (var file in Photos)
                    {
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

                        // Read file into memory and store in the database
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
                            Data = bytes
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }
            return View(murderEvent);
        }

        // GET: MurderEvents/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var murderEvent = await _context.MurderEvents
                .Include(m => m.Location)
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
        public async Task<IActionResult> Edit(int id, MurderEvent murderEvent, List<IFormFile>? Photos)
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
                    const int MaxFiles = 10;
                    const long MaxFileSize = 25L * 1024L * 1024L; // 25 MB
                    var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

                    var existingCount = await _context.MurderEventPhotos.CountAsync(p => p.MurderEventId == id);
                    if (Photos != null && (existingCount + Photos.Count) > MaxFiles)
                    {
                        ModelState.AddModelError("Photos", $"Total photos cannot exceed {MaxFiles}. You already have {existingCount}.");
                        return View(murderEvent);
                    }

                    // Validate files server-side before committing update
                    if (Photos != null && Photos.Count > 0)
                    {
                        foreach (var file in Photos)
                        {
                            if (file.Length == 0) continue;
                            if (file.Length > MaxFileSize)
                            {
                                ModelState.AddModelError("Photos", $"File {file.FileName} exceeds the {MaxFileSize / (1024*1024)} MB limit.");
                                return View(murderEvent);
                            }
                            if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                            {
                                ModelState.AddModelError("Photos", $"File {file.FileName} is not a valid image.");
                                return View(murderEvent);
                            }
                            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                            if (!allowedExt.Contains(ext))
                            {
                                ModelState.AddModelError("Photos", $"File {file.FileName} has an unsupported extension.");
                                return View(murderEvent);
                            }
                        }
                    }

                    _context.Update(murderEvent);
                    await _context.SaveChangesAsync();

                    if (Photos != null && Photos.Count > 0)
                    {
                        foreach (var file in Photos)
                        {
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
                                Data = bytes
                            });
                        }
                        await _context.SaveChangesAsync();
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.MurderEvents.Any(e => e.Id == murderEvent.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
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
        public async Task<IActionResult> Submit(MurderEvent murderEvent, List<IFormFile>? Photos)
        {
            // Verify reCAPTCHA v3 token (action: submit)
            var token = Request.Form["g-recaptcha-response"].ToString();
            if (string.IsNullOrEmpty(token) || !await VerifyReCaptchaAsync(token, "submit"))
            {
                ModelState.AddModelError(string.Empty, "Captcha verification failed. Please try again.");
                return View(murderEvent);
            }
            // Force public submissions to require moderation
            murderEvent.IsApproved = false;

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
                    foreach (var file in Photos)
                    {
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
                            Data = bytes
                        });
                    }
                    await _context.SaveChangesAsync();
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
    }
}