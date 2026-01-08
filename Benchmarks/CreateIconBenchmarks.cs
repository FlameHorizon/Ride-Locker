using System.Xml.Serialization;
using BenchmarkDotNet.Attributes;
using Website.Models;
using Website.Components.Pieces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

[ShortRunJob]
[MemoryDiagnoser] // Tracks RAM allocations
public class CreateIconBenchmarks
{
    private readonly List<Ride> _rides = [];

    [GlobalSetup]
    public void Setup()
    {
        var serializer = new XmlSerializer(typeof(Gpx));
        foreach (string path in Directory.GetFiles("./test_data/CreateIconBenchmarks/"))
        {
            using FileStream stream = File.OpenRead(path);
            Gpx? gpx = serializer.Deserialize(stream) as Gpx;
            Ride ride = Website.Components.Pages.UploadModal.ConvertToRide(gpx!);
            _rides.Add(ride);
        }
    }

    [Benchmark]
    public int CreateImage_Candidate()
    {
        using Image<Rgba32> image = RideTableRow.CreateImage(_rides[0]);
        return image.Width;
    }
}
