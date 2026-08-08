namespace TheMurderStoneArchive.Models
{
    public class MapPinViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string ShortDescription { get; set; } = string.Empty;
    }
}