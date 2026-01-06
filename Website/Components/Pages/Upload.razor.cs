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

        // Counters
        var sw = new Stopwatch();
        sw.Start();

        _logger.LogDebug("Loading {0} files.", count);
        List<Ride> rides = [];

        var serializer = new XmlSerializer(typeof(Gpx));
        foreach (IBrowserFile file in e.GetMultipleFiles(count))
        {
            // NOTE: Wierd things are happening when I declare stream outside of the loop.
            // I've tried to flush it at the end of each iteraction but it hangs.
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

        _logger.LogDebug("Took {0} ms to process files.", sw.ElapsedMilliseconds);
        sw.Restart();

        await using AppDbContext db = await _dbContextFactory.CreateDbContextAsync();

        // Synchronous = Off: Tells SQLite not to wait for the disk to physically acknowledge the write before moving on.
        // Journal Mode = WAL (Write-Ahead Logging): Allows concurrent reads and much faster writes.
        await db.Database.ExecuteSqlRawAsync("PRAGMA synchronous = OFF;");
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;");

        await db.Rides.AddRangeAsync(rides);
        int recordCount = await db.SaveChangesAsync();

        _logger.LogDebug("Took {0} ms to insert {1} records into database.", sw.ElapsedMilliseconds, recordCount);
        sw.Restart();

        // Invalidate cache which stores rides.
        _cache.Remove("rides");
        sw.Stop();
        _logger.LogDebug("Took {0} ms to clear cache.", sw.ElapsedMilliseconds);

        // Update flag so that 'Done' message can be displayed.
        _uploadDone = true;
    }

    public static Ride ConvertToRide(Gpx gpx)
    {
        var trkpts = gpx.Trk.Trkseg.Trkpt;
        int count = trkpts.Count;
        if (count == 0) return new Ride();

        // 1. Pre-size the list to avoid resizing overhead
        List<TrackPoint> trackPoints = new List<TrackPoint>(count);

        // Initialize stats with the first point data
        var firstTrk = trkpts[0];
        double elePrev = firstTrk.Ele;
        double latPrev = firstTrk.Lat;
        double lonPrev = firstTrk.Lon;
        DateTime dtPrev = firstTrk.Time;
        double speedPrev = firstTrk.Extensions?.Speed ?? 0;

        double elevationGain = 0, elevationLoss = firstTrk.Ele;
        double maxSpeed = double.MinValue, sumSpeed = 0;
        int fastAcc = 0, fastDec = 0;

        // 2. Single loop for conversion and calculation
        foreach (var trk in trkpts)
        {
            var currentSpeed = trk.Extensions?.Speed ?? 0;
            var tp = new TrackPoint
            {
                Elevation = trk.Ele,
                Hdop = trk.Hdop,
                Latitude = trk.Lat,
                Longitude = trk.Lon,
                Speed = currentSpeed,
                Time = trk.Time
            };
            trackPoints.Add(tp);

            // Physics/Stats calculations
            double dtSec = (tp.Time - dtPrev).TotalSeconds;
            if (dtSec > 0)
            {
                double accel = (currentSpeed - speedPrev) / 3.6 / dtSec;
                if (accel > 2.0d) fastAcc++;
                else if (accel < -2.0d) fastDec++;
            }

            maxSpeed = Math.Max(maxSpeed, currentSpeed);
            sumSpeed += currentSpeed;

            double eleDiff = elePrev - tp.Elevation;
            if (eleDiff > 0.0d) elevationGain += eleDiff;
            else elevationLoss -= eleDiff;

            // Update "Prev" values
            elePrev = tp.Elevation;
            latPrev = tp.Latitude;
            lonPrev = tp.Longitude;
            dtPrev = tp.Time;
            speedPrev = currentSpeed;
        }

        return new Ride
        {
            Start = trkpts[0].Time,
            End = trkpts[count - 1].Time,
            ElevationGain = elevationGain,
            ElevationLoss = elevationLoss,
            FastAccelerationCount = fastAcc,
            FastDecelerationCount = fastDec,
            MaxSpeed = maxSpeed,
            TrackPoints = trackPoints,
        };
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
