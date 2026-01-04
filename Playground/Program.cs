using Website.Components.Pages;
using Website.Models;

for (int i = 0; i < 20_000; i++)
{
var rides = new List<Ride> {
    new Ride {
        TrackPoints = new List<TrackPoint> {
            new TrackPoint {
                Time = DateTime.MinValue,
                Latitude = Convert.ToDouble(i),
                Longitude = 0.0
            },
            new TrackPoint {
                Time = DateTime.MinValue,
                Latitude = Convert.ToDouble(i),
                Longitude = 0.0
            },
            new TrackPoint {
                Time = DateTime.MinValue.AddMonths(1),
                Latitude = 1.0,
                Longitude = Convert.ToDouble(i)
            },
            new TrackPoint {
                Time = DateTime.MinValue.AddMonths(1),
                Latitude = Convert.ToDouble(i),
                Longitude = 0.0
            }

        }
    }
};

var s = new Summary(rides);
Console.WriteLine(s.DistanceOverMonthsData.ToList());
Console.WriteLine(s.SpeedOverMonthsData.ToList());
Console.WriteLine(s.SpeedDistributionData.ToList());
}
