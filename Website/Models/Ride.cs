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
  public double Distance => Geo.HaversineDistance(TrackPoints);

  /// <summary>
  /// Elevation gained during the ride (in meters).
  /// </summary>
  public double ElevationGain { get; set; }

  /// <summary>
  /// Elevation loss during the ride (in meters).
  /// </summary>
  public double ElevationLoss { get; set; }
  public int FastAccelerationCount { get; set; }
  public int FastDecelerationCount { get; set; }

  /// <summary>
  /// Number of track points record during the ride.
  /// </summary>
  public int TrackPointCount { get; set; }

  /// <summary>
  /// Set of track points recorded during the ride.
  /// </summary>
  public List<TrackPoint> TrackPoints { get; set; } = [];
}