using Website.Models;

using System.Diagnostics;

using ScottPlot;
using ScottPlot.AxisPanels;
using ScottPlot.Plottables;
using ScottPlot.Statistics;
using ScottPlot.TickGenerators;

namespace Website;

public static class Charts
{
    public static IEnumerable<ChartData<double, double>> CreateSpeedHistogramData(IEnumerable<TrackPoint> track)
    {
        IEnumerable<double> ys = track.Select(x => Math.Round(x.Speed * 3.6));
        Histogram hist = Histogram.WithBinSize(5, ys);
        var res = hist.Bins.Zip(hist.Counts, (x, y) => new ChartData<double, double>()
        {
            XValue = x,
            YValue = y
        });

        return res;
    }

    [Obsolete]
    public static async Task<string> CreateSpeedHistogramAsync(List<TrackPoint> track)
    {
        if (track.Count == 0)
        {
            Console.WriteLine("ERR: Track is empty.");
            return await Task.FromResult("");
        }

        Plot plt = new();

        plt.YLabel("Time");
        plt.XLabel("Speed (km/h)");

        IEnumerable<double> ys = track.Select(x => Math.Round(x.Speed * 3.6));
        Histogram hist = Histogram.WithBinSize(5, ys);
        BarPlot barPlot = plt.Add.Bars(hist.Bins, hist.Counts);

        foreach (Bar bar in barPlot.Bars)
        {
            bar.Size = hist.FirstBinSize;
            bar.LineWidth = 0;
            bar.FillStyle.AntiAlias = false;
        }

        plt.Axes.Margins(bottom: 0);

        return await Task.FromResult(plt.GetSvgHtml(400, 400));
    }

    [Obsolete]
    public static async Task<string> CreateTrackMapAsync(List<TrackPoint> track)
    {
        if (track.Count == 0)
        {
            Console.WriteLine("ERR: Track is empty.");
            return await Task.FromResult("");
        }

        // Once we have data, we can create map.
        Plot plt = new();
        double[] xs = track.Select(x => x.Longitude).ToArray();
        double[] ys = track.Select(x => x.Latitude).ToArray();

        plt.Add.ScatterLine(xs, ys);

        // Invert axis - since 0/0 is NORTH POLE.
        //plt.Axes.InvertY();
        plt.HideGrid();

        return await Task.FromResult(plt.GetSvgHtml(400, 400));
    }

    public static IEnumerable<ChartData<DateTime, double>> CreateSpeedRunningAverageData(List<TrackPoint> track)
    {
        double[] ys = track.Select(x => x.Speed * 3.6).ToArray();
        double[] xs = track.Select(x => x.Time.ToOADate()).ToArray();

        // Calculate running avg. of speed.
        double[] runningAvg = new double[ys.Length];
        double sum = 0;

        for (int i = 0; i < ys.Length; i++)
        {
            sum += ys[i];
            runningAvg[i] = sum / (i + 1);
        }

        var res = xs.Zip(runningAvg, (x, y) => new ChartData<DateTime, double>()
        {
            XValue = DateTime.FromOADate(x),
            YValue = y
        });

        return res;
    }

    [Obsolete]
    public static async Task<string> CreateSpeedRunningAverageAsync(List<TrackPoint> track)
    {
        Plot plt = new();
        plt.YLabel("Speed (km/h)");

        // Set up the bottom axis to use DateTime ticks
        DateTimeXAxis axis = plt.Axes.DateTimeTicksBottom();

        // NOTE: do not move XLabel specification about plt.Axes.DateTimeTicksBottom
        // because it will not be drawn.
        plt.XLabel("Time");

        // Apply our custom tick formatter
        DateTimeAutomatic tickGen = (DateTimeAutomatic)axis.TickGenerator;
        tickGen.LabelFormatter = CustomFormatter;

        double[] ys = track.Select(x => x.Speed * 3.6).ToArray();
        double[] xs = track.Select(x => x.Time.ToOADate()).ToArray();

        // Calculate running avg. of speed.
        double[] runningAvg = new double[ys.Length];
        double sum = 0;

        for (int i = 0; i < ys.Length; i++)
        {
            sum += ys[i];
            runningAvg[i] = sum / (i + 1);
        }

        plt.Add.ScatterLine(xs, runningAvg);
        plt.Axes.SetLimitsY(0, 130);

        return await Task.FromResult(plt.GetSvgHtml(400, 400));
    }

    public static IEnumerable<ChartData<DateTime, double>> CreateSpeedOverTimeData(List<TrackPoint> track)
    {
        var res = track.Select(x => new ChartData<DateTime, double>()
        {
            XValue = x.Time,
            YValue = x.Speed * 3.6
        });
        return res;
    }

    [Obsolete]
    public static string CreateSpeedOverTimeChart(List<TrackPoint> track)
    {
        Plot plt = new();
        plt.YLabel("Speed (km/h)");

        double[] ys = track.Select(x => x.Speed * 3.6).ToArray();
        double[] xs = track.Select(x => x.Time.ToOADate()).ToArray();

        plt.Add.ScatterLine(xs, ys);
        // Set up the bottom axis to use DateTime ticks
        DateTimeXAxis axis = plt.Axes.DateTimeTicksBottom();

        // NOTE: do not move XLabel specification about plt.Axes.DateTimeTicksBottom
        // because it will not be drawn.
        plt.XLabel("Time");

        // Apply our custom tick formatter
        DateTimeAutomatic tickGen = (DateTimeAutomatic)axis.TickGenerator;
        tickGen.LabelFormatter = CustomFormatter;

        // Add speed limits markers
        LinePlot speedLimitOutsideCity = plt.Add.Line(xs.First(), 90, xs.Last(), 90);
        speedLimitOutsideCity.LineWidth = 2;
        speedLimitOutsideCity.MarkerSize = 10;

        LinePlot speedLimitInCity = plt.Add.Line(xs.First(), 50, xs.Last(), 50);
        speedLimitInCity.LineWidth = 2;
        speedLimitInCity.MarkerSize = 10;

        // I'm almost never drive over 130 km/h. This also makes comparing charts easier.
        plt.Axes.SetLimitsY(0, 130);
        return plt.GetSvgHtml(800, 400);
    }

    private static string CustomFormatter(DateTime dt)
    {
        bool isMidnight = dt is { Hour: 0, Minute: 0, Second: 0 };
        return isMidnight
                ? DateOnly.FromDateTime(dt).ToString("HH:mm")
                : TimeOnly.FromDateTime(dt).ToString("HH:mm");
    }

    public static IEnumerable<ChartData<DateTime, double>> CreateDriveDynamicsAccelerationsData(List<TrackPoint> track, double threshold = 0.5)
    {
        double[] xs = track.Select(x => x.Time.ToOADate()).ToArray();
        double[] ys = track.Select(x => x.Speed * 3.6).ToArray();

        double[] accelerations = DriverDynamics.AccelerationsRates(xs, ys)
                .Select(x => x >= threshold ? x : 0)
                .ToArray();

        var res = xs.Zip(accelerations, (x, y) => new ChartData<DateTime, double>()
        {
            XValue = DateTime.FromOADate(x),
            YValue = y
        });

        return res;
    }

    public static IEnumerable<ChartData<DateTime, double>> CreateDriveDynamicsDecelerationsData(List<TrackPoint> track, double threshold = -0.5)
    {
        double[] xs = track.Select(x => x.Time.ToOADate()).ToArray();
        double[] ys = track.Select(x => x.Speed * 3.6).ToArray();

        double[] decelerations = DriverDynamics.DecelerationsRates(xs, ys)
                .Select(x => x <= threshold ? x : 0)
                .ToArray();

        var res = xs.Zip(decelerations, (x, y) => new ChartData<DateTime, double>()
        {
            XValue = DateTime.FromOADate(x),
            YValue = y
        });

        return res;
    }

    public static IEnumerable<ChartData<DateTime, double>> CreateDriveDynamicsData(List<TrackPoint> track)
    {
        double[] xs = track.Select(x => x.Time.ToOADate()).ToArray();
        double[] ys = track.Select(x => x.Speed * 3.6).ToArray();

        double[] accelerations = DriverDynamics.AccelerationsRates(xs, ys);
        double[] decelerations = DriverDynamics.DecelerationsRates(xs, ys);

        var res = xs.Zip(accelerations, (x, y) => new ChartData<DateTime, double>()
        {
            XValue = DateTime.FromOADate(x),
            YValue = y
        });

        return res;
    }

    public static async Task<string> CreateDriveDynamicsAsync(List<TrackPoint> track)
    {
        Plot plt = new();
        plt.YLabel("Speed change (m/s2)");

        // Set up the bottom axis to use DateTime ticks
        DateTimeXAxis axis = plt.Axes.DateTimeTicksBottom();

        // NOTE: do not move XLabel specification about plt.Axes.DateTimeTicksBottom
        // because it will not be drawn.
        plt.XLabel("Time");

        // Apply our custom tick formatter
        DateTimeAutomatic tickGen = (DateTimeAutomatic)axis.TickGenerator;
        tickGen.LabelFormatter = CustomFormatter;

        double[] xs = track.Select(x => x.Time.ToOADate()).ToArray();
        double[] ys = track.Select(x => x.Speed * 3.6).ToArray();

        double[] zeroLine = new double[xs.Length];
        double[] decelerations = DriverDynamics.DecelerationsRates(xs, ys);
        double[] accelerations = DriverDynamics.AccelerationsRates(xs, ys);

        FillY decelerationFill = plt.Add.FillY(xs, zeroLine, decelerations);
        decelerationFill.FillColor = Colors.Red;
        decelerationFill.LineWidth = 0;

        FillY accelerationFill = plt.Add.FillY(xs, zeroLine, accelerations);
        accelerationFill.FillColor = Colors.Green;
        accelerationFill.LineWidth = 0;

        return await Task.FromResult(plt.GetSvgHtml(800, 400));
    }

    public static async Task<string> CreateOverallDistanceAsync(List<TrackPoint> track)
    {
        Plot plt = new();
        plt.YLabel("Distance (km)");
        plt.XLabel("Month");

        // FIX: This grouping will stop to work, when we start working
        // on two different years which include the same month number.

        // Dictionary of month numbers and distance covered each month.
        Dictionary<int, double> data = track
                .GroupBy(x => x.Time.Month)
                .ToDictionary(x => x.Key, x => Geo.HaversineDistance(x.ToList()));

        // Use custom tick labels on the bottom
        NumericManual tickGen = new();

        // Get starting position of key major tick.
        int position = data.Min(x => x.Key);
        foreach (var kvp in data)
        {
            tickGen.AddMajor(position, $"{kvp.Key}");
            position++;
        }
        plt.Axes.Bottom.TickGenerator = tickGen;

        foreach (var kvp in data)
        {
            BarPlot b = plt.Add.Bars([kvp.Key], [kvp.Value]);
            Debug.Assert(b.Bars.Count == 1, $"Expected to get only one bar, got {b.Bars.Count}");

            // Since I always add one bar at the time, there should be always
            // one bar to color.
            b.Bars.Single().FillColor = Colors.C0;
        }

        return await Task.FromResult(plt.GetSvgHtml(400, 400));
    }

    public static IEnumerable<ChartData<string, double>> CreateDistanceOverMonthsData(List<TrackPoint> track)
    {
        // Dictionary of month numbers and distance covered each month.
        Dictionary<string, double> data = track
            .GroupBy(x => x.Time.ToString("yy-MM"))
            .ToDictionary(x => x.Key, x => Geo.HaversineDistance(x.ToList()));

        return data.Select(kvp => new ChartData<string, double>()
        {
            XValue = kvp.Key,
            YValue = kvp.Value
        });
    }

    public static IEnumerable<ChartData<DateTime, double>> CreateElevationChangesData(List<TrackPoint> track)
    {
        double[] ys = track.Select(x => x.Elevation).ToArray();
        DateTime[] xs = track.Select(x => x.Time).ToArray();

        var res = xs.Zip(ys, (x, y) => new ChartData<DateTime, double>()
        {
            XValue = x,
            YValue = y
        });

        return res;
    }

    public static IEnumerable<ChartData<string, double>> CreateSpeedOverMonthData(List<TrackPoint> track)
    {
        // Dictionary of month numbers and distance covered each month.
        Dictionary<string, double> data = track
          .GroupBy(x => x.Time.ToString("yy-MM"))
          .ToDictionary(x => x.Key, x => x.Average(y => y.Speed * 3.6));

        return data.Select(kvp => new ChartData<string, double>()
        {
            XValue = kvp.Key,
            YValue = kvp.Value
        });
    }

    public static IEnumerable<ChartData<double>> CreateSpeedDistributionData(IEnumerable<TrackPoint> track)
    {
        return track.Select(x => new ChartData<double>() { XValue = x.Speed * 3.6 });
    }
}

public class ChartData<TX>
{
    public TX XValue { get; set; }
}

public class ChartData<TX, TY>
{
    public TX XValue { get; set; }
    public TY YValue { get; set; }
}
