namespace Website.Models;

public class Ride
{
    public int Id { get; set; }

    /// <summary>
    /// In meters per second.
    /// </summary>
    public double MaxSpeed { get; set; }

    /// <summary>
    /// Average speed across ride in meters per second.
    /// </summary>
    public double AvgSpeed => Distance * 1000 / Duration.TotalSeconds;

    /// <summary>
    /// Start of the ride.
    /// </summary>
    public DateTime Start { get; set; }

    /// <summary>
    /// End of the ride.
    /// </summary>
    public DateTime End { get; set; }

    /// <summary>
    /// Duration of the entire ride.
    /// </summary>
    public TimeSpan Duration => End - Start;

    /// <summary>
    /// Distance (in kilometers) covered over the ride.
    /// </summary>
    public double Distance { get; set; }

    /// <summary>
    /// Elevation gained during the ride (in meters).
    /// </summary>
    public double ElevationGain { get; set; }

    /// <summary>
    /// Elevation loss during the ride (in meters).
    /// </summary>
    public double ElevationLoss { get; set; }

    /// <summary>
    /// Number of fast accelerations during the ride.
    /// Fast accelerations are defined as accelerations greater than 2.0 m/s².
    /// </summary>
    public int FastAccelerationCount { get; set; }

    /// <summary>
    /// Number of fast decelerations during the ride.
    /// Fast decelerations are defined as decelerations greater than 2.0 m/s².
    /// </summary>
    public int FastDecelerationCount { get; set; }

    /// <summary>
    /// Set of track points recorded during the ride.
    /// </summary>
    public List<TrackPoint> TrackPoints { get; set; } = [];

    /// <summary>
    /// Represents the name of the file uploaded with extension.
    /// </summary>
    public string FullName { get; internal set; } = "";

    /// <summary>
    /// Moment when ride was created.
    /// </summary>
    public DateTime Created { get; internal set; }

    /// <summary>
    /// Score which indicates how smooth was the ride. Value between 0 (worst)
    /// to 100 (best).
    /// </summary>
    public int SmoothnessScore { get; internal set; }
}
