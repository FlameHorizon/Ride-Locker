using System.Xml;
using System.Xml.Serialization;

namespace Website.Models;

[XmlRoot(ElementName = "metadata")]
public class Metadata
{

    [XmlElement(ElementName = "name")]
    public string Name { get; set; } = "";
}

[XmlRoot(ElementName = "trkpt", Namespace = "http://www.topografix.com/GPX/1/1")]
public class Trkpt
{

    [XmlElement(ElementName = "ele")]
    public double Ele { get; set; }

    [XmlElement(ElementName = "time")]
    public DateTime Time { get; set; }

    [XmlElement(ElementName = "hdop")]
    public double Hdop { get; set; }

    [XmlElement(ElementName = "extensions")]
    public ExtensionsSpeed? Extensions { get; set; }

    [XmlAttribute(AttributeName = "lat")]
    public double Lat { get; set; }

    [XmlAttribute(AttributeName = "lon")]
    public double Lon { get; set; }
}

[XmlRoot(ElementName = "extensions", Namespace = "https://osmand.net/docs/technical/osmand-file-formats/osmand-gpx")]
public class ExtensionsSpeed
{
    /// <summary
    /// Speed in meters per second.
    /// </summary>
    [XmlElement(ElementName = "speed")]
    public double Speed { get; set; }
}

[XmlRoot(ElementName = "trkseg")]
public class Trkseg
{

    [XmlElement(ElementName = "trkpt")]
    public List<Trkpt> Trkpt { get; set; } = [];
}

[XmlRoot(ElementName = "trk")]
public class Trk
{

    // NOTE: I have not seen yet that a single track contains muplitple track segments.
    // Might not be the case.
    [XmlElement(ElementName = "trkseg")]
    public Trkseg Trkseg { get; set; } = new();
}

[XmlRoot("gpx", Namespace = "http://www.topografix.com/GPX/1/1")]
public class Gpx
{

    [XmlElement(ElementName = "metadata")]
    public Metadata Metadata { get; set; } = new();

    [XmlElement(ElementName = "trk")]
    public List<Trk> Trk { get; set; } = new();

    [XmlAttribute(AttributeName = "version")]
    public string Version { get; set; } = "";

    [XmlAttribute(AttributeName = "creator")]
    public string Creator { get; set; } = "";

    [XmlText]
    public string Text { get; set; } = "";

}

