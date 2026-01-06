using Website.Data;
using Website.Models;
using System.Xml.Serialization;
using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components.Forms;

namespace Website.Components.Pages;

public partial class Upload
{
    // NOTE: Upload page has control over when cache should be invalidated.
    // This is required since we want to allow user to see updated list of
    // rides immediately after new rides are added.

    private readonly ILogger<Upload> _logger;
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private bool _uploadDone = false;

    public Upload(
        ILogger<Upload> logger,
        IMemoryCache cache,
        IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _logger = logger;
        _cache = cache;
        _dbContextFactory = dbContextFactory;
    }

    private async Task LoadFiles(InputFileChangeEventArgs e)
    {
        const int MAX_FILE_COUNT = 10;
        int count = e.FileCount;
        if (count > MAX_FILE_COUNT)
        {
            // TODO: Implement information for user that the limit is 10.
            _logger.LogWarning("User attempted to upload {0} files, but count limit is {1}.",
                count,
                MAX_FILE_COUNT);
            return;
        }

        const int MAX_FILE_SIZE_BYTES = 1024 * 1024 * 1; // 1MB
        foreach (IBrowserFile file in e.GetMultipleFiles(count))
        {
            if (file.Size > MAX_FILE_SIZE_BYTES)
            {
                _logger.LogWarning("User attempted to upload file '{0}' of size {1}, but size limit is {2}. Aborting.",
                    file.Name,
                    file.Size,
                    MAX_FILE_SIZE_BYTES);
                return;
            }

            string ext = Path.GetExtension(file.Name).ToLower();
            if (ext != ".gpx")
            {
                _logger.LogWarning("User attempted to upload file '{0}' with extension '{1}', but expected extension is '.gpx'. Aborting.",
                    file.Name,
                    ext);
                return;
            }
        }

        // PERF: Counters
        var sw = new Stopwatch();
        sw.Start();

        _logger.LogDebug("Loading {0} files.", count);
        List<Ride> rides = [];

        foreach (IBrowserFile file in e.GetMultipleFiles(count))
        {
            var serializer = new XmlSerializer(typeof(Gpx));
            var ms = new MemoryStream();
            await file.OpenReadStream(MAX_FILE_SIZE_BYTES).CopyToAsync(ms);

            ms.Position = 0;
            Gpx? gpx = serializer.Deserialize(ms) as Gpx;

            if (gpx is null)
            {
                _logger.LogWarning("Tried to deserailize file '{0}' into Gpx but got null as result. Aborting.", file.Name);
                return;
            }

            var ride = ConvertToRide(gpx);
            rides.Add(ride);
        }

        await using AppDbContext db = await _dbContextFactory.CreateDbContextAsync();
        db.Rides.AddRange(rides);
        await db.SaveChangesAsync();

        // Invalidate cache which stores rides.
        _cache.Remove("rides");
        sw.Stop();

        _logger.LogDebug("Took {0} ms to process {1} files.", sw.ElapsedMilliseconds, count);

        // Update flag so that 'Done' message can be displayed.
        _uploadDone = true;
    }

    public static Ride ConvertToRide(Gpx gpx)
    {
        List<TrackPoint> trackPoints = new List<TrackPoint>(gpx.Trk.Trkseg.Trkpt.Count);
        foreach (Trkpt trk in gpx.Trk.Trkseg.Trkpt)
        {
            trackPoints.Add(new TrackPoint()
            {
                Elevation = trk.Ele,
                Hdop = trk.Hdop,
                Latitude = trk.Lat,
                Longitude = trk.Lon,
                Speed = trk.Extensions is null ? 0 : trk.Extensions.Speed,
                Time = trk.Time
            });
        }

        int trackPointCount = trackPoints.Count;

        DateTime start = trackPoints[0].Time;
        DateTime end = trackPoints[trackPointCount - 1].Time;

        double elevationGain = 0.0d;
        double elevationLoss = trackPoints[0].Elevation;
        double elePrev = trackPoints[0].Elevation;
        double maxSpeed = double.MinValue;

        double latPrev = trackPoints[0].Latitude;
        double lonPrev = trackPoints[0].Longitude;
        double distance = 0.0d;

        int fastAccelerationCount = 0;
        int fastDecelerationCount = 0;
        DateTime dtPrev = trackPoints[0].Time;

        // NOTE: First value might be also null.
        double? speedPrev = trackPoints[0].Speed;

        double sumSpeed = 0.0d;

        for (int i = 0; i < trackPoints.Count; i++)
        {
            double dtSec = (trackPoints[i].Time - dtPrev).TotalSeconds;
            if (dtSec > 0)
            {
                double speedDelta = (trackPoints[i].Speed - speedPrev.Value) / 3.6 / dtSec;
                if (speedDelta > 2.0d)
                {
                    fastAccelerationCount++;
                }
                else if (speedDelta < -2.0d)
                {
                    fastDecelerationCount++;
                }
            }

            dtPrev = trackPoints[i].Time;
            speedPrev = trackPoints[i].Speed;

            // Speed and max speed.
            // NOTE: There are cases where speed value is not available.
            // Right now, to make visible I'm putting -1 as value.
            // In real case it would make more sense to copy previous data point.

            maxSpeed = Math.Max(maxSpeed, trackPoints[i].Speed);
            sumSpeed += trackPoints[i].Speed;

            // Elevation gain and loss.
            double diff = elePrev - trackPoints[i].Elevation;
            if (diff > 0.0d)
            {
                elevationGain += diff;
            }
            else
            {
                elevationLoss -= diff;
            }
            elePrev = trackPoints[i].Elevation;

            // Distnace
            distance += Geo.HaversineDistance(
                latPrev,
                lonPrev,
                trackPoints[i].Latitude,
                trackPoints[i].Longitude);
            latPrev = trackPoints[i].Latitude;
            lonPrev = trackPoints[i].Longitude;
        }

        double avgSpeed = sumSpeed / trackPointCount;

        var ride = new Ride
        {
            Start = start,
            End = end,
            ElevationGain = elevationGain,
            ElevationLoss = elevationLoss,
            FastAccelerationCount = fastAccelerationCount,
            FastDecelerationCount = fastDecelerationCount,
            MaxSpeed = maxSpeed,
            TrackPoints = trackPoints
        };

        return ride;
    }

    private static double GetElevationLoss(List<Trkpt> points)
    {
        double res = 0.0;
        for (int i = 1; i < points.Count; i++)
        {
            double diff = points[i].Ele - points[i - 1].Ele;
            if (diff < 0.0)
            {
                res += diff;
            }
        }

        return res;
    }

    private static double GetElevationGain(List<Trkpt> points)
    {
        double res = 0.0;
        for (int i = 1; i < points.Count; i++)
        {
            double diff = points[i].Ele - points[i - 1].Ele;
            if (diff > 0.0)
            {
                res += diff;
            }
        }

        return res;
    }

}
