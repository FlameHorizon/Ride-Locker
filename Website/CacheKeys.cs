namespace Website;

public enum CacheKey
{
    TotalCount,
    TotalDistance,
    HardBrakingEvents,
    GForceAlerts,
    SmoothnessScore
}

public static class CacheKeys
{
    public static readonly Dictionary<CacheKey, string> Map = new()
    {
        { CacheKey.TotalCount, "rides_total_count" },
        { CacheKey.TotalDistance, "rides_total_distance" },
        { CacheKey.HardBrakingEvents, "rides_hard_braking_events" },
        { CacheKey.GForceAlerts, "rides_gforce_alerts" },
        { CacheKey.SmoothnessScore, "rides_smoothness_score" }
    };

    // Helper method for cleaner syntax
    public static string Get(CacheKey key) => Map[key];
}
