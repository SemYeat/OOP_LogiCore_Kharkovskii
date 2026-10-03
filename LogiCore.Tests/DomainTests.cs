using LogiCore.Domain;

namespace LogiCore.Tests;

public sealed class DomainTests
{
    private static Route ShortRoute() => new Route(new[]
    {
        new RoutePoint(0, 0),
        new RoutePoint(0, 0.1)
    });

    private static StandardCargo SmallCargo() => new("Коробка", 1, 0.1m, 1000);

    [Fact]
    public void CargoRejectsZeroWeight() =>
        Assert.Throws<CargoValidationException>(() => new StandardCargo("A", 0, 1, 1));

    [Fact]
    public void DangerousCargoRejectsWrongClass() =>
        Assert.Throws<CargoValidationException>(() => new DangerousCargo("A", 1, 1, 1, 10));

    [Fact]
    public void VehicleRejectsWrongRegistrationNumber() =>
        Assert.Throws<ArgumentException>(() => new Truck("T"));

    [Fact]
    public void RefrigeratorRejectsWrongTemperatureRange() =>
        Assert.Throws<ArgumentException>(() => new RefrigeratorTruck("RF-01", 10, -10));

    [Fact]
    public void RoutePointOperatorReturnsDistance() =>
        Assert.True(new RoutePoint(0, 1) - new RoutePoint(0, 0) > 100);

    [Fact]
    public void RouteNeedsTwoPoints() =>
        Assert.Throws<RouteNotFoundException>(() => new Route(new[] { new RoutePoint(0, 0) }));

    [Fact]
    public void TruckCalculatesCost() =>
        Assert.True(new Truck("TR-01").CalculateDeliveryCost(ShortRoute(), new Cargo[] { SmallCargo() }) > 0);

    [Fact]
    public void RefrigeratorCalculatesCost() =>
        Assert.True(new RefrigeratorTruck("RF-01").CalculateDeliveryCost(ShortRoute(), new Cargo[] { SmallCargo() }) > 0);

    [Fact]
    public void PlaneCalculatesCost() =>
        Assert.True(new CargoPlane("PL-01").CalculateDeliveryCost(ShortRoute(), new Cargo[] { SmallCargo() }) > 0);

    [Fact]
    public void ShipCalculatesCost() =>
        Assert.True(new CargoShip("SH-01").CalculateDeliveryCost(ShortRoute(), new Cargo[] { SmallCargo() }) > 0);

    [Fact]
    public void DroneCalculatesCost() =>
        Assert.True(new DroneCourier("DR-01").CalculateDeliveryCost(ShortRoute(), new Cargo[] { SmallCargo() }) > 0);

    [Fact]
    public void TruckAcceptsCargoAtWeightLimit()
    {
        var cargo = new StandardCargo("Предел", 20_000, 1, 1);
        Assert.True(new Truck("TR-01").CanCarry(cargo));
    }

    [Fact]
    public void RefrigeratorChecksTemperature()
    {
        var cargo = new PerishableCargo("A", 1, 1, 1, DateTime.Today.AddDays(1), 20);
        Assert.False(new RefrigeratorTruck("RF-01").CanCarry(cargo));
    }

    [Fact]
    public void PlaneRejectsHighHazardClass() =>
        Assert.False(new CargoPlane("PL-01").CanCarry(new DangerousCargo("A", 1, 1, 1, 5)));

    [Fact]
    public void ValidatorRejectsDangerousWithPerishable()
    {
        Cargo[] cargo =
        {
            new DangerousCargo("D", 1, 1, 1, 1),
            new PerishableCargo("P", 1, 1, 1, DateTime.Today.AddDays(1), 2)
        };
        var validator = new CargoCompatibilityValidator(new CargoValidator());
        Assert.Throws<IncompatibleCargoException>(() => validator.Validate(new RefrigeratorTruck("RF-01"), cargo));
    }

    [Fact]
    public void ValidatorRejectsExpiredCargo()
    {
        Cargo cargo = new PerishableCargo("P", 1, 1, 1, DateTime.Today.AddDays(-1), 2);
        Assert.False(new CargoValidator().Validate(cargo).IsValid);
    }

    [Fact]
    public void ValidatorRejectsPerishableWithoutCooling()
    {
        Cargo[] cargo = { new PerishableCargo("P", 1, 1, 1, DateTime.Today.AddDays(1), 2) };
        var validator = new CargoCompatibilityValidator(new CargoValidator());
        Assert.Throws<IncompatibleCargoException>(() => validator.Validate(new Truck("TR-01"), cargo));
    }

    [Fact]
    public void ValidatorChecksTotalWeight()
    {
        Cargo[] cargo = { new StandardCargo("A", 15_000, 1, 1), new StandardCargo("B", 15_000, 1, 1) };
        var validator = new CargoCompatibilityValidator(new CargoValidator());
        Assert.Throws<VehicleOverloadException>(() => validator.Validate(new Truck("TR-01"), cargo));
    }

    [Fact]
    public void OrderPassesFullStateMachine()
    {
        var order = new Order(new Customer("A", "1"), new Cargo[] { SmallCargo() }, ShortRoute());
        order.Assign(new Truck("TR-01"), 100);
        order.StartDelivery();
        order.Complete();
        Assert.Equal(OrderStatus.Delivered, order.Status);
    }

    [Fact]
    public void OrderRejectsWrongTransition()
    {
        var order = new Order(new Customer("A", "1"), new Cargo[] { SmallCargo() }, ShortRoute());
        Assert.Throws<InvalidOrderStateException>(order.StartDelivery);
    }

    [Fact]
    public void OrderCanBeCancelledBeforeStart()
    {
        var order = new Order(new Customer("A", "1"), new Cargo[] { SmallCargo() }, ShortRoute());
        order.Cancel();
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void RepositoryAddsFindsIteratesAndRemoves()
    {
        var repository = new Repository<Customer>();
        var customer = new Customer("A", "1");
        repository.Add(customer);
        Assert.Same(customer, repository[customer.Id]);
        Assert.Single(repository.FindAll(item => item.Name == "A"));
        Assert.Single(repository.ToList());
        Assert.True(repository.Remove(customer));
        Assert.Null(repository[customer.Id]);
    }

    [Fact]
    public void DeliveryServiceDoesNotAssignOneVehicleTwice()
    {
        var vehicles = new Repository<Vehicle>();
        vehicles.Add(new Truck("TR-01"));
        var service = new DeliveryService(vehicles, new CargoCompatibilityValidator(new CargoValidator()));
        var customer = new Customer("A", "1");
        Order first = service.CreateOrder(customer, new Cargo[] { SmallCargo() }, ShortRoute());
        Order second = service.CreateOrder(customer, new Cargo[] { SmallCargo() }, ShortRoute());

        service.AssignCheapest(first, new StandardTariff());

        Assert.Throws<IncompatibleCargoException>(() => service.AssignCheapest(second, new StandardTariff()));
    }

    [Fact]
    public void DeliveryServicesAreIncludedInOrderCost()
    {
        var vehicles = new Repository<Vehicle>();
        vehicles.Add(new Truck("TR-01"));
        var service = new DeliveryService(vehicles, new CargoCompatibilityValidator(new CargoValidator()));
        Order order = service.CreateOrder(new Customer("A", "1"), new Cargo[] { SmallCargo() }, ShortRoute());

        service.AssignCheapest(order, new StandardTariff(), addInsurance: true, addPacking: true);

        Assert.Contains("страховка", order.CostDescription);
        Assert.Contains("упаковка", order.CostDescription);
        Assert.True(order.TotalCost > 500);
    }

    [Fact]
    public void DecoratorOrderChangesResult()
    {
        IDeliveryCost insuranceThenPriority = new PriorityDecorator(
            new InsuranceDecorator(new BasicDeliveryCost(1000), 10_000));
        IDeliveryCost priorityThenInsurance = new InsuranceDecorator(
            new PriorityDecorator(new BasicDeliveryCost(1000)), 10_000);

        Assert.NotEqual(insuranceThenPriority.Total, priorityThenInsurance.Total);
    }

    [Fact]
    public void SerializationRestoresTypesIdsAndRelations()
    {
        string path = Path.Combine(Path.GetTempPath(), $"logicore-{Guid.NewGuid()}.json");
        try
        {
            var vehicles = new Repository<Vehicle>();
            var truck = new Truck("TR-01");
            vehicles.Add(truck);
            var customers = new List<Customer> { new Customer("A", "1") };
            var orders = new Repository<Order>();
            Order order = new Order(customers[0], new Cargo[] { SmallCargo() }, ShortRoute());
            order.Assign(truck, 123, "Проверочная цена");
            orders.Add(order);
            SystemSnapshot snapshot = StateStorage.CreateSnapshot(vehicles, customers, orders, 55);
            StateStorage.Save(path, snapshot);

            RestoredSystemState restored = StateStorage.Restore(StateStorage.Load(path));

            Order restoredOrder = Assert.Single(restored.Orders);
            Assert.Equal(order.Id, restoredOrder.Id);
            Assert.Equal(customers[0].Id, restoredOrder.Customer.Id);
            Assert.IsType<StandardCargo>(Assert.Single(restoredOrder.Cargo));
            Assert.Equal(truck.Id, restoredOrder.Vehicle?.Id);
            Assert.Equal(55, restored.Service.Revenue);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".errors.log")) File.Delete(path + ".errors.log");
        }
    }

    [Fact]
    public void FlagsCanBeCombined()
    {
        TransportConditions value = TransportConditions.Refrigerated | TransportConditions.Sealed;
        Assert.True(value.HasFlag(TransportConditions.Refrigerated));
    }
}
