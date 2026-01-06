using Website.Data;
using Website.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

namespace Website.Components.Pages;

public partial class Home
{
    private Ride[] _rides = [];
    private readonly ILogger<Home> _logger;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IMemoryCache _cache;

    public Home(
        ILogger<Home> logger,
        IDbContextFactory<AppDbContext> dbContextFactory,
        IMemoryCache cache)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
        _cache = cache;
    }

    protected override async Task OnInitializedAsync()
    {
        var sw = Stopwatch.StartNew();

        // NOTE: When values are retrieved from cache, they might be not sorted.
        Ride[]? result = await _cache.GetOrCreateAsync(
            "rides",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                await using AppDbContext db = await _dbContextFactory
                    .CreateDbContextAsync();

                return db.Rides
                    .AsNoTracking()
                    .Include(x => x.TrackPoints)
                    .OrderByDescending(x => x.Start)
                    .ToArray();
            }
        );
        _logger.LogDebug("Took {0} ms to query database.", sw.ElapsedMilliseconds);
        sw.Restart();

        if (result is null)
        {
            _logger.LogWarning("Expected to get set of rides from either database or cache, but got null");
            return;
        }

        // Ordering should be done in place to avoid Linq allocations.
        // Ordering is done here in-place and in reverse order (OrderByDescending).
        _rides = result;
        _rides.Sort((a, b) => b.Start.CompareTo(a.Start));
        _logger.LogDebug("Took {0} ms to prepare data. Size {1}", sw.ElapsedMilliseconds, _rides.Length);
    }
}
