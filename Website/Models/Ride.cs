namespace Website.Models;

public class Ride
{
  public int Id { get; set; }
  public double MaxSpeed { get; set; }
  public double AvgSpeed { get; set; }
  public DateTime Start { get; set; }
  public DateTime End { get; set; }
  public TimeSpan Duration => End - Start;
  public double Distance { get; set; }

  public double ElevationGain { get; set; }
  public double ElevationLoss { get; set; }
  public int FastAccelerationCount { get; set; }
  public int FastDecelerationCount { get; set; }
  public int TrackPointCount {get;set;}

  public List<TrackPoint> TrackPoints { get; set; } = [];
}