using Website.Models;
using Website.Data;
using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Website.Components.Pages;

public partial class Summary
{
    // REFAC: Maybe custom list would be better than built in?
    // See: Issue #2.
    public Ride[] Rides = [];

    // REFAC: Also use more concrete types instead of interfaces?
    public IEnumerable<ChartData<string, double>> DistanceOverMonthsData = [];
    public IEnumerable<ChartData<string, double>> SpeedOverMonthsData = [];
    public IEnumerable<ChartData<double>> SpeedDistributionData = [];
    private readonly ILogger<Summary> _logger;
    private readonly IMemoryCache? _cache;

    public Summary()
    {
        _logger = NullLogger<Summary>.Instance;
    }

    // NOTE: This empty constructor is needed for the DI.
    // Without it, Dependency resolver would pick second constrcutor (Summary(IEnumerable<Ride>))
    // and fail with message:
    // "Unable to resolve service for type 'System.Collections.Generic.List`1..."
    public Summary(ILogger<Summary> logger, IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// Mostly used as an entry point for testing.
    /// </summary>
    public Summary(IEnumerable<Ride> rides)
    {
        // For testing purposes, I'm not going to log anything anywhere.
        // NullLogger is just for that.
        _logger = NullLogger<Summary>.Instance;

        if (rides.Any() == false) return;

        Rides = rides.ToArray();
        DistanceOverMonthsData = CreateDistanceOverMonthsData(Rides);
        SpeedOverMonthsData = CreateSpeedOverMonthData(Rides);
        SpeedDistributionData = CreateSpeedDistributionData(Rides);
    }

    protected override async Task OnInitializedAsync()
    {
        var sw = Stopwatch.StartNew();

        // NOTE: Cache mechanism is provided, when application starts with DI
        // container. For testing, I'm not using cache.
        if (_cache is not null)
        {
            Ride[]? result = await _cache.GetOrCreateAsync(
                "tracks",
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                    await using AppDbContext db = await DbContextFactory
                        .CreateDbContextAsync();

                    return db.Rides
                        .AsNoTracking()
                        .Include(x => x.TrackPoints)
                        .ToArray();
                }
            );

            if (result is null)
            {
                _logger.LogWarning("Expected to get set of rides from either database or cache, but got null");
                return;
            }

            Rides = result;
        }

        Console.WriteLine($"Took {sw.ElapsedMilliseconds} ms to query database.");
        sw.Restart();

        DistanceOverMonthsData = CreateDistanceOverMonthsData(Rides);
        SpeedOverMonthsData = CreateSpeedOverMonthData(Rides);
        SpeedDistributionData = CreateSpeedDistributionData(Rides);
        Console.WriteLine($"Took {sw.ElapsedMilliseconds} ms to create data for charts");
    }

    // Unfortunately, there isn't an overload of Sum that accepts an
    // IEnumerable<TimeSpan>. Additionally, there's no currently way of
    // specifying operator-based generic constraints for type-parameters,
    // so even though TimeSpan is "natively" summable, that fact can't be
    // picked up easily by generic code.
    private static TimeSpan SumDurations(Ride[] rides)
    {
        TimeSpan result = TimeSpan.Zero;
        foreach (Ride ride in rides)
        {
            result += ride.Duration;
        }
        return result;
    }

    public static List<ChartData<string, double>> CreateDistanceOverMonthsData(
      Ride[] rides)
    {
        if (rides.Length == 0) return [];

        var result = new List<ChartData<string, double>>(24);
        IFormatProvider formatProvider = CultureInfo.CurrentCulture;
        foreach (Ride ride in rides)
        {
            if (ride.TrackPoints.Count == 0) continue;

            int index = -1;

            // Create a numeric key (YYYYMM). NO STRINGS YET.
            int year = ride.Start.Year;
            int month = ride.Start.Month;
            int numericKey = (year * 100) + month;

            // Serach for the label in data set.
            for (int i = 0; i < result.Count; i++)
            {
                if (result[i].Tag == numericKey)
                {
                    index = i;
                    break;
                }
            }

            // If found, update distance value. Otherwise add new label.
            if (index != -1) result[index].YValue += ride.Distance;
            else
            {
                result.Add(new ChartData<string, double>()
                {
                    XValue = (year < 10 ? "0" : "") + year.ToString() + "-" + (month < 10 ? "0" : "") + month.ToString(),
                    YValue = ride.Distance,
                    Tag = numericKey
                });
            }
        }

        // Sort the end result by labels (XValue).
        result.Sort((a, b) => a.Tag.CompareTo(b.Tag));
        return result;
    }

    private static IEnumerable<ChartData<string, double>> CreateSpeedOverMonthData(
        Ride[] rides)
    {
        if (rides.Length == 0) return [];

        var temp = new List<(string, FastNumericAverageDouble, int)>();
        IFormatProvider formatProvider = CultureInfo.CurrentCulture;
        foreach (Ride ride in rides)
        {
            if (ride.TrackPoints.Count == 0) continue;

            int index = -1;

            // Create a numeric key (YYYYMM). NO STRINGS YET.
            int year = ride.Start.Year;
            int month = ride.Start.Month;
            int numericKey = (year * 100) + month;

            // Serach for the label in data set.
            for (int i = 0; i < temp.Count; i++)
            {
                if (temp[i].Item3 == numericKey)
                {
                    index = i;
                    break;
                }
            }


            // If found, update distance value. Otherwise add new label.
            if (index != -1) temp[index].Item2.Add(ride.AvgSpeed);
            else
            {
                var fna = new FastNumericAverageDouble();
                fna.Add(ride.AvgSpeed);
                temp.Add(((year < 10 ? "0" : "") + year.ToString() + "-"
                        + (month < 10 ? "0" : "") + month.ToString(), fna, numericKey));
            }
        }

        var result = new List<ChartData<string, double>>();
        foreach (var item in temp)
        {
            result.Add(new ChartData<string, double>()
            {
                XValue = item.Item1,
                YValue = item.Item2.Average * 3.6,
                Tag = item.Item3
            });
        }

        // Sort the end result by labels (XValue).
        result.Sort((a, b) => a.Tag.CompareTo(b.Tag));
        return result;
    }

    private static IEnumerable<ChartData<double>> CreateSpeedDistributionData(Ride[] rides)
    {
        List<ChartData<double>> res = [];
        foreach (var ride in rides)
        {
            foreach (var pt in ride.TrackPoints)
            {
                res.Add(new ChartData<double>() { XValue = pt.Speed * 3.6 });
            }
        }
        return res;
    }

    private string GetAverageTripDuration()
    {
        if (Rides.Length == 0)
        {
            return "0 minutes";
        }

        return Math.Round(Rides.Average(x => x.Duration.TotalMinutes), 2) + " minutes";
    }

    private string GetAverageSpeed()
    {
        if (Rides.Length == 0)
        {
            return "0 km/h";
        }

        return Math.Round(Rides.Average(x => x.AvgSpeed) * 3.6, 2) + " km/h";
    }

    private string GetMaxSpeed()
    {
        if (Rides.Length == 0)
        {
            return "0 km/h";
        }

        return Math.Round(Rides.Max(x => x.MaxSpeed) * 3.6, 2) + " km/h";
    }
}
