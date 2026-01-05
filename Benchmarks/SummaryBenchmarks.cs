using System.Xml.Serialization;
using BenchmarkDotNet.Attributes;
using Website.Models;

public class SummaryBenchmarks
{
    private readonly List<Ride> _rides = [];

    public SummaryBenchmarks()
    {
        var serializer = new XmlSerializer(typeof(Gpx));
        foreach (string path in Directory.GetFiles("./test_data/SummaryBenchmarks/"))
        {
            FileStream stream = File.OpenRead(path);
            Gpx? gpx = serializer.Deserialize(stream) as Gpx;
            Ride ride = Website.Components.Pages.Upload.ConvertToRide(gpx!);
            _rides.Add(ride);
        }
    }

    [Benchmark]
    public Website.Components.Pages.Summary Constructor()
    {
        return new Website.Components.Pages.Summary(_rides);
    }
}
