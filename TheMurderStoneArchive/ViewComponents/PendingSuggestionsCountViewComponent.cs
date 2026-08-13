using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TheMurderStoneArchive.Data;

namespace TheMurderStoneArchive.ViewComponents
{
    public class PendingSuggestionsCountViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public PendingSuggestionsCountViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            int count = await _context.MurderEventEditSuggestions.CountAsync(s => s.Status == Models.EditSuggestionStatus.Pending);

            return View(count);
        }
    }
}
