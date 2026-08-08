using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TheMurderStoneArchive.Models;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        // Mock data - Eventually, this will be queried via Entity Framework
        var stones = new List<MapPinViewModel>
        {
            new MapPinViewModel
            {
                Id = 1,
                Title = "The Unknown Sailor",
                Latitude = 51.1118, // Coordinates for the Devil's Punch Bowl
                Longitude = -0.7311,
                ShortDescription = "Erected in 1786 to mark the brutal murder of an unknown sailor by three men he befriended."
            }
        };

        // Serialize the data to JSON so our JavaScript can consume it
        ViewBag.MapData = JsonSerializer.Serialize(stones);

        return View();
    }
}