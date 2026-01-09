using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Website.Data;
using Website.Models;

namespace Website.Components.Pages;

public partial class AltHome
{
    private double _smoothnessScore;
    private int _gForceAlerts;
    private int _hardBrakingEvents;
    private double _totalDistanceAnalyze;

    private Ride[] _rides = [];
    private readonly ILogger<Home> _logger;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IMemoryCache _cache;
    private readonly CacheSignalService _cacheSignal;

    // Pagination
    private int _currentPage = 1;
    private int _pageSize = 10;
    private int _totalPages;
    private IEnumerable<Ride> _pagedRides = [];
    private int _totalCount;

    // Toast
    private bool _showToast = false;

    public AltHome(
        ILogger<Home> logger,
        IDbContextFactory<AppDbContext> dbContextFactory,
        IMemoryCache cache,
        CacheSignalService cacheSignal)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
        _cache = cache;
        _cacheSignal = cacheSignal;
    }

    protected override async Task OnInitializedAsync()
    {
        _cacheSignal.OnCacheInvalidated += HandleCacheInvalidated;
    }

    private async void HandleCacheInvalidated()
    {
        await InvokeAsync(async () =>
        {
            _logger.LogDebug("Cache has been invalidated. Refreshing page.");
            var sw = Stopwatch.StartNew();
            _totalCount = await _cache.GetOrCreateAsync("rides_total_count", async entry =>
            {
                _logger.LogDebug("Cache miss for rides_total_count. Fetching from DB...");
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                await using var db = await _dbContextFactory.CreateDbContextAsync();
                return await db.Rides.CountAsync();
            });

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            _totalDistanceAnalyze = await db.Rides.SumAsync(x => x.Distance);
            _hardBrakingEvents = await db.Rides.SumAsync(x => x.FastDecelerationCount);
            _gForceAlerts = _hardBrakingEvents + await db.Rides.SumAsync(x => x.FastAccelerationCount);
            _smoothnessScore = await db.Rides.AverageAsync(x => x.SmoothnessScore);

            _totalPages = (int)Math.Ceiling((double)_totalCount / _pageSize);
            _pagedRides = await GetRides();

            StateHasChanged();
            _logger.LogDebug("Took {0} ms to get data for page.", sw.ElapsedMilliseconds);

            _showToast = true; // Show the toast
            StateHasChanged();

            // Auto-hide the toast after 4 seconds
            await Task.Delay(4000);
            _showToast = false;
            StateHasChanged();
        });
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender == false) return;
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        if (db.Rides.Any() == false) return;

        // NOTE: Here, we are asking about couple of things abour rides,
        // like total travel time, count, fast acc./dec. and othes.
        // It will be worth later to understand if wouldn't be better
        // to fetch all rides for the user and do all of these operations
        // in memory. Also, my thing can be cached.

        var sw = Stopwatch.StartNew();

        _totalDistanceAnalyze = await db.Rides.SumAsync(x => x.Distance);
        _hardBrakingEvents = await db.Rides.SumAsync(x => x.FastDecelerationCount);
        _gForceAlerts = _hardBrakingEvents + await db.Rides.SumAsync(x => x.FastAccelerationCount);
        _smoothnessScore = await db.Rides.AverageAsync(x => x.SmoothnessScore);

        // TODO: Remember to invalidate cache when new files are imported.
        _totalCount = await _cache.GetOrCreateAsync("rides_total_count", async entry =>
        {
            _logger.LogDebug("Cache miss for rides_total_count. Fetching from DB...");
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            return await db.Rides.CountAsync();
        });

        _pagedRides = await GetRides();
        _logger.LogDebug("Took {0} ms to get data for page.", sw.ElapsedMilliseconds);
        sw.Restart();

        // Ordering should be done in place to avoid Linq allocations.
        // Ordering is done here in-place and in reverse order (OrderByDescending).
        _totalPages = (int)Math.Ceiling((double)_totalCount / _pageSize);
        _logger.LogDebug("Took {0} ms to prepare data. Size {1}", sw.ElapsedMilliseconds, _rides.Length);
        StateHasChanged();
    }

    private async Task<Ride[]> GetRides()
    {
        string cacheKey = $"rides_page_{_currentPage}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

            // Allow for cache invalidation when new data is uploaded.
            entry.AddExpirationToken(_cacheSignal.GetToken());

            await using var db = await _dbContextFactory.CreateDbContextAsync();

            _logger.LogDebug("Cache miss for {0}. Fetching from DB...", cacheKey);

            return await db.Rides
                .AsNoTracking()
                .OrderByDescending(x => x.Start)
                .Skip((_currentPage - 1) * _pageSize) // Skip 10 items
                .Take(_pageSize)                     // Only take 10
                .ToArrayAsync();
        }) ?? Array.Empty<Ride>();
    }

    private async Task ChangePage(int newPage)
    {
        _currentPage = newPage;
        _logger.LogDebug("Change Page to {0}", _currentPage);

        var sw = Stopwatch.StartNew();
        _pagedRides = await GetRides();
        _logger.LogDebug("Took {0} ms to get data for page.", sw.ElapsedMilliseconds);
    }

    public void Dispose()
    {
        _cacheSignal.OnCacheInvalidated -= HandleCacheInvalidated;
    }


}
