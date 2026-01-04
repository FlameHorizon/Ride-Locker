using Website.Components.Pages;
using Website.Models;

namespace Tests;

public class SummaryTests
{
    [Fact]
    public void DistanceOverMonths_IsEmpty_WhenNoTrackPointsAreGiven()
    {
        var rides = new List<Ride> {
            new Ride {
                TrackPoints = new List<TrackPoint>()
            }
        };

        var s = new Summary(rides);
        Assert.Empty(s.DistanceOverMonthsData);
    }

    [Fact]
    public void DistanceOverMonths_IsNotEmpty_WhenAtLeastOneTrackPointIsGiven()
    {
        var rides = new List<Ride> {
            new Ride {
                TrackPoints = new List<TrackPoint> {
                    new TrackPoint {
                        Time = DateTime.MinValue,
                        Latitude = 0.0,
                        Longitude = 0.0
                    },
                }
            }
        };

        var s = new Summary(rides);
        Assert.True(s.DistanceOverMonthsData.Any());
    }

    [Fact]
    public void DistanceOverMonths_IsOk_WhenMultipleTrackPointsWithinSameMonth()
    {
        var rides = new List<Ride> {
            new Ride {
                TrackPoints = new List<TrackPoint> {
                    new TrackPoint {
                        Time = DateTime.MinValue,
                        Latitude = 0.0,
                        Longitude = 0.0,
                    },
                    new TrackPoint {
                        Time = DateTime.MinValue,
                        Latitude = 1.0,
                        Longitude = 0.0
                    },
                    new TrackPoint {
                        Time = DateTime.MinValue,
                        Latitude = 1.0,
                        Longitude = 0.0
                    },
                    new TrackPoint {
                        Time = DateTime.MinValue,
                        Latitude = 2.0,
                        Longitude = 0.0
                    }
                },
            }
        };

        var s = new Summary(rides);

        Assert.Single(s.DistanceOverMonthsData);
        var first = s.DistanceOverMonthsData.First();
        Assert.Equal("01-01", first.XValue);
        Assert.Equal(111.19492664455873 * 2.0, first.YValue);
    }

    [Fact]
    public void DistanceOverMonths_IsOk_WhenForTwoDifferentRides()
    {
        var rides = new List<Ride> {
            new Ride {
                TrackPoints = new List<TrackPoint> {
                    new TrackPoint {
                        Time = DateTime.MinValue,
                        Latitude = 0.0,
                        Longitude = 0.0
                    },
                    new TrackPoint {
                        Time = DateTime.MinValue,
                        Latitude = 1.0,
                        Longitude = 0.0
                    },
                },
            },
            new Ride {
                TrackPoints = new List<TrackPoint> {
                    new TrackPoint {
                        Time = DateTime.MinValue,
                        Latitude = 2.0,
                        Longitude = 0.0
                    },
                    new TrackPoint {
                        Time = DateTime.MinValue,
                        Latitude = 3.0,
                        Longitude = 0.0
                    }
                }
            }
        };

        var s = new Summary(rides);

        Assert.Single(s.DistanceOverMonthsData);
        var first = s.DistanceOverMonthsData.First();
        Assert.Equal("01-01", first.XValue);
        Assert.Equal(111.19492664455873 * 2.0, first.YValue);
    }

    [Fact]
    public void DistanceOverMonths_IsOk_WhenMultipleTrackPointsWithinTwoDifferentMonths()
    {
        var rides = new List<Ride> {
            new Ride {
                TrackPoints = new List<TrackPoint> {
                    new TrackPoint {
                        Time = DateTime.MinValue,
                        Latitude = 0.0,
                        Longitude = 0.0
                    },
                    new TrackPoint {
                        Time = DateTime.MinValue,
                        Latitude = 1.0,
                        Longitude = 0.0
                    },
                },
                Start = DateTime.MinValue,
                End = DateTime.MinValue,
            },
            new Ride {
                TrackPoints = new List<TrackPoint> {
                    new TrackPoint {
                        Time = DateTime.MinValue.AddMonths(1),
                        Latitude = 2.0,
                        Longitude = 0.0
                    },
                    new TrackPoint {
                        Time = DateTime.MinValue.AddMonths(1),
                        Latitude = 3.0,
                        Longitude = 0.0
                    }
                },
                Start = DateTime.MinValue.AddMonths(1),
                End = DateTime.MinValue.AddMonths(1),
            }
        };

        var s = new Summary(rides);

        var first = s.DistanceOverMonthsData.First();
        Assert.Equal("01-01", first.XValue);
        Assert.Equal(111.19492664455873, first.YValue);

        var second = s.DistanceOverMonthsData.Skip(1).First();
        Assert.Equal("01-02", second.XValue);
        Assert.Equal(111.19492664455873, second.YValue);
    }

    [Fact]
    public void SpeedOverMonth_IsOk_WhenMultipleTrackPointsWithinTwoDifferentMonths()
    {
        var rides = new List<Ride> {
            new Ride {
                TrackPoints = new List<TrackPoint> {
                    new TrackPoint {
                        Latitude = 0.0,
                        Longitude = 0.0
                    },
                    new TrackPoint {
                        Latitude = 1.0,
                        Longitude = 0.0
                    },
                },
                Start = DateTime.MinValue,
                End = DateTime.MinValue.AddHours(1)
            },
            new Ride {
                TrackPoints = new List<TrackPoint> {
                    new TrackPoint {
                        Latitude = 0.0,
                        Longitude = 0.0
                    },
                    new TrackPoint {
                        Latitude = 2.0,
                        Longitude = 0.0
                    },
                },
                Start = DateTime.MinValue.AddMonths(1),
                End = DateTime.MinValue.AddMonths(1).AddHours(1)
            }
        };

        var s = new Summary(rides);

        var first = s.SpeedOverMonthsData.First();
        Assert.Equal("01-01", first.XValue);
        Assert.Equal(111.19492664455873, first.YValue * 3.6, 0.0001); // Converted into km/s

        var second = s.SpeedOverMonthsData.Skip(1).First();
        Assert.Equal("01-02", second.XValue);
        Assert.Equal(111.19492664455873 * 2, second.YValue * 3.6, 0.0001); // Converted into km/s
    }

    [Fact]
    public void SpeedDistributiuon_IsOk_WhenMultipleTrackPointsWithinTwoDifferentMonths()
    {
        var rides = new List<Ride> {
            new Ride {
                TrackPoints = new List<TrackPoint> {
                    new TrackPoint {
                        Speed = 1.0 // m/s
                    },
                    new TrackPoint {
                        Speed = 1.0 // m/s
                    },
                    new TrackPoint {
                        Speed = 2.0 // m/s
                    },
                    new TrackPoint {
                        Speed = 2.0 // m/s
                    }

                }
            }
        };

        var s = new Summary(rides);

        var item = s.SpeedDistributionData.First();
        Assert.Equal(3.6, item.XValue); // Converted into km/s

        item = s.SpeedDistributionData.Skip(1).First();
        Assert.Equal(3.6, item.XValue); // Converted into km/s

        item = s.SpeedDistributionData.Skip(2).First();
        Assert.Equal(2 * 3.6, item.XValue); // Converted into km/s

        item = s.SpeedDistributionData.Skip(3).First();
        Assert.Equal(2 * 3.6, item.XValue); // Converted into km/s
    }
}
