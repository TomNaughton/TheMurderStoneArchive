using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NetTopologySuite.Geometries;

namespace TheMurderStoneArchive.Models
{
    public class Location
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        // PostGIS geometry column — SRID 4326 (WGS 84 / GPS coordinates).
        // X = Longitude, Y = Latitude (NTS convention).
        // BindNever  — prevents the model binder from recursing into the NTS Point
        //              object graph (which causes an InsufficientExecutionStackException).
        // JsonIgnore — the JSON serializer uses the Latitude/Longitude wrappers below
        //              instead; serializing a raw NTS Point causes the same deep recursion.
        [BindNever]
        [JsonIgnore]
        public Point? Coordinates { get; set; }

        // Convenience wrappers so all existing code that reads or writes
        // Location.Latitude / Location.Longitude continues to work unchanged.
        // Nullable backing fields ensure that setting one property in an object
        // initialiser or model-binder call doesn't overwrite the other with 0.
        private double? _latitude;
        private double? _longitude;

        [NotMapped]
        public double Latitude
        {
            get => Coordinates?.Y ?? _latitude ?? 0;
            set
            {
                _latitude = value;
                Coordinates = new Point(_longitude ?? Coordinates?.X ?? 0, value) { SRID = 4326 };
            }
        }

        [NotMapped]
        public double Longitude
        {
            get => Coordinates?.X ?? _longitude ?? 0;
            set
            {
                _longitude = value;
                Coordinates = new Point(value, _latitude ?? Coordinates?.Y ?? 0) { SRID = 4326 };
            }
        }

        public ICollection<MurderEvent> MurderEvents { get; set; } = new List<MurderEvent>();
    }
}
