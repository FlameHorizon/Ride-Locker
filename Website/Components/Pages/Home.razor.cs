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

    // Pagination
    private int _currentPage = 1;
    private int _pageSize = 10;
    private int _totalPages;
    private IEnumerable<Ride> _pagedRides = [];

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
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender == false) return;

        var sw = Stopwatch.StartNew();

        // NOTE: When values are retrieved from cache, they might be not sorted.
        Ride[]? result = await _cache.GetOrCreateAsync(
            "rides",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                await using AppDbContext db = await _dbContextFactory
                    .CreateDbContextAsync();

                return await db.Rides
                    .AsNoTracking()
                    .OrderByDescending(x => x.Start)
                    .ToArrayAsync();
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
        UpdatePagination();

        _logger.LogDebug("Took {0} ms to prepare data. Size {1}", sw.ElapsedMilliseconds, _rides.Length);
        StateHasChanged();
    }

    private void UpdatePagination()
    {
        if (_rides == null) return;

        _totalPages = (int)Math.Ceiling((double)_rides.Length / _pageSize);

        // Slice the array for the current view
        _pagedRides = _rides
            .Skip((_currentPage - 1) * _pageSize)
            .Take(_pageSize);
    }

    private void ChangePage(int newPage)
    {
        _currentPage = newPage;
        UpdatePagination();
        StateHasChanged();
    }
}
