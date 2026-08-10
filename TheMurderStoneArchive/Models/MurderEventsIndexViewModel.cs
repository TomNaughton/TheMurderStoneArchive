namespace TheMurderStoneArchive.Models
{
    public class MurderEventsIndexViewModel
    {
        public IEnumerable<MurderEvent> Events { get; set; } = new List<MurderEvent>();

        public string? SearchTerm { get; set; }

        public string SortOrder { get; set; } = "title"; // Default sort order

        public int CurrentPage { get; set; } = 1;

        public int PageSize { get; set; } = 10;

        public int TotalEvents { get; set; }

        public int TotalPages 
        { 
            get => TotalEvents == 0 ? 1 : (int)Math.Ceiling((double)TotalEvents / PageSize); 
        }

        public bool HasPreviousPage => CurrentPage > 1;

        public bool HasNextPage => CurrentPage < TotalPages;
    }
}
