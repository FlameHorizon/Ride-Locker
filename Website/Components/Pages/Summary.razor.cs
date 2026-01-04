using Microsoft.EntityFrameworkCore;
using Website.Data;
using Website.Models;

namespace Website.Components.Pages
{
    public partial class Summary
    {
        // REFAC: Maybe custom list would be better than built in?
        // See: Issue #2.
        public List<Ride> Rides = [];
        private List<TrackPoint> _tracks = [];

        // REFAC: Also use more concrete types instead of interfaces?
        public IEnumerable<ChartData<string, double>> DistanceOverMonthsData = [];
        public IEnumerable<ChartData<string, double>> SpeedOverMonthsData = [];
        public IEnumerable<ChartData<double>> SpeedDistributionData = [];


        /// <summary>
        /// Mostly used as an entry point for testing.
        /// </summary>
        public Summary(List<Ride> rides)
        {
            Rides = rides;
            foreach (var r in rides)
            {
                foreach (var tr in r.TrackPoints)
                {
                    _tracks.Add(tr);
                }
            }
            //_tracks = rides.SelectMany(x => x.TrackPoints).ToList();

            DistanceOverMonthsData = CreateDistanceOverMonthsData();
            SpeedOverMonthsData = CreateSpeedOverMonthData();
            SpeedDistributionData = CreateSpeedDistributionData();
        }

        protected override async Task OnInitializedAsync()
        {
            await using AppDbContext db = await DbContextFactory
              .CreateDbContextAsync();

            // List is used here because we are going to iterate 
            // over it multiple times.
            Rides = db.Rides
              .AsNoTracking()
              .Include(x => x.TrackPoints)
              .ToList();

            _tracks = Rides.SelectMany(x => x.TrackPoints).ToList();
            CreateSpeedHistogramData().ToList();

            // PERF: Each of these calls, iterates over all track points
            // in each ride. Instead of doing that, I can iterate once over
            // track points and compute results for all data sets
            // within single a loop.
            // foreach (var tp in Rides.TrackPoints) {
            //     ... some calculations
            // }
            // ... or we can cache results and update them once needed.
            // Caching is harder to maintain so for now, I'm leaving it as it is.
            DistanceOverMonthsData = CreateDistanceOverMonthsData();
            SpeedOverMonthsData = CreateSpeedOverMonthData();
            SpeedDistributionData = CreateSpeedDistributionData();
        }

        // Unfortunately, there isn't an overload of Sum that accepts an
        // IEnumerable<TimeSpan>. Additionally, there's no currently way of
        // specifying operator-based generic constraints for type-parameters,
        // so even though TimeSpan is "natively" summable, that fact can't be
        // picked up easily by generic code.
        private static TimeSpan SumDurations(List<Ride> rides)
        {
            return rides.Select(x => x.Duration)
              .Aggregate(TimeSpan.Zero, (t1, t2) => t1 + t2);
        }

        // Since histogram can't be created from rides themselves,
        // I need to again look into all GPX files to get all required
        // details.
        private IEnumerable<ChartData<double, double>> CreateSpeedHistogramData()
        {
            return Charts.CreateSpeedHistogramData(_tracks);
        }

        private IEnumerable<ChartData<string, double>> CreateDistanceOverMonthsData()
        {
            var s = new List<(string, List<TrackPoint>)>(_tracks.Count);
            if (_tracks.Count == 0)
            {
                return [];
            }

            foreach (var trackPoint in _tracks)
            {
                bool found = false;
                int index = -1;
                string key = trackPoint.Time.Year.ToString("00") +
                  "-" + trackPoint.Time.Month.ToString("00");
                // Search of XValue
                for (int i = 0; i < s.Count; i++)
                {

                    if (s[i].Item1 == key)
                    {
                        found = true;
                        index = i;
                        break;
                    }

                    found = false;
                    index = -1;

                }
                if (found)
                {
                    s[index].Item2.Add(trackPoint);
                }
                else
                {
                    s.Add((key, new List<TrackPoint>() { trackPoint }));
                }
            }

            var result = new List<ChartData<string, double>>(s.Count);
            foreach (var val in s)
            {
                result.Add(new ChartData<string, double>() { XValue = val.Item1, YValue = Geo.HaversineDistance(val.Item2) });
            }
            return result;

            //return s.Select(x => new ChartData<string, double>() { XValue = x.Item1, YValue = Geo.HaversineDistance(x.Item2) });


            // return res;
            //
            // // return Charts.CreateDistanceOverMonthsData(_tracks);
            // // Dictionary of month numbers and distance covered each month.
            // Dictionary<string, double> data = _tracks
            //     .GroupBy(x => x.Time.ToString("yy-MM"))
            //     .ToDictionary(x => x.Key, x => Geo.HaversineDistance(x.ToList()));
            //
            // return data.Select(kvp => new ChartData<string, double>()
            // {
            //     XValue = kvp.Key,
            //     YValue = kvp.Value
            // });

        }

        private IEnumerable<ChartData<string, double>> CreateSpeedOverMonthData()
        {
            var s = new List<(string, List<double>)>(_tracks.Count);
            if (_tracks.Count == 0)
            {
                return [];
            }

            foreach (var trackPoint in _tracks)
            {
                bool found = false;
                int index = -1;
                string key = trackPoint.Time.Year.ToString("00") +
                  "-" + trackPoint.Time.Month.ToString("00");
                // Search of XValue
                for (int i = 0; i < s.Count; i++)
                {
                    if (s[i].Item1 == key)
                    {
                        found = true;
                        index = i;
                        break;
                    }

                    found = false;
                    index = -1;

                }

                if (found)
                {
                    s[index].Item2.Add(trackPoint.Speed * 3.6);
                }
                else
                {
                    s.Add((key, new List<double>() { trackPoint.Speed * 3.6 }));
                }
            }

            var result = new List<ChartData<string, double>>(s.Count);
            foreach (var val in s)
            {
                result.Add(new ChartData<string, double>() { XValue = val.Item1, YValue = val.Item2.Average() });
            }
            return result;
            //return s.Select(x => new ChartData<string, double>() { XValue = x.Item1, YValue = x.Item2.Average() });

            //            return Charts.CreateSpeedOverMonthData(_tracks);
        }

        private IEnumerable<ChartData<double>> CreateSpeedDistributionData()
        {
            return Charts.CreateSpeedDistributionData(_tracks);
        }

        private string GetAverageTripDuration()
        {
            if (Rides.Count == 0)
            {
                return "0 minutes";
            }

            return Math.Round(Rides.Average(x => x.Duration.TotalMinutes), 2) + " minutes";
        }

        private string GetAverageSpeed()
        {
            if (Rides.Count == 0)
            {
                return "0 km/h";
            }

            return Math.Round(Rides.Average(x => x.AvgSpeed) * 3.6, 2) + " km/h";
        }

        private string GetMaxSpeed()
        {
            if (Rides.Count == 0)
            {
                return "0 km/h";
            }

            return Math.Round(Rides.Max(x => x.MaxSpeed) * 3.6, 2) + " km/h";
        }

    }
}
