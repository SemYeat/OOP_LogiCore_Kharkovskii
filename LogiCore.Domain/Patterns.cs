namespace LogiCore.Domain;

public interface ITariffStrategy
{
    string Name { get; }
    decimal Calculate(decimal baseCost);
}

public sealed class StandardTariff : ITariffStrategy
{
    public string Name => "Стандартный";
    public decimal Calculate(decimal baseCost) => baseCost;
}

public sealed class ExpressTariff : ITariffStrategy
{
    public string Name => "Срочный";
    public decimal Calculate(decimal baseCost) => baseCost * 1.5m;
}

public interface IDeliveryCost
{
    decimal Total { get; }
    string Describe();
}

public sealed class BasicDeliveryCost : IDeliveryCost
{
    public BasicDeliveryCost(decimal total) => Total = total;
    public decimal Total { get; }
    public string Describe() => $"Базовая доставка: {Total:F2}";
}

public abstract class DeliveryCostDecorator : IDeliveryCost
{
    protected DeliveryCostDecorator(IDeliveryCost inner) => Inner = inner;
    protected IDeliveryCost Inner { get; }
    public abstract decimal Total { get; }
    public abstract string Describe();
}

public sealed class InsuranceDecorator : DeliveryCostDecorator
{
    private readonly decimal _declaredValue;
    public InsuranceDecorator(IDeliveryCost inner, decimal declaredValue) : base(inner) => _declaredValue = declaredValue;
    public override decimal Total => Inner.Total + _declaredValue * 0.01m;
    public override string Describe() => Inner.Describe() + $" -> страховка: {Total:F2}";
}

public sealed class PackingDecorator : DeliveryCostDecorator
{
    public PackingDecorator(IDeliveryCost inner) : base(inner) { }
    public override decimal Total => Inner.Total + 500;
    public override string Describe() => Inner.Describe() + $" -> упаковка: {Total:F2}";
}

public sealed class PriorityDecorator : DeliveryCostDecorator
{
    private const decimal PriorityMultiplier = 1.20m;

    public PriorityDecorator(IDeliveryCost inner) : base(inner) { }
    public override decimal Total => Inner.Total * PriorityMultiplier;
    public override string Describe() => Inner.Describe() + $" -> срочность: {Total:F2}";
}

public sealed class LogisticsSettings
{
    private static readonly Lazy<LogisticsSettings> LazyInstance = new(() => new LogisticsSettings());
    private LogisticsSettings() { }
    public static LogisticsSettings Instance => LazyInstance.Value;
    public decimal ExpensiveOrderLimit { get; } = 10;
}

public abstract class VehicleFactory
{
    public abstract Vehicle Create(string registrationNumber);
}

public sealed class TruckFactory : VehicleFactory
{
    public override Vehicle Create(string registrationNumber) => new Truck(registrationNumber);
}

public sealed class RefrigeratorFactory : VehicleFactory
{
    public override Vehicle Create(string registrationNumber) => new RefrigeratorTruck(registrationNumber);
}

public sealed class PlaneFactory : VehicleFactory
{
    public override Vehicle Create(string registrationNumber) => new CargoPlane(registrationNumber);
}

public sealed class ShipFactory : VehicleFactory
{
    public override Vehicle Create(string registrationNumber) => new CargoShip(registrationNumber);
}

public sealed class DroneFactory : VehicleFactory
{
    public override Vehicle Create(string registrationNumber) => new DroneCourier(registrationNumber);
}

public static class DemoFleetFactory
{
    public static IReadOnlyCollection<Vehicle> Create()
    {
        return new List<Vehicle>
        {
            new TruckFactory().Create("TR-01"),
            new TruckFactory().Create("TR-02"),
            new RefrigeratorFactory().Create("RF-01"),
            new PlaneFactory().Create("PL-01"),
            new ShipFactory().Create("SH-01"),
            new DroneFactory().Create("DR-01")
        }.AsReadOnly();
    }
}
