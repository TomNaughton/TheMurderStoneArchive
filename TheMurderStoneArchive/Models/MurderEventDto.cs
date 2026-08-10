namespace TheMurderStoneArchive.Models
{
    public class MurderEventDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Description { get; set; } = string.Empty;
        public LocationDto? Location { get; set; }
    }

    public class LocationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class MurderEventsIndexApiViewModel
    {
        public IEnumerable<MurderEventDto> Events { get; set; } = new List<MurderEventDto>();
        public string? SearchTerm { get; set; }
        public string SortOrder { get; set; } = "title";
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
