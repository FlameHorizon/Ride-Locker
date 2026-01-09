namespace Website.Components.Pieces;

using Website.Models;
using Microsoft.AspNetCore.Components;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Path = System.IO.Path;
using Microsoft.EntityFrameworkCore;
using Website.Data;

public partial class RideTableRow
{
    // NOTE: Property is automatically populated by the fact that this is 
    // a required parameter.
    [Parameter][EditorRequired] public Ride Ride { get; set; } = new();
    private readonly ILogger<RideTableRow> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private string _iconPath = "";

    public RideTableRow(
        ILogger<RideTableRow> logger,
        IWebHostEnvironment env,
        IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _logger = logger;
        _env = env;
        _dbContextFactory = dbContextFactory;
    }

    // This runs every time the 'Ride' parameter changes (e.g., during pagination)
    protected override async Task OnParametersSetAsync()
    {
        // Fetch the new icon for the new Ride ID
        _iconPath = await CreateIcon();
    }

    private string FormatTimeAgo(DateTime dt)
    {
        TimeSpan ts = DateTime.Now - dt;
        if (ts.TotalDays > 1)
        {
            return $"{ts.Days} days ago";
        }

        if (ts.TotalHours > 1)
        {
            return $"{ts.Hours} hours ago";
        }

        if (ts.TotalMinutes > 1)
        {
            return $"{ts.Minutes} minutes ago";
        }

        return $"{ts.Seconds} seconds ago";
    }

    // Should be GetOrCreateIcon
    private async Task<string> CreateIcon()
    {
        // Check, if icon already exists for this ride.
        string storagePath = Path.Combine(_env.WebRootPath, "uploads", Ride.Id.ToString());
        string iconPath = Path.Combine(storagePath, "icon.png");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (File.Exists(iconPath))
        {
            return await Task.FromResult(_env.GetRelativeWebPath(iconPath));
        }

        _logger.LogDebug("Took {0} ms to check if file exists.", sw.ElapsedMilliseconds);
        sw.Restart();

        // NOTE: Since track points are not collected for rides on home page,
        // we need to fetch them from database for each ride which needs it
        // to create the icon. I'm not donwloading them along with rides
        // because track points not on Home page.
        using var db = await _dbContextFactory.CreateDbContextAsync();
        List<TrackPoint> trackPoints = await db.TrackPoints
            .AsNoTracking()
            .Where(x => x.RideId == Ride.Id)
            .ToListAsync();

        _logger.LogDebug("Took {0} ms to query database.", sw.ElapsedMilliseconds);
        sw.Restart();

        using var image = CreateImage(trackPoints);

        _logger.LogDebug("Took {0} ms to create an icon image.", sw.ElapsedMilliseconds);
        sw.Restart();

        // Absolute path allows to manage files on the system, whereas 
        // relative path allows to display content on website.
        if (Directory.Exists(storagePath) == false)
        {
            Directory.CreateDirectory(storagePath);
        }

        _logger.LogDebug("Took {0} ms to create directory.", sw.ElapsedMilliseconds);
        sw.Restart();

        _logger.LogInformation("Saving icon at '{0}'", iconPath);

        await image.SaveAsPngAsync(iconPath, new PngEncoder());
        _logger.LogDebug("Took {0} ms to save icon.", sw.ElapsedMilliseconds);

        string relPath = _env.GetRelativeWebPath(iconPath);
        _logger.LogInformation("Relative path of the icon is '{0}'", relPath);

        return await Task.FromResult(relPath);
    }

    /// <summary>
    /// User of this method is resposible for disposing the image.
    /// </summary>
    /// TODO: For shorter rides we can switch to drawing a line since they would look nicer.
    public static Image<Rgba32> CreateImage(Ride ride)
    {
        return CreateImage(ride.TrackPoints);
    }

    public static Image<Rgba32> CreateImage(IEnumerable<TrackPoint> track)
    {
        double originLat = track.First().Latitude;
        double originLon = track.First().Longitude;

        // Compute grid indices relative to origin
        IEnumerable<(double x, double y)> local = track
            .Select(x => Geo.LatLonToLocal(x.Latitude, x.Longitude, originLat, originLon))
            .ToList();

        // Find bounding box to of the local values to center points
        double minX = local.Min(p => p.x);
        double maxX = local.Max(p => p.x);
        double minY = local.Min(p => p.y);
        double maxY = local.Max(p => p.y);

        double width = maxX - minX;
        double height = maxY - minY;

        const int imageSize = 50;

        // Scale so that the largest dimension fits in imageSize
        double scale = width > height ? imageSize / width : imageSize / height;
        double heightOffset = (imageSize - height * scale) / 2;

        List<(int x, int y)> px = local.Select(p => (
            x: (int)((p.x - minX) * scale),
            y: (int)((p.y - minY) * scale + heightOffset)
        )).ToList();

        // Convert to PointF
        List<PointF> points = new List<PointF>(px.Count / 2);

        // Allows to eliminate count check.
        points.Add(new PointF(px[0].x, px[0].y));

        for (int i = 1; i < px.Count; i++)
        {
            var p = px[i];
            var currentPoint = new PointF(p.x, p.y);

            // Only add if it's different from the previous pixel
            if (points[^1] != currentPoint)
            {
                points.Add(currentPoint);
            }
        }

        // NOTE: Setting color as transparent during initialization and not filling image with it makes code run faster by 10% (97 -> 90)
        // Also, we can skip a lot of overhead while setting up configuration with just 
        // passing empty confing. Loding that, we can reduce time during cold start.
        // But our image object will be kinda dumb. It will not know who to save itself
        // as encoders for png, provided by defualt well be missing.
        Image<Rgba32> image = new(new Configuration(), imageSize, imageSize, Color.Transparent);

        // PERF: For performance reasons instead of drawing a line,
        // I'm drawing each pixel individually since drawing a line
        // which is smooth is much slower. Image will not look as nice,
        // but the speed is better.
        image.ProcessPixelRows(accessor =>
        {
            foreach (var p in points)
            {
                int x = (int)p.X;
                int y = (int)p.Y;

                if (x >= 0 && x < accessor.Width && y >= 0 && y < accessor.Height)
                {
                    // Get a reference to the specific row
                    Span<Rgba32> pixelRow = accessor.GetRowSpan(y);

                    // Set the pixel in that row
                    pixelRow[x] = Color.White;
                }
            }
        });

        return image;
    }
}
