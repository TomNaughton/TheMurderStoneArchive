using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Models;

namespace TheMurderStoneArchive.Controllers
{
    [Authorize(Roles = "Admin")] // Locks down the entire controller to admins only
    public class MurderEventsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MurderEventsController(ApplicationDbContext context)
        {
            _context = context;
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

        // GET: MurderEvents/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var murderEvent = await _context.MurderEvents
                .Include(m => m.Location)
                .Include(m => m.Monuments)
                .Include(m => m.Perpetrators)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (murderEvent == null) return NotFound();

            return View(murderEvent);
        }

        // GET: MurderEvents/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: MurderEvents/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MurderEvent murderEvent)
        {
            if (ModelState.IsValid)
            {
                _context.Add(murderEvent);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(murderEvent);
        }

        // GET: MurderEvents/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var murderEvent = await _context.MurderEvents
                .Include(m => m.Location)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (murderEvent == null) return NotFound();

            return View(murderEvent);
        }

        // POST: MurderEvents/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MurderEvent murderEvent)
        {
            if (id != murderEvent.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(murderEvent);
                    await _context.SaveChangesAsync();
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
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var murderEvent = await _context.MurderEvents
                .Include(m => m.Location)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (murderEvent == null) return NotFound();

            return View(murderEvent);
        }

        // POST: MurderEvents/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var murderEvent = await _context.MurderEvents.FindAsync(id);
            if (murderEvent != null)
            {
                _context.MurderEvents.Remove(murderEvent);
            }

            await _context.SaveChangesAsync();
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
        public async Task<IActionResult> Submit(MurderEvent murderEvent)
        {
            // Force public submissions to require moderation
            murderEvent.IsApproved = false;

            if (ModelState.IsValid)
            {
                _context.Add(murderEvent);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(SubmissionThankYou));
            }
            return View(murderEvent);
        }

        [AllowAnonymous]
        public IActionResult SubmissionThankYou()
        {
            return Content("Thank you! Your submission has been received and is pending admin review.");
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