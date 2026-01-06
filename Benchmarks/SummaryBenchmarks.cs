using System.Xml.Serialization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Website.Components.Pages;
using Website.Models;

[MemoryDiagnoser]
public class SummaryBenchmarks
{
    private readonly List<Ride> _rides = [];
    private readonly Ride[] _ridesData;
    private readonly Consumer _consumer;

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

        _ridesData = _rides.ToArray();
        _consumer = new BenchmarkDotNet.Engines.Consumer();
    }

    // [Benchmark]
    public Website.Components.Pages.Summary Constructor()
    {
        return new Website.Components.Pages.Summary(_rides);
    }

    [Benchmark]
    public void CreateSpeedOverMonthBaseline()
    {
        _consumer.Consume(Summary.CreateDistanceOverMonthsData(_ridesData));
    }
}
