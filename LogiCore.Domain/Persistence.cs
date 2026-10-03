using System.Text.Json;

namespace LogiCore.Domain;

public sealed class VehicleSnapshot
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string RegistrationNumber { get; set; } = "";
    public VehicleState State { get; set; }
    public decimal? MinTemperatureC { get; set; }
    public decimal? MaxTemperatureC { get; set; }
}

public sealed class CargoSnapshot
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal WeightKg { get; set; }
    public decimal VolumeM3 { get; set; }
    public decimal DeclaredValue { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public decimal? RequiredTemperatureC { get; set; }
    public decimal? RiskCoefficient { get; set; }
    public int? HazardClass { get; set; }
}

public sealed class CustomerSnapshot
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Contact { get; set; } = "";
}

public sealed class RoutePointSnapshot
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public sealed class OrderSnapshot
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public List<Guid> CargoIds { get; set; } = new();
    public List<RoutePointSnapshot> RoutePoints { get; set; } = new();
    public Guid? VehicleId { get; set; }
    public decimal TotalCost { get; set; }
    public string CostDescription { get; set; } = "";
    public OrderStatus Status { get; set; }
}

public sealed class SystemSnapshot
{
    public List<VehicleSnapshot> Vehicles { get; set; } = new();
    public List<CargoSnapshot> Cargo { get; set; } = new();
    public List<CustomerSnapshot> Customers { get; set; } = new();
    public List<OrderSnapshot> Orders { get; set; } = new();
    public decimal Revenue { get; set; }
}

public sealed class RestoredSystemState
{
    public RestoredSystemState(Repository<Vehicle> vehicles, List<Customer> customers,
        Repository<Order> orders, DeliveryService service)
    {
        Vehicles = vehicles;
        Customers = customers;
        Orders = orders;
        Service = service;
    }

    public Repository<Vehicle> Vehicles { get; }
    public List<Customer> Customers { get; }
    public Repository<Order> Orders { get; }
    public DeliveryService Service { get; }
}

public static class StateStorage
{
    public static SystemSnapshot CreateSnapshot(IEnumerable<Vehicle> vehicles,
        IEnumerable<Customer> customers, IEnumerable<Order> orders, decimal revenue)
    {
        List<Order> orderList = orders.ToList();
        List<Cargo> cargoList = orderList.SelectMany(order => order.Cargo)
            .GroupBy(cargo => cargo.Id)
            .Select(group => group.First())
            .ToList();

        return new SystemSnapshot
        {
            Vehicles = vehicles.Select(ToSnapshot).ToList(),
            Cargo = cargoList.Select(ToSnapshot).ToList(),
            Customers = customers.Select(customer => new CustomerSnapshot
            {
                Id = customer.Id,
                Name = customer.Name,
                Contact = customer.Contact
            }).ToList(),
            Orders = orderList.Select(ToSnapshot).ToList(),
            Revenue = revenue
        };
    }

    public static void Save(string path, SystemSnapshot snapshot)
    {
        string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static SystemSnapshot Load(string path)
    {
        try
        {
            return ReadSnapshot(path);
        }
        catch (LogisticsException exception)
        {
            File.AppendAllText(path + ".errors.log", DateTime.Now + " " + exception.Message + Environment.NewLine);
            throw; // Повторный проброс сохраняет исходный стек вызовов.
        }
    }

    public static RestoredSystemState Restore(SystemSnapshot snapshot)
    {
        var vehicles = new Repository<Vehicle>();
        var vehicleById = new Dictionary<Guid, Vehicle>();
        foreach (VehicleSnapshot data in snapshot.Vehicles)
        {
            Vehicle vehicle = RestoreVehicle(data);
            vehicle.RestoreState(data.State);
            vehicles.Add(vehicle);
            vehicleById.Add(vehicle.Id, vehicle);
        }

        var cargoById = new Dictionary<Guid, Cargo>();
        foreach (CargoSnapshot data in snapshot.Cargo)
        {
            Cargo cargo = RestoreCargo(data);
            cargoById.Add(cargo.Id, cargo);
        }

        var customers = new List<Customer>();
        var customerById = new Dictionary<Guid, Customer>();
        foreach (CustomerSnapshot data in snapshot.Customers)
        {
            var customer = new Customer(data.Id, data.Name, data.Contact);
            customers.Add(customer);
            customerById.Add(customer.Id, customer);
        }

        var orders = new Repository<Order>();
        foreach (OrderSnapshot data in snapshot.Orders)
        {
            Customer customer = customerById[data.CustomerId];
            List<Cargo> orderCargo = data.CargoIds.Select(id => cargoById[id]).ToList();
            Route route = new Route(data.RoutePoints.Select(point => new RoutePoint(point.Latitude, point.Longitude)));
            var order = new Order(data.Id, customer, orderCargo, route);
            Vehicle? vehicle = data.VehicleId.HasValue ? vehicleById[data.VehicleId.Value] : null;
            order.Restore(vehicle, data.TotalCost, data.CostDescription, data.Status);
            orders.Add(order);
        }

        var validator = new CargoCompatibilityValidator(new CargoValidator());
        var service = new DeliveryService(vehicles, validator, snapshot.Revenue, orders);
        return new RestoredSystemState(vehicles, customers, orders, service);
    }

    private static SystemSnapshot ReadSnapshot(string path)
    {
        if (!File.Exists(path)) throw new LogisticsException("Файл состояния не найден.");
        try
        {
            string json = File.ReadAllText(path);
            SystemSnapshot? snapshot = JsonSerializer.Deserialize<SystemSnapshot>(json);
            if (snapshot is null) throw new LogisticsException("Файл состояния пуст.");
            return snapshot;
        }
        catch (JsonException)
        {
            throw new LogisticsException("Файл состояния повреждён.");
        }
    }

    private static VehicleSnapshot ToSnapshot(Vehicle vehicle)
    {
        var data = new VehicleSnapshot
        {
            Id = vehicle.Id,
            Type = vehicle.GetType().Name,
            RegistrationNumber = vehicle.RegistrationNumber,
            State = vehicle.State
        };
        if (vehicle is RefrigeratorTruck refrigerator)
        {
            data.MinTemperatureC = refrigerator.MinTemperatureC;
            data.MaxTemperatureC = refrigerator.MaxTemperatureC;
        }
        return data;
    }

    private static CargoSnapshot ToSnapshot(Cargo cargo)
    {
        var data = new CargoSnapshot
        {
            Id = cargo.Id,
            Type = cargo.GetType().Name,
            Description = cargo.Description,
            WeightKg = cargo.WeightKg,
            VolumeM3 = cargo.VolumeM3,
            DeclaredValue = cargo.DeclaredValue
        };
        if (cargo is PerishableCargo perishable)
        {
            data.ExpirationDate = perishable.ExpirationDate;
            data.RequiredTemperatureC = perishable.RequiredTemperatureC;
        }
        if (cargo is FragileCargo fragile) data.RiskCoefficient = fragile.RiskCoefficient;
        if (cargo is DangerousCargo dangerous) data.HazardClass = dangerous.HazardClass;
        return data;
    }

    private static OrderSnapshot ToSnapshot(Order order)
    {
        return new OrderSnapshot
        {
            Id = order.Id,
            CustomerId = order.Customer.Id,
            CargoIds = order.Cargo.Select(cargo => cargo.Id).ToList(),
            RoutePoints = order.Route.Points.Select(point => new RoutePointSnapshot
            {
                Latitude = point.Latitude,
                Longitude = point.Longitude
            }).ToList(),
            VehicleId = order.Vehicle?.Id,
            TotalCost = order.TotalCost,
            CostDescription = order.CostDescription,
            Status = order.Status
        };
    }

    private static Vehicle RestoreVehicle(VehicleSnapshot data)
    {
        if (data.Type == nameof(Truck)) return new Truck(data.Id, data.RegistrationNumber);
        if (data.Type == nameof(RefrigeratorTruck))
            return new RefrigeratorTruck(data.Id, data.RegistrationNumber,
                data.MinTemperatureC ?? -25, data.MaxTemperatureC ?? 10);
        if (data.Type == nameof(CargoPlane)) return new CargoPlane(data.Id, data.RegistrationNumber);
        if (data.Type == nameof(CargoShip)) return new CargoShip(data.Id, data.RegistrationNumber);
        if (data.Type == nameof(DroneCourier)) return new DroneCourier(data.Id, data.RegistrationNumber);
        throw new LogisticsException("Неизвестный тип транспорта: " + data.Type);
    }

    private static Cargo RestoreCargo(CargoSnapshot data)
    {
        if (data.Type == nameof(StandardCargo))
            return new StandardCargo(data.Id, data.Description, data.WeightKg, data.VolumeM3, data.DeclaredValue);
        if (data.Type == nameof(PerishableCargo))
            return new PerishableCargo(data.Id, data.Description, data.WeightKg, data.VolumeM3,
                data.DeclaredValue, data.ExpirationDate ?? DateTime.Today, data.RequiredTemperatureC ?? 0);
        if (data.Type == nameof(FragileCargo))
            return new FragileCargo(data.Id, data.Description, data.WeightKg, data.VolumeM3,
                data.DeclaredValue, data.RiskCoefficient ?? 1);
        if (data.Type == nameof(DangerousCargo))
            return new DangerousCargo(data.Id, data.Description, data.WeightKg, data.VolumeM3,
                data.DeclaredValue, data.HazardClass ?? 1);
        if (data.Type == nameof(OversizedCargo))
            return new OversizedCargo(data.Id, data.Description, data.WeightKg, data.VolumeM3, data.DeclaredValue);
        throw new LogisticsException("Неизвестный тип груза: " + data.Type);
    }
}
