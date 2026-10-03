namespace LogiCore.Domain;

public readonly struct RoutePoint
{
    public RoutePoint(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }
    public double Longitude { get; }

    public static double operator -(RoutePoint left, RoutePoint right)
    {
        double latitude = left.Latitude - right.Latitude;
        double longitude = left.Longitude - right.Longitude;
        return Math.Sqrt(latitude * latitude + longitude * longitude) * 111;
    }

    public static explicit operator string(RoutePoint point) => point.ToString();
    public override string ToString() => $"{Latitude:F4}; {Longitude:F4}";
}

public sealed class Route
{
    private readonly List<RoutePoint> _points;

    public Route(IEnumerable<RoutePoint> points)
    {
        _points = points.ToList();
        if (_points.Count < 2)
            throw new RouteNotFoundException("Маршрут должен содержать хотя бы две точки.");
    }

    public IReadOnlyCollection<RoutePoint> Points => _points.AsReadOnly();

    public decimal DistanceKm
    {
        get
        {
            double distance = 0;
            for (int i = 1; i < _points.Count; i++)
                distance += _points[i] - _points[i - 1];
            return (decimal)distance;
        }
    }

    public TimeSpan EstimateTime(Vehicle vehicle) =>
        TimeSpan.FromHours((double)(DistanceKm / vehicle.AverageSpeedKmH));
}
