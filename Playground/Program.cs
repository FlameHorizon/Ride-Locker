using System.Xml.Serialization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Website.Components.Pieces;
using Website.Models;

List<Ride> _rides = [];
var serializer = new XmlSerializer(typeof(Gpx));
foreach (string path in Directory.GetFiles("./test_data/CreateIconBenchmarks/"))
{
    using FileStream stream = File.OpenRead(path);
    Gpx? gpx = serializer.Deserialize(stream) as Gpx;
    Ride ride = Website.Components.Pages.UploadModal.ConvertToRide(gpx!);
    _rides.Add(ride);
}

using Image<Rgba32> image = RideTableRow.CreateImage(_rides[0]);
Console.WriteLine(image.Width);
