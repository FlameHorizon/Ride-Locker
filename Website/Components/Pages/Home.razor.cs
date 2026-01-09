using Website.Data;
using Website.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

namespace Website.Components.Pages;

// TODO: Add pagination.
public partial class Home
{
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
    }

    protected override async Task OnInitializedAsync()
    {
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender == false) return;

        var sw = Stopwatch.StartNew();

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
}
