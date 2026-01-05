using System.Runtime.CompilerServices;
using Website.Models;

public static class Geo
{
    private const double EarthRadiusKm = 6371.0;

    // WGS84 / Web Mercator helper constants
    private const double R = 6378137.0;
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    public static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = DegreesToRadians(lat2 - lat1);
        double dLon = DegreesToRadians(lon2 - lon1);

        lat1 = DegreesToRadians(lat1);
        lat2 = DegreesToRadians(lat2);

        double a = Math.Pow(Math.Sin(dLat / 2), 2) +
                   Math.Pow(Math.Sin(dLon / 2), 2) *
                   Math.Cos(lat1) * Math.Cos(lat2);

        double c = 2 * Math.Asin(Math.Sqrt(a));

        return EarthRadiusKm * c;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    // Convert lat/lon (degrees) -> Web Mercator meters (x, y)
    public static (double x, double y) LatLonToWebMercator(double latDeg, double lonDeg)
    {
        double lonRad = lonDeg * DegToRad;
        double latRad = latDeg * DegToRad;
        double x = R * lonRad;
        double y = R * Math.Log(Math.Tan(Math.PI / 4.0 + latRad / 2.0));
        return (x, y);
    }

    // Convert Web Mercator meters (x, y) -> lat/lon (degrees)
    public static (double latDeg, double lonDeg) WebMercatorToLatLon(double x, double y)
    {
        double lon = x / R * RadToDeg;
        double lat = (2.0 * Math.Atan(Math.Exp(y / R)) - Math.PI / 2.0) * RadToDeg;
        return (lat, lon);
    }

    // Given lat/lon and an origin (lat0,lon0) and cellSize in meters,
    // compute local projected coordinates and integer grid indices (ix, iy).
    // Grid cell boundaries follow: ix = Floor((x_local) / cellSize)
    public static (double localX, double localY, long ix, long iy) LatLonToGrid(
        double latDeg,
        double lonDeg,
        double originLatDeg,
        double originLonDeg,
        double cellSizeMeters)
    {
        (double x, double y) = LatLonToWebMercator(latDeg, lonDeg);
        (double ox, double oy) = LatLonToWebMercator(originLatDeg, originLonDeg);

        double localX = x - ox;
        double localY = y - oy;

        long ix = (long)Math.Floor(localX / cellSizeMeters);
        long iy = (long)Math.Floor(localY / cellSizeMeters);

        return (localX, localY, ix, iy);
    }

    public static (double x, double y) LatLonToLocal(double lat, double lon, double lat0, double lon0)
    {
        (double x, double y) p = LatLonToWebMercator(lat, lon);
        (double x, double y) o = LatLonToWebMercator(lat0, lon0);
        return (p.x - o.x, p.y - o.y);
    }

    public static double HaversineDistance(List<TrackPoint> segments)
    {
        double totalDistance = 0.0;

        for (int i = 1; i < segments.Count; i++)
            totalDistance += HaversineDistance(
                segments[i - 1].Latitude,
                segments[i - 1].Longitude,
                segments[i].Latitude,
                segments[i].Longitude);

        return totalDistance;
    }

}
