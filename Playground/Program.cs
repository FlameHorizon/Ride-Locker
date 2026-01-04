using Website.Components.Pages;
using Website.Models;
var rnd = Random.Shared;

for (int i = 0; i < 100_00; i++)
{
  var rides = new List<Ride> {
    new Ride {
        TrackPoints = new List<TrackPoint> {
            new TrackPoint {
                Time = DateTime.MinValue,
                Latitude = rnd.NextDouble(),
                Longitude = i
            },
            new TrackPoint {
                Time = DateTime.MinValue,
                Latitude = rnd.NextDouble(),
                Longitude = i
            },
            new TrackPoint {
                Time = DateTime.MinValue.AddMonths(1),
                Latitude = i,
                Longitude = rnd.NextDouble(),
            },
            new TrackPoint {
                Time = DateTime.MinValue.AddMonths(1),
                Latitude = rnd.NextDouble(),
                Longitude = i
            }
        }
    }
  };
  var s = new Summary(rides);
  Console.WriteLine(s.DistanceOverMonthsData.OrderBy(x => x.XValue).First().XValue);
  Console.WriteLine(s.SpeedOverMonthsData.OrderBy(x => x.XValue).First().XValue);
  Console.WriteLine(s.SpeedDistributionData.OrderBy(x => x.XValue).First().XValue);

}
