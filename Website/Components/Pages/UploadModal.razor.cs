using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using EFCore.BulkExtensions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Website.Data;
using Website.Models;
using Website.Services;

namespace Website.Components.Pages;

public partial class UploadModal
{
    private readonly UploadModalStateService _uploadModalState;
    private readonly ILogger<UploadModal> _logger;
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly CacheSignalService _cacheSignal;

    public UploadModal(
        UploadModalStateService uploadModalState,
        ILogger<UploadModal> logger,
        IMemoryCache cache,
        IDbContextFactory<AppDbContext> dbContextFactory,
        CacheSignalService cacheSignal)
    {
        _uploadModalState = uploadModalState;
        _logger = logger;
        _cache = cache;
        _dbContextFactory = dbContextFactory;
        _cacheSignal = cacheSignal;
    }

    protected override void OnInitialized()
    {
        // Everytime the state of the modal changes, re-render the page.
        _uploadModalState.OnChanged += StateHasChanged;
    }

    public void Dispose() => _uploadModalState.OnChanged -= StateHasChanged;

    private async Task HandleFile(InputFileChangeEventArgs e)
    {
        const int MAX_FILE_COUNT = 50;
        int count = e.FileCount;
        if (count > MAX_FILE_COUNT)
        {
            _uploadModalState.ErrorMessage = $"You can only upload up to {MAX_FILE_COUNT} files at once.";
            _logger.LogWarning("User attempted to upload {0} files, but count limit is {1}.",
                count,
                MAX_FILE_COUNT);
            return;
        }

        const int MAX_FILE_SIZE_BYTES = 1024 * 1024 * 5; // 5MB
        foreach (IBrowserFile file in e.GetMultipleFiles(count))
        {
            if (file.Size > MAX_FILE_SIZE_BYTES)
            {
                _uploadModalState.ErrorMessage = $"File '{file.Name}' exceeds the 5MB limit.";
                _logger.LogWarning("User attempted to upload file '{0}' of size {1}, but size limit is {2}. Aborting.",
                    file.Name,
                    file.Size,
                    MAX_FILE_SIZE_BYTES);
                return;
            }

            string ext = Path.GetExtension(file.Name).ToLower();
            if (ext != ".gpx")
            {
                _uploadModalState.ErrorMessage = $"'{file.Name}' is not a valid GPX file.";
                _logger.LogWarning("User attempted to upload file '{0}' with extension '{1}', but expected extension is '.gpx'. Aborting.",
                    file.Name,
                    ext);
                return;
            }

            // Collect file sizes to display transfer metrics.
            _uploadModalState.TotalSize += file.Size;
        }

        var sw = new Stopwatch();
        sw.Start();

        _logger.LogDebug("Loading {0} files.", count);
        List<Ride> rides = [];

        var serializer = new XmlSerializer(typeof(Gpx));

        // NOTE: Here I'm working with progress bar data
        // but this isn't really tracking speed of network transfer since
        // I'm also processing data in memory which also take time.
        //
        // Another thing is that if user selects multiple files and one of them
        // fails to process, we reject entire upload, even files which are correct.
        double elapsedSeconds = sw.Elapsed.TotalSeconds;
        foreach (IBrowserFile file in e.GetMultipleFiles(count))
        {
            // NOTE: Wierd things are happening when I declare stream outside of the loop.
            // I've tried to flush it at the end of each iteraction but it hangs.
            var ms = new MemoryStream();
            await file.OpenReadStream(MAX_FILE_SIZE_BYTES).CopyToAsync(ms);
            _uploadModalState.BytesUploaded += file.Size;

            ms.Position = 0;
            Gpx? gpx = null;
            try
            {
                // In case of error, we want to show the error message to the user.
                // User might want to any upload file which matches .gpx extension.
                gpx = serializer.Deserialize(ms) as Gpx;
            }
            catch (Exception ex)
            {
                _uploadModalState.ErrorMessage = $"Failed to import file '{file.Name}'. Nothing was uploaded.";
                _logger.LogError(ex, "Failed to deserialize file '{0}' into Gpx object.", file.Name);
                return;
            }

            elapsedSeconds = sw.Elapsed.TotalSeconds;

            if (gpx is null)
            {
                _uploadModalState.ErrorMessage = $"Failed to import file '{file.Name}'. Nothing was uploaded.";
                _logger.LogWarning("Tried to deserailize file '{0}' into Gpx object but got null as result. Aborting.", file.Name);
                return;
            }

            if (elapsedSeconds > 0.0)
            {
                _uploadModalState.TransferSpeedKbs = (_uploadModalState.BytesUploaded / 1024.0) / elapsedSeconds;
            }

            _uploadModalState.NotifyProgress();
            StateHasChanged();

            var ride = ConvertToRide(gpx);

            // Separetly, add file name and extension to the ride.
            ride.FullName = SanitizeFileName(file.Name);
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

        // NOTE: From MS docs. it look like CurrentEstimatedSize is not tracked
        // until SizeLimit is not set. Shame. At some point I might work on this.
        //long? before = _cache.GetCurrentStatistics()?.CurrentEstimatedSize ?? -1;
        //_logger.LogDebug("Size of the cache before invalidating: {0}", before);

        // Invalidate cache which stores rides and track points.
        _cache.Remove("tracks");
        _cache.Remove("rides_total_count");
        _cacheSignal.Reset();

        sw.Stop();
        _logger.LogDebug("Took {0} ms to clear cache.", sw.ElapsedMilliseconds);

        // Close modal windown once upload is done.
        // TODO: Show toast pop up with "Upload completed" and ask to refresh the page.
        // Or maybe we can refresh webpage automatically?
        _uploadModalState.Close();
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
        double speedMetersPerSecondPrev = firstTrk.Extensions?.Speed ?? 0;

        double elevationGain = 0, elevationLoss = 0;
        double speedMetersPerSecondMax = double.MinValue, sumSpeed = 0;
        int fastAcc = 0, fastDec = 0;

        // 2. Single loop for conversion and calculation
        foreach (var trk in trkpts)
        {
            var speedMetersPerSecondCurrent = trk.Extensions?.Speed ?? 0;
            var tp = new TrackPoint
            {
                Elevation = trk.Ele,
                Hdop = trk.Hdop,
                Latitude = trk.Lat,
                Longitude = trk.Lon,
                Speed = speedMetersPerSecondCurrent,
                Time = trk.Time
            };
            trackPoints.Add(tp);

            // Physics/Stats calculations
            double dtSec = (tp.Time - dtPrev).TotalSeconds;
            if (dtSec > 0)
            {
                double accel = (speedMetersPerSecondCurrent - speedMetersPerSecondPrev) / dtSec;
                if (accel > 2.0d) fastAcc++;
                else if (accel < -2.0d) fastDec++;
            }

            speedMetersPerSecondMax = Math.Max(speedMetersPerSecondMax, speedMetersPerSecondCurrent);
            // TODO: Remove variable below, does not need to be here.
            sumSpeed += speedMetersPerSecondCurrent;

            double eleDiff = tp.Elevation - elePrev;

            // If elevetion difference is negative, it means that elevation has decreased.
            // Example: T1e = 100m, T2e = 105m
            // delta = T2e - T1e = 5m; we are 5m higher than before.
            if (eleDiff > 0.0d) elevationGain += eleDiff;
            else elevationLoss -= eleDiff;

            // Update "Prev" values
            elePrev = tp.Elevation;
            latPrev = tp.Latitude;
            lonPrev = tp.Longitude;
            dtPrev = tp.Time;
            speedMetersPerSecondPrev = speedMetersPerSecondCurrent;
        }

        return new Ride
        {
            Start = trkpts[0].Time,
            End = trkpts[count - 1].Time,
            ElevationGain = elevationGain,
            ElevationLoss = elevationLoss,
            FastAccelerationCount = fastAcc,
            FastDecelerationCount = fastDec,
            MaxSpeed = speedMetersPerSecondMax,
            Distance = Geo.HaversineDistance(trackPoints),
            TrackPoints = trackPoints,
            Created = DateTime.UtcNow,
        };
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "unknown_file";

        // 1. Remove path information
        string nameOnly = Path.GetFileName(fileName);

        // 2. Replace any character that is NOT a letter, digit, dot, hyphen, or underscore
        string cleanName = Regex.Replace(nameOnly, @"[^a-zA-Z0-9\.\-_]", "_");

        // 3. Optional: Truncate length to prevent UI overflow (e.g., 50 chars)
        if (cleanName.Length > 50)
        {
            string ext = Path.GetExtension(cleanName);
            cleanName = cleanName.Substring(0, 40) + "..." + ext;
        }

        return cleanName;
    }
}
