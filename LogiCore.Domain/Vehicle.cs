namespace LogiCore.Domain;

public abstract class Vehicle : IEntity
{
    protected Vehicle(string registrationNumber, decimal maxLoadKg, decimal maxVolumeM3,
        decimal averageSpeedKmH, decimal baseRatePerKm, TransportConditions conditions, Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(registrationNumber) || registrationNumber.Length < 4 || !registrationNumber.Contains('-'))
            throw new ArgumentException("Номер транспорта должен иметь формат XX-00.", nameof(registrationNumber));
        if (maxLoadKg <= 0 || maxVolumeM3 <= 0 || averageSpeedKmH <= 0 || baseRatePerKm < 0)
            throw new ArgumentException("Числовые параметры транспорта заданы неверно.");

        Id = id ?? Guid.NewGuid();
        RegistrationNumber = registrationNumber;
        MaxLoadKg = maxLoadKg;
        MaxVolumeM3 = maxVolumeM3;
        AverageSpeedKmH = averageSpeedKmH;
        BaseRatePerKm = baseRatePerKm;
        Conditions = conditions;
    }

    public Guid Id { get; }
    public string RegistrationNumber { get; }
    public decimal MaxLoadKg { get; }
    public decimal MaxVolumeM3 { get; }
    public decimal AverageSpeedKmH { get; }
    public decimal BaseRatePerKm { get; }
    public TransportConditions Conditions { get; }
    public VehicleState State { get; private set; } = VehicleState.Free;

    public virtual bool CanCarry(Cargo cargo) =>
        State == VehicleState.Free && cargo.WeightKg <= MaxLoadKg && cargo.VolumeM3 <= MaxVolumeM3;

    public abstract decimal CalculateDeliveryCost(Route route, IReadOnlyCollection<Cargo> cargo);

    public virtual bool CanTravel(Route route) => true;

    protected decimal BaseCost(Route route, IReadOnlyCollection<Cargo> cargo)
    {
        decimal weight = cargo.Sum(item => item.WeightKg);
        decimal volume = cargo.Sum(item => item.VolumeM3);
        if (weight > MaxLoadKg || volume > MaxVolumeM3)
            throw new VehicleOverloadException($"Транспорт {RegistrationNumber} перегружен.");
        return route.DistanceKm * BaseRatePerKm;
    }

    public void StartTrip() => State = VehicleState.InTransit;
    public void Release() => State = VehicleState.Free;
    internal void RestoreState(VehicleState state) => State = state;

    public override bool Equals(object? obj) => obj is Vehicle other && Id == other.Id;
    public override int GetHashCode() => Id.GetHashCode();
    public override string ToString() => $"{GetType().Name} {RegistrationNumber}";
}

public class Truck : Vehicle
{
    public Truck(string number) : base(number, 20_000, 90, 75, 2.2m, TransportConditions.Sealed) { }
    protected Truck(string number, decimal maxLoadKg, decimal maxVolumeM3, decimal speed,
        decimal rate, TransportConditions conditions, Guid? id = null)
        : base(number, maxLoadKg, maxVolumeM3, speed, rate, conditions, id) { }

    internal Truck(Guid id, string number) : base(number, 20_000, 90, 75, 2.2m, TransportConditions.Sealed, id) { }
    public override decimal CalculateDeliveryCost(Route route, IReadOnlyCollection<Cargo> cargo) => BaseCost(route, cargo) * 1.10m;
}

public sealed class RefrigeratorTruck : Truck, IRefrigeratedTransport
{
    public RefrigeratorTruck(string number, decimal minTemperatureC = -25, decimal maxTemperatureC = 10)
        : this(Guid.NewGuid(), number, minTemperatureC, maxTemperatureC) { }

    internal RefrigeratorTruck(Guid id, string number, decimal minTemperatureC, decimal maxTemperatureC)
        : base(number, 20_000, 90, 75, 2.2m,
            TransportConditions.Sealed | TransportConditions.Refrigerated, id)
    {
        if (minTemperatureC > maxTemperatureC)
            throw new ArgumentException("Минимальная температура не может быть выше максимальной.");
        MinTemperatureC = minTemperatureC;
        MaxTemperatureC = maxTemperatureC;
    }

    public decimal MinTemperatureC { get; }
    public decimal MaxTemperatureC { get; }
    public bool SupportsTemperature(decimal temperatureC) =>
        temperatureC >= MinTemperatureC && temperatureC <= MaxTemperatureC;

    public override bool CanCarry(Cargo cargo)
    {
        if (!base.CanCarry(cargo)) return false;
        if (cargo is not ITemperatureSensitive temperatureCargo) return true;
        return SupportsTemperature(temperatureCargo.RequiredTemperatureC);
    }

    public override decimal CalculateDeliveryCost(Route route, IReadOnlyCollection<Cargo> cargo) =>
        base.CalculateDeliveryCost(route, cargo) * 1.20m;
}

public sealed class CargoPlane : Vehicle
{
    public CargoPlane(string number) : base(number, 10_000, 500, 800, 12, TransportConditions.Pressurized | TransportConditions.LongRange) { }
    internal CargoPlane(Guid id, string number) : base(number, 10_000, 500, 800, 12, TransportConditions.Pressurized | TransportConditions.LongRange, id) { }
    public override bool CanCarry(Cargo cargo) => base.CanCarry(cargo) && cargo is not DangerousCargo { HazardClass: > 3 };
    public override decimal CalculateDeliveryCost(Route route, IReadOnlyCollection<Cargo> cargo) => BaseCost(route, cargo) + cargo.Sum(item => item.WeightKg) * 2;
}

public sealed class CargoShip : Vehicle
{
    public CargoShip(string number) : base(number, 1_000_000, 50_000, 35, 0.7m, TransportConditions.LongRange) { }
    internal CargoShip(Guid id, string number) : base(number, 1_000_000, 50_000, 35, 0.7m, TransportConditions.LongRange, id) { }
    public override decimal CalculateDeliveryCost(Route route, IReadOnlyCollection<Cargo> cargo) => BaseCost(route, cargo) + (cargo.Any(item => item is OversizedCargo) ? 5000 : 0);
}

public sealed class DroneCourier : Vehicle
{
    public DroneCourier(string number) : base(number, 20, 0.2m, 60, 4, TransportConditions.None) { }
    internal DroneCourier(Guid id, string number) : base(number, 20, 0.2m, 60, 4, TransportConditions.None, id) { }
    public override bool CanTravel(Route route) => route.DistanceKm <= 50;
    public override decimal CalculateDeliveryCost(Route route, IReadOnlyCollection<Cargo> cargo)
    {
        if (!CanTravel(route)) throw new IncompatibleCargoException("Дрон летает не дальше 50 км.");
        return BaseCost(route, cargo);
    }
}
