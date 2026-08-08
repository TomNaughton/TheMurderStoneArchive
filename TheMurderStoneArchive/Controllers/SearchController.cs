using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Models;

namespace TheMurderStoneArchive.Controllers
{
    public class SearchController : Controller
    {
        private readonly ApplicationDbContext _context;
        public SearchController(ApplicationDbContext context) => _context = context;

        public async Task<IActionResult> Index(string q, int page = 1)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                ViewData["Query"] = string.Empty;
                return View(new List<MurderEvent>());
            }

            ViewData["Query"] = q;

            var query = q.Trim().ToUpper();
            var results = await _context.MurderEvents
                .Include(m => m.Location)
                .Where(m => m.IsApproved && !m.IsLost && (
                    EF.Functions.Like(m.Title.ToUpper(), $"%{query}%") ||
                    EF.Functions.Like(m.Description.ToUpper(), $"%{query}%") ||
                    EF.Functions.Like(m.Location.Name.ToUpper(), $"%{query}%")
                ))
                .OrderByDescending(m => m.Year)
                .Take(200)
                .ToListAsync();

            return View(results);
        }
    }
}
