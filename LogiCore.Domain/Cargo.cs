namespace LogiCore.Domain;

public abstract class Cargo : IEntity, IInsurable
{
    protected Cargo(string description, decimal weightKg, decimal volumeM3, decimal declaredValue, Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new CargoValidationException("Описание груза не заполнено.");
        if (weightKg <= 0 || volumeM3 <= 0)
            throw new CargoValidationException("Вес и объём должны быть больше нуля.");
        if (declaredValue < 0)
            throw new CargoValidationException("Стоимость не может быть отрицательной.");

        Id = id ?? Guid.NewGuid();
        Description = description;
        WeightKg = weightKg;
        VolumeM3 = volumeM3;
        DeclaredValue = declaredValue;
    }

    public Guid Id { get; }
    public string Description { get; }
    public decimal WeightKg { get; }
    public decimal VolumeM3 { get; }
    public decimal DeclaredValue { get; }

    // Явная реализация: страховая стоимость видна только через контракт IInsurable.
    decimal IInsurable.InsuranceValue => DeclaredValue;

    public override string ToString() => $"{GetType().Name}: {Description}, {WeightKg} кг";
}

public sealed class StandardCargo : Cargo, IStackable
{
    public StandardCargo(string description, decimal weightKg, decimal volumeM3, decimal declaredValue)
        : base(description, weightKg, volumeM3, declaredValue) { }

    internal StandardCargo(Guid id, string description, decimal weightKg, decimal volumeM3, decimal declaredValue)
        : base(description, weightKg, volumeM3, declaredValue, id) { }

    public bool CanBeStacked => true;
}

public sealed class PerishableCargo : Cargo, ITemperatureSensitive
{
    public PerishableCargo(string description, decimal weightKg, decimal volumeM3,
        decimal declaredValue, DateTime expirationDate, decimal requiredTemperatureC)
        : this(Guid.NewGuid(), description, weightKg, volumeM3, declaredValue, expirationDate, requiredTemperatureC) { }

    internal PerishableCargo(Guid id, string description, decimal weightKg, decimal volumeM3,
        decimal declaredValue, DateTime expirationDate, decimal requiredTemperatureC)
        : base(description, weightKg, volumeM3, declaredValue, id)
    {
        ExpirationDate = expirationDate;
        RequiredTemperatureC = requiredTemperatureC;
    }

    public DateTime ExpirationDate { get; }
    public decimal RequiredTemperatureC { get; }
}

public sealed class FragileCargo : Cargo
{
    public FragileCargo(string description, decimal weightKg, decimal volumeM3,
        decimal declaredValue, decimal riskCoefficient)
        : this(Guid.NewGuid(), description, weightKg, volumeM3, declaredValue, riskCoefficient) { }

    internal FragileCargo(Guid id, string description, decimal weightKg, decimal volumeM3,
        decimal declaredValue, decimal riskCoefficient)
        : base(description, weightKg, volumeM3, declaredValue, id)
    {
        if (riskCoefficient < 1)
            throw new CargoValidationException("Коэффициент риска должен быть не меньше 1.");
        RiskCoefficient = riskCoefficient;
    }

    public decimal RiskCoefficient { get; }
}

public sealed class DangerousCargo : Cargo
{
    public DangerousCargo(string description, decimal weightKg, decimal volumeM3,
        decimal declaredValue, int hazardClass)
        : this(Guid.NewGuid(), description, weightKg, volumeM3, declaredValue, hazardClass) { }

    internal DangerousCargo(Guid id, string description, decimal weightKg, decimal volumeM3,
        decimal declaredValue, int hazardClass)
        : base(description, weightKg, volumeM3, declaredValue, id)
    {
        if (hazardClass is < 1 or > 9)
            throw new CargoValidationException("Класс опасности должен быть от 1 до 9.");
        HazardClass = hazardClass;
    }

    public int HazardClass { get; }
}

public sealed class OversizedCargo : Cargo
{
    public OversizedCargo(string description, decimal weightKg, decimal volumeM3, decimal declaredValue)
        : base(description, weightKg, volumeM3, declaredValue) { }

    internal OversizedCargo(Guid id, string description, decimal weightKg, decimal volumeM3, decimal declaredValue)
        : base(description, weightKg, volumeM3, declaredValue, id) { }
}
