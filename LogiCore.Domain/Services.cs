namespace LogiCore.Domain;

public sealed class CargoValidator : IValidator<Cargo>
{
    public ValidationResult Validate(Cargo item)
    {
        if (item is PerishableCargo perishable && perishable.ExpirationDate.Date < DateTime.Today)
            return new ValidationResult(false, "Срок годности груза истёк.");
        return new ValidationResult(true);
    }
}

public sealed class CargoCompatibilityValidator
{
    private readonly IValidator<Cargo> _cargoValidator;

    public CargoCompatibilityValidator(IValidator<Cargo> cargoValidator) => _cargoValidator = cargoValidator;

    public void Validate(Vehicle vehicle, IReadOnlyCollection<Cargo> cargo)
    {
        ValidateCargoSet(cargo);
        bool overload;
        if (CanTransport(vehicle, cargo, out overload)) return;
        if (overload)
            throw new VehicleOverloadException($"Превышены лимиты транспорта {vehicle.RegistrationNumber}.");
        throw new IncompatibleCargoException($"Транспорт {vehicle.RegistrationNumber} не подходит.");
    }

    public bool CanTransport(Vehicle vehicle, IReadOnlyCollection<Cargo> cargo, out bool overload)
    {
        decimal weight = cargo.Sum(item => item.WeightKg);
        decimal volume = cargo.Sum(item => item.VolumeM3);
        overload = weight > vehicle.MaxLoadKg || volume > vehicle.MaxVolumeM3;
        if (overload) return false;

        foreach (Cargo item in cargo)
        {
            if (item is ITemperatureSensitive temperatureCargo)
            {
                IRefrigeratedTransport? refrigerated = vehicle as IRefrigeratedTransport;
                if (refrigerated is null || !refrigerated.SupportsTemperature(temperatureCargo.RequiredTemperatureC))
                    return false;
            }

            if (!vehicle.CanCarry(item)) return false;
        }

        return true;
    }

    public void ValidateCargoSet(IReadOnlyCollection<Cargo> cargo)
    {
        foreach (Cargo item in cargo)
        {
            ValidationResult result = _cargoValidator.Validate(item);
            if (!result.IsValid) throw new CargoValidationException(result.Error);
        }

        if (cargo.Any(item => item is DangerousCargo) && cargo.Any(item => item is PerishableCargo))
            throw new IncompatibleCargoException("Опасный и скоропортящийся грузы несовместимы.");
    }
}

public sealed class OrderEventArgs : EventArgs
{
    public OrderEventArgs(Order order) => Order = order;
    public Order Order { get; }
}

public sealed class OrderStatusChangedEventArgs : EventArgs
{
    public OrderStatusChangedEventArgs(Order order, OrderStatus oldStatus) { Order = order; OldStatus = oldStatus; }
    public Order Order { get; }
    public OrderStatus OldStatus { get; }
}

public sealed class VehicleOverloadEventArgs : EventArgs
{
    public VehicleOverloadEventArgs(Order order) => Order = order;
    public Order Order { get; }
}

public delegate void OrderEventHandler(object sender, OrderEventArgs args);

public sealed class DeliveryService
{
    private readonly Repository<Vehicle> _vehicles;
    private readonly CargoCompatibilityValidator _validator;
    private readonly HashSet<Guid> _reservedVehicles = new();

    public DeliveryService(Repository<Vehicle> vehicles, CargoCompatibilityValidator validator,
        decimal initialRevenue = 0, IEnumerable<Order>? existingOrders = null)
    {
        _vehicles = vehicles;
        _validator = validator;
        Revenue = initialRevenue;
        if (existingOrders is not null)
        {
            foreach (Order order in existingOrders)
            {
                if (order.Vehicle is not null && order.Status is OrderStatus.Assigned or OrderStatus.InTransit)
                    _reservedVehicles.Add(order.Vehicle.Id);
            }
        }
    }

    public event OrderEventHandler? OrderCreated;
    public event EventHandler<OrderStatusChangedEventArgs>? OrderStatusChanged;
    public event EventHandler<VehicleOverloadEventArgs>? VehicleOverloadAttempt;
    public event OrderEventHandler? DeliveryCompleted;
    public decimal Revenue { get; private set; }

    public Order CreateOrder(Customer customer, IEnumerable<Cargo> cargo, Route route)
    {
        List<Cargo> cargoList = cargo.ToList();
        _validator.ValidateCargoSet(cargoList);
        var order = new Order(customer, cargoList, route);
        OrderCreated?.Invoke(this, new OrderEventArgs(order));
        return order;
    }

    public void AssignCheapest(Order order, ITariffStrategy tariff,
        bool addInsurance = false, bool addPacking = false, bool addPriority = false)
    {
        _validator.ValidateCargoSet(order.Cargo);
        Vehicle? bestVehicle = null;
        IDeliveryCost? bestPrice = null;
        bool overloadFound = false;

        foreach (Vehicle vehicle in _vehicles)
        {
            if (vehicle.State != VehicleState.Free || _reservedVehicles.Contains(vehicle.Id)) continue;
            if (!vehicle.CanTravel(order.Route)) continue;

            bool overload;
            if (!_validator.CanTransport(vehicle, order.Cargo, out overload))
            {
                overloadFound = overloadFound || overload;
                continue;
            }

            decimal transportCost = vehicle.CalculateDeliveryCost(order.Route, order.Cargo);
            decimal tariffCost = tariff.Calculate(transportCost);
            IDeliveryCost price = BuildPrice(order, tariffCost, addInsurance, addPacking, addPriority);
            if (bestPrice is null || price.Total < bestPrice.Total)
            {
                bestVehicle = vehicle;
                bestPrice = price;
            }
        }

        if (bestVehicle is null || bestPrice is null)
        {
            if (overloadFound) VehicleOverloadAttempt?.Invoke(this, new VehicleOverloadEventArgs(order));
            if (overloadFound) throw new VehicleOverloadException("Подходящий транспорт не найден из-за превышения лимитов.");
            throw new IncompatibleCargoException("Свободный подходящий транспорт не найден.");
        }

        OrderStatus oldStatus = order.Status;
        order.Assign(bestVehicle, bestPrice.Total, bestPrice.Describe());
        _reservedVehicles.Add(bestVehicle.Id);
        OrderStatusChanged?.Invoke(this, new OrderStatusChangedEventArgs(order, oldStatus));
    }

    public void Start(Order order)
    {
        OrderStatus oldStatus = order.Status;
        order.StartDelivery();
        OrderStatusChanged?.Invoke(this, new OrderStatusChangedEventArgs(order, oldStatus));
    }

    public void Complete(Order order)
    {
        OrderStatus oldStatus = order.Status;
        order.Complete();
        Revenue += order.TotalCost;
        if (order.Vehicle is not null) _reservedVehicles.Remove(order.Vehicle.Id);
        OrderStatusChanged?.Invoke(this, new OrderStatusChangedEventArgs(order, oldStatus));
        DeliveryCompleted?.Invoke(this, new OrderEventArgs(order));
    }

    public void Cancel(Order order)
    {
        OrderStatus oldStatus = order.Status;
        Guid? vehicleId = order.Vehicle?.Id;
        order.Cancel();
        if (vehicleId.HasValue) _reservedVehicles.Remove(vehicleId.Value);
        OrderStatusChanged?.Invoke(this, new OrderStatusChangedEventArgs(order, oldStatus));
    }

    private static IDeliveryCost BuildPrice(Order order, decimal tariffCost,
        bool addInsurance, bool addPacking, bool addPriority)
    {
        IDeliveryCost price = new BasicDeliveryCost(tariffCost);
        if (addInsurance)
        {
            decimal declaredValue = order.Cargo.Sum(item => item.DeclaredValue);
            price = new InsuranceDecorator(price, declaredValue);
        }
        if (addPacking) price = new PackingDecorator(price);
        if (addPriority) price = new PriorityDecorator(price);
        return price;
    }
}
