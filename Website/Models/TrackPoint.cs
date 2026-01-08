namespace Website.Models;

public class TrackPoint
{
    public int Id { get; set; }
    public int RideId { get; set; }
    public double Elevation { get; set; }
    public DateTime Time { get; set; }
    public double Hdop { get; set; }

    /// <summary>
    /// Speed in meters per second.
    /// </summary>
    public double Speed { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
