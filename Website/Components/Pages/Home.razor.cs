using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Website.Data;
using Website.Models;

namespace Website.Components.Pages;

public partial class Home
{
    [SupplyParameterFromQuery(Name = "filter")]
    public string? SelectedFilter { get; set; }

    private double _smoothnessScore;
    private int _gForceAlerts;
    private int _hardBrakingEvents;
    private double _totalDistanceAnalyzed;

    private Ride[] _rides = [];
    private readonly ILogger<Home> _logger;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IMemoryCache _cache;
    private readonly CacheSignalService _cacheSignal;

    // Pagination
    private int _currentPage = 1;
    private int _pageSize = 10;
    private int _totalPages;
    private IEnumerable<Ride> _displayedRides = [];
    private int _totalCount;

    // Toast
    private bool _showToast = false;

    // Other
    private Filter _currentFilter;

    public Home(
        ILogger<Home> logger,
        IDbContextFactory<AppDbContext> dbContextFactory,
        IMemoryCache cache,
        CacheSignalService cacheSignal)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
        _cache = cache;
        _cacheSignal = cacheSignal;

        // FIX: These are duplices.
        _currentFilter = Filter.All;
    }

    protected override async Task OnInitializedAsync()
    {
        _cacheSignal.OnCacheInvalidated += HandleCacheInvalidated;
        SelectedFilter = "all";
    }

    private async void HandleCacheInvalidated()
    {
        await InvokeAsync(async () =>
        {
            _logger.LogDebug("Reciever singal that cache needs to be refeshed.");
            var sw = Stopwatch.StartNew();

            // If we got information that cache has been invalidated, I'm forcing
            // certain kets to be updated since I don't have a of knowing which 
            // keys has been invalidated.
            await UpdateCacheAsync();

            StateHasChanged();
            _logger.LogDebug("Took {0} ms to refresh cache.", sw.ElapsedMilliseconds);

            _showToast = true; // Show the toast
            StateHasChanged();

            // Auto-hide the toast after 4 seconds
            await Task.Delay(4000);
            _showToast = false;
            StateHasChanged();
        });
    }

    private async Task UpdateCacheAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var stats = await db.Rides
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Distance = g.Sum(x => x.Distance),
                HardBraking = g.Sum(x => x.FastDecelerationCount),
                FastAccents = g.Sum(x => x.FastAccelerationCount),
                AvgSmoothness = g.Average(x => x.SmoothnessScore)
            })
            .FirstOrDefaultAsync();

        if (stats is not null)
        {
            _totalCount = stats.Count;
            _totalDistanceAnalyzed = stats.Distance;
            _hardBrakingEvents = stats.HardBraking;
            _gForceAlerts = stats.HardBraking + stats.FastAccents;
            _smoothnessScore = stats.AvgSmoothness;

            SetInCache(CacheKeys.Get(CacheKey.TotalCount), _totalCount);
            SetInCache(CacheKeys.Get(CacheKey.TotalDistance), _totalDistanceAnalyzed);
            SetInCache(CacheKeys.Get(CacheKey.HardBrakingEvents), _hardBrakingEvents);
            SetInCache(CacheKeys.Get(CacheKey.GForceAlerts), _gForceAlerts);
            SetInCache(CacheKeys.Get(CacheKey.SmoothnessScore), _smoothnessScore);
        }

        _totalPages = (int)Math.Ceiling((double)_totalCount / _pageSize);
        _displayedRides = await GetRidesPaged(db, _pageSize);
        _currentFilter = Filter.All;
        SelectedFilter = "all";
        await ChangePage(1);
    }

    private void SetInCache<TItem>(object key, TItem value)
    {
        // QUEST: I'm not sure if this code would fail if key does not exist.
        // Right now, I know this key will exist for sure since it is
        // created when page is loaded.
        _cache.Set(
            key,
            value,
            new MemoryCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                ExpirationTokens = { _cacheSignal.GetToken() }
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
        _totalDistanceAnalyzed = await _cache.GetOrCreateAsync(
            CacheKeys.Get(CacheKey.TotalDistance),
            async entry =>
            {
                _logger.LogDebug("Cache miss for {0}. Creating value...", CacheKeys.Get(CacheKey.TotalDistance));
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                entry.AddExpirationToken(_cacheSignal.GetToken());
                return await db.Rides.SumAsync(x => x.Distance);
            });

        _hardBrakingEvents = await _cache.GetOrCreateAsync(
            CacheKeys.Get(CacheKey.HardBrakingEvents),
            async entry =>
            {
                _logger.LogDebug("Cache miss for {0}. Creating value...", CacheKeys.Get(CacheKey.HardBrakingEvents));
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                entry.AddExpirationToken(_cacheSignal.GetToken());
                return await db.Rides.SumAsync(x => x.FastDecelerationCount);
            });

        _gForceAlerts = await _cache.GetOrCreateAsync(
            CacheKeys.Get(CacheKey.GForceAlerts),
            async entry =>
            {
                _logger.LogDebug("Cache miss for {0}. Creating value...", CacheKeys.Get(CacheKey.GForceAlerts));
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                entry.AddExpirationToken(_cacheSignal.GetToken());
                return await db.Rides.SumAsync(x => x.FastAccelerationCount + x.FastDecelerationCount);
            });


        _smoothnessScore = await _cache.GetOrCreateAsync(
            CacheKeys.Get(CacheKey.SmoothnessScore),
            async entry =>
            {
                _logger.LogDebug("Cache miss for {0}. Creating value...", CacheKeys.Get(CacheKey.SmoothnessScore));
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                entry.AddExpirationToken(_cacheSignal.GetToken());
                return await db.Rides.AverageAsync(x => x.SmoothnessScore);
            });

        _totalCount = await _cache.GetOrCreateAsync(
            CacheKeys.Get(CacheKey.TotalCount),
            async entry =>
        {
            _logger.LogDebug("Cache miss for {0}. Fetching from DB...", CacheKeys.Get(CacheKey.TotalCount));
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            entry.AddExpirationToken(_cacheSignal.GetToken());
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            return await db.Rides.CountAsync();
        });

        _displayedRides = await GetRidesPaged(db, _pageSize);
        _logger.LogDebug("Took {0} ms to get data for page.", sw.ElapsedMilliseconds);
        sw.Restart();

        // Ordering should be done in place to avoid Linq allocations.
        // Ordering is done here in-place and in reverse order (OrderByDescending).
        _totalPages = (int)Math.Ceiling((double)_totalCount / _pageSize);
        _logger.LogDebug("Took {0} ms to prepare data. Size {1}", sw.ElapsedMilliseconds, _rides.Length);
        StateHasChanged();
    }

    private async Task<Ride[]> GetRidesPaged(AppDbContext db, int pageSize)
    {
        string cacheKey = $"rides_page_{_currentPage}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            _logger.LogDebug("Cache miss for {0}. Fetching from DB...", cacheKey);
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            entry.AddExpirationToken(_cacheSignal.GetToken());

            return await db.Rides
                .AsNoTracking()
                .OrderByDescending(x => x.Start)
                .Skip((_currentPage - 1) * _pageSize) // Skip 10 items
                .Take(pageSize)                     // Only take 10
                .ToArrayAsync();
        }) ?? Array.Empty<Ride>();
    }

    private async Task ChangePage(int newPage)
    {
        if (_currentPage == newPage) return;

        _currentPage = newPage;
        _logger.LogDebug("Change Page to {0}", _currentPage);

        var sw = Stopwatch.StartNew();
        using var db = await _dbContextFactory.CreateDbContextAsync();
        _displayedRides = await GetRidesPaged(db, _pageSize);
        _logger.LogDebug("Took {0} ms to get data for page.", sw.ElapsedMilliseconds);
    }

    public void Dispose()
    {
        _cacheSignal.OnCacheInvalidated -= HandleCacheInvalidated;
    }

    private IEnumerable<int> GetVisiblePages()
    {
        if (_currentFilter == Filter.Smooth)
            return new[] { 1 };

        const int windowSize = 3; // How many pages to show before/after current
        var pages = new List<int>();

        int startPage = Math.Max(1, _currentPage - windowSize);
        int endPage = Math.Min(_totalPages, _currentPage + windowSize);

        for (int i = startPage; i <= endPage; i++)
        {
            pages.Add(i);
        }

        return pages;
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

    private async Task ShowSmoothRidesAsync()
    {
        _currentFilter = Filter.Smooth;

        // Get 10 recent rides which have score higher than 90%
        using var db = await _dbContextFactory.CreateDbContextAsync();
        var rides = await db.Rides
            .AsNoTracking()
            .Where(x => x.SmoothnessScore > 90)
            .OrderByDescending(x => x.Start)
            .Take(10)
            .ToArrayAsync();

        if (rides.Length == 0)
        {
            // TODO: Display message that no rides found with high smoothness.
            _logger.LogWarning("No rides found with high smoothness.");
            return;
        }

        _totalPages = 1;
        await ChangePage(1);
        _displayedRides = rides;
    }

    private enum Filter
    {
        All,
        Smooth
    }

    private async Task ShowAllRidesAsync()
    {
        _currentFilter = Filter.All;

        // Restore state of the pagination element.
        _totalPages = (int)Math.Ceiling((double)_totalCount / _pageSize);
        await ChangePage(1);

        _logger.LogDebug("Change Page to {0}", _currentPage);

        var sw = Stopwatch.StartNew();
        using var db = await _dbContextFactory.CreateDbContextAsync();
        _displayedRides = await GetRidesPaged(db, _pageSize);
        _logger.LogDebug("Took {0} ms to get data for page.", sw.ElapsedMilliseconds);
    }

    private async Task ToggleFilter(string filterName)
    {
        if (filterName == SelectedFilter) return;
        SelectedFilter = filterName;

        string? newValue = filterName;
        if (SelectedFilter == "smooth")
        {
            await ShowSmoothRidesAsync();
        }
        else
        {
            await ShowAllRidesAsync();
        }
    }
}
