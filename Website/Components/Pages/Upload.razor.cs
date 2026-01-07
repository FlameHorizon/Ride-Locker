using Website.Data;
using Website.Models;
using System.Xml.Serialization;
using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components.Forms;
using EFCore.BulkExtensions;

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

        // NOTE: Since EFCore.BulkExtensions does not support bulk insertion of graph objects
        // insertion has to be done in two passes. First, add parents and to all childrens.
        // This requires two-step process for inserting rides and track points.
        // More manuall work.

        await using var transaction = await db.Database.BeginTransactionAsync();

        _logger.LogDebug("Attempting to insert {0} rides...", rides.Count);
        await db.BulkInsertAsync(rides, o => o.SetOutputIdentity = true);

        // Make sure Id of the ride has changed from defualt.
        if (rides[0].Id == 0)
        {
            await transaction.RollbackAsync();
            _logger.LogWarning("Attempted to insert rides but Id has not been updated.");
            return;
        }

        // Insert track points as well.
        List<TrackPoint> allTrackPoints = [];
        foreach (var ride in rides)
        {
            foreach (var trackPoint in ride.TrackPoints)
            {
                trackPoint.RideId = ride.Id;
                allTrackPoints.Add(trackPoint);
            }
        }

        _logger.LogDebug("Attempting to insert {0} track points...", allTrackPoints.Count);
        await db.BulkInsertAsync(allTrackPoints);

        await transaction.CommitAsync();
        _logger.LogDebug(
            "Took {0} ms to insert {1} records into database.",
            sw.ElapsedMilliseconds,
            rides.Count + allTrackPoints.Count);

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
}
