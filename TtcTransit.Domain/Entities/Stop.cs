namespace TtcTransit.Domain.Entities;

public sealed class Stop
{
    public string Id { get; }
    public string Name { get; }
    public double Latitude { get; }
    public double Longitude { get; }

    public Stop(string id, string name, double latitude, double longitude)
    {
        Id = id;
        Name = name;
        Latitude = latitude;
        Longitude = longitude;
    }
}