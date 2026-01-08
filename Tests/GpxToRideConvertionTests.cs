using System.Xml.Serialization;
using Website.Components.Pages;
using Website.Models;

namespace Tests;

public class GpxToRideConvertionTests
{
    [Fact]
    public void ConvertGpxToRide_IsOk_WhenGpxIsValid()
    {
        var serializer = new XmlSerializer(typeof(Gpx));
        var stream = File.OpenRead("./test_data/GpxToRideConvertion/2025-12-28_15-53_niedz..gpx");
        Gpx? gpx = serializer.Deserialize(stream) as Gpx;
        Ride ride = UploadModal.ConvertToRide(gpx!);

        Assert.Equal(2, ride.FastAccelerationCount);
        Assert.Equal(2, ride.FastAccelerationCount);
    }
}
