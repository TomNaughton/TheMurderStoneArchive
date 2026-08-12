using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TheMurderStoneArchive.Data;

namespace TheMurderStoneArchive.ViewComponents
{
    public class PendingSubmissionsCountViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public PendingSubmissionsCountViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            int count = await _context.MurderEvents.CountAsync(s => s.IsApproved == false);

            return View(count);
        }
    }
}
