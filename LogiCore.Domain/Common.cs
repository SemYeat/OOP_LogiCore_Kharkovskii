namespace LogiCore.Domain;

public interface IEntity
{
    Guid Id { get; }
}

public interface IReadOnlyRepository<out T> where T : class, IEntity
{
    T? GetById(Guid id);
    IEnumerable<T> GetAll();
}

public interface IValidator<in T>
{
    ValidationResult Validate(T item);
}

public record ValidationResult(bool IsValid, string Error = "");

[Flags]
public enum TransportConditions
{
    None = 0,
    Refrigerated = 1,
    Sealed = 2,
    Pressurized = 4,
    LongRange = 8
}

public enum VehicleState { Free, InTransit, UnderMaintenance }
public enum OrderStatus { Created, Assigned, InTransit, Delivered, Cancelled }

public interface ITemperatureSensitive
{
    decimal RequiredTemperatureC { get; }
}

public interface IRefrigeratedTransport
{
    bool SupportsTemperature(decimal temperatureC);
}

public interface IInsurable
{
    decimal InsuranceValue { get; }
}

public interface IStackable
{
    bool CanBeStacked { get; }
}
