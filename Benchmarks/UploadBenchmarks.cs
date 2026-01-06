using System.Xml.Serialization;
using BenchmarkDotNet.Attributes;
using Website.Components.Pages;
using Website.Models;

[EventPipeProfiler(BenchmarkDotNet.Diagnosers.EventPipeProfile.CpuSampling)]
[MemoryDiagnoser]
public class UploadBenchmarks
{
    private readonly Gpx _gpx;

    public UploadBenchmarks()
    {
        XmlSerializer serializer = new XmlSerializer(typeof(Gpx));
        string path = "./test_data/upload_benchmarks/2025-11-09_15-18_niedz.-3.gpx";
        FileStream stream = File.OpenRead(path);

        Gpx? result = serializer.Deserialize(stream)! as Gpx;
        ArgumentNullException.ThrowIfNull(result);
        _gpx = result;
    }

    [Benchmark(Baseline = true)]
    public Ride ConvertSingleRide()
    {
        return Upload.ConvertToRide(_gpx);
    }
}
