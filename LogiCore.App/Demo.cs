using LogiCore.Domain;

namespace LogiCore.App;

public sealed class ApplicationState
{
    public ApplicationState(Repository<Vehicle> vehicles, List<Customer> customers,
        Repository<Order> orders, DeliveryService service)
    {
        Vehicles = vehicles;
        Customers = customers;
        Orders = orders;
        Service = service;
    }

    public Repository<Vehicle> Vehicles { get; private set; }
    public List<Customer> Customers { get; private set; }
    public Repository<Order> Orders { get; private set; }
    public DeliveryService Service { get; private set; }

    public void Replace(RestoredSystemState restored)
    {
        Vehicles = restored.Vehicles;
        Customers = restored.Customers;
        Orders = restored.Orders;
        Service = restored.Service;
        Demo.SubscribeConsole(Service, new ConsoleNotifier());
    }
}

public static class Demo
{
    public static ApplicationState Run()
    {
        Console.WriteLine("=== LogiCore: автоматический демонстрационный сценарий ===");
        var vehicles = new Repository<Vehicle>();
        foreach (Vehicle vehicle in DemoFleetFactory.Create()) vehicles.Add(vehicle);

        var orders = new Repository<Order>();
        var customers = new List<Customer>
        {
            new Customer("Анна", "+7 900 111-11-11"),
            new Customer("Борис", "+7 900 222-22-22")
        };
        var validator = new CargoCompatibilityValidator(new CargoValidator());
        var service = new DeliveryService(vehicles, validator);
        var console = new ConsoleNotifier();
        SubscribeConsole(service, console);

        string logPath = Path.Combine(AppContext.BaseDirectory, "logs", "deliveries.log");
        using var logger = new FileLogger(logPath);
        SubscribeLogger(service, logger);

        try
        {
            Route route = new Route(new RoutePoint[]
            {
                new RoutePoint(55.7500, 37.6100),
                new RoutePoint(55.8000, 37.6500)
            });
            DateTime tomorrow = DateTime.Today.AddDays(1);
            var milk = new PerishableCargo("Молоко", 500, 3, 60_000, tomorrow, 4);
            Cargo[] cargo =
            {
                new StandardCargo("Книги", 100, 1, 20_000), new StandardCargo("Документы", 2, 0.02m, 5_000),
                milk, new PerishableCargo("Овощи", 300, 2, 30_000, tomorrow, 6),
                new FragileCargo("Стекло", 200, 2, 80_000, 1.5m), new FragileCargo("Мониторы", 150, 4, 150_000, 1.2m),
                new DangerousCargo("Краска", 100, 1, 10_000, 3), new DangerousCargo("Газ", 80, 1, 20_000, 2),
                new OversizedCargo("Станок", 5_000, 30, 500_000), new OversizedCargo("Трубы", 3_000, 20, 200_000)
            };

            Deliver(service, orders, customers[0], new Cargo[] { cargo[0], cargo[4] }, route,
                new StandardTariff(), addInsurance: true, addPacking: true, addPriority: true);
            Deliver(service, orders, customers[1], new Cargo[] { cargo[2] }, route, new ExpressTariff());
            Deliver(service, orders, customers[0], new Cargo[] { cargo[6] }, route, new StandardTariff());
            Deliver(service, orders, customers[1], new Cargo[] { cargo[8] }, route, new StandardTariff());
            Deliver(service, orders, customers[0], new Cargo[] { cargo[1] }, route, new ExpressTariff());
            ShowInvalidOrder(service, orders, customers[0], new Cargo[] { cargo[3], cargo[7] }, route);
            ShowOverload(service, orders, customers[1], route);
            ShowReports(orders, customers);
            ShowVariance(vehicles, milk);

            RestoredSystemState restored = ShowSerialization(vehicles, customers, orders, service.Revenue);
            SubscribeConsole(restored.Service, console);
            return new ApplicationState(restored.Vehicles, restored.Customers, restored.Orders, restored.Service);
        }
        finally
        {
            UnsubscribeLogger(service, logger);
        }
    }

    private static void Deliver(DeliveryService service, Repository<Order> orders, Customer customer,
        Cargo[] cargo, Route route, ITariffStrategy tariff, bool addInsurance = false,
        bool addPacking = false, bool addPriority = false)
    {
        Order order = service.CreateOrder(customer, cargo, route);
        orders.Add(order);
        service.AssignCheapest(order, tariff, addInsurance, addPacking, addPriority);
        Console.WriteLine("Расчёт цены: " + order.CostDescription);
        service.Start(order);
        service.Complete(order);
    }

    private static void ShowInvalidOrder(DeliveryService service, Repository<Order> orders,
        Customer customer, Cargo[] cargo, Route route)
    {
        try
        {
            Order order = service.CreateOrder(customer, cargo, route);
            orders.Add(order);
            service.AssignCheapest(order, new StandardTariff());
        }
        catch (IncompatibleCargoException exception) when (
            cargo.Any(item => item is DangerousCargo) && cargo.Any(item => item is PerishableCargo))
        {
            Console.WriteLine("Ожидаемая ошибка совместимости: " + exception.Message);
        }
    }

    private static void ShowOverload(DeliveryService service, Repository<Order> orders,
        Customer customer, Route route)
    {
        try
        {
            var huge = new OversizedCargo("Сверхтяжёлый генератор", 2_000_000, 60_000, 2_000_000);
            Order order = service.CreateOrder(customer, new Cargo[] { huge }, route);
            orders.Add(order);
            service.AssignCheapest(order, new StandardTariff());
        }
        catch (VehicleOverloadException exception)
        {
            Console.WriteLine("Ожидаемая перегрузка: " + exception.Message);
        }
    }

    public static void ShowReports(Repository<Order> orders, List<Customer> customers)
    {
        Console.WriteLine("\n--- 6 LINQ-отчётов ---");
        Print("Топ транспорта", Reports.TopVehicles(orders));
        Print("По статусам", Reports.OrdersByStatus(orders));
        Print("Средняя загрузка", Reports.AverageLoadByVehicleType(orders));
        Print("Клиенты выше порога", Reports.ExpensiveCustomers(customers,
            LogisticsSettings.Instance.ExpensiveOrderLimit));
        Print("Груз -> заказ -> клиент", Reports.CargoOrderCustomer(orders));
        Print("Опасные грузы", Reports.DangerousCargoCount(orders)
            .Select(item => $"Класс {item.Key}: {item.Value}"));
    }

    private static void Print(string title, IEnumerable<string> lines)
    {
        List<string> result = lines.ToList();
        Console.WriteLine(title + ":");
        Console.WriteLine(result.Count == 0 ? "нет данных" : result.ToReportTable());
    }

    public static RestoredSystemState SaveAndLoad(ApplicationState state, string path)
    {
        SystemSnapshot snapshot = StateStorage.CreateSnapshot(
            state.Vehicles, state.Customers, state.Orders, state.Service.Revenue);
        StateStorage.Save(path, snapshot);
        SystemSnapshot loadedSnapshot = StateStorage.Load(path);
        return StateStorage.Restore(loadedSnapshot);
    }

    private static RestoredSystemState ShowSerialization(Repository<Vehicle> vehicles,
        List<Customer> customers, Repository<Order> orders, decimal revenue)
    {
        var state = new ApplicationState(vehicles, customers, orders,
            new DeliveryService(vehicles, new CargoCompatibilityValidator(new CargoValidator()), revenue, orders));
        string path = Path.Combine(AppContext.BaseDirectory, "state.json");
        RestoredSystemState restored = SaveAndLoad(state, path);
        Console.WriteLine($"JSON: восстановлено {restored.Vehicles.Count()} ТС, " +
                          $"{restored.Customers.Count} клиентов и {restored.Orders.Count()} заказов.");
        Print("Повторный отчёт после загрузки", Reports.OrdersByStatus(restored.Orders));
        return restored;
    }

    private static void ShowVariance(Repository<Vehicle> vehicles, PerishableCargo milk)
    {
        IValidator<Cargo> cargoValidator = new CargoValidator();
        IValidator<PerishableCargo> perishableValidator = cargoValidator;

        var trucks = new Repository<Truck>();
        trucks.Add(new Truck("TR-03"));
        IReadOnlyRepository<Truck> truckReader = trucks;
        IReadOnlyRepository<Vehicle> vehicleReader = truckReader;

        Console.WriteLine($"Контравариантность: {perishableValidator.Validate(milk).IsValid}; " +
                          $"ковариантность: {vehicleReader.GetAll().Count()} ТС; парк: {vehicles.Count()}");
        TransportConditions conditions = TransportConditions.Refrigerated | TransportConditions.Sealed;
        Console.WriteLine($"Flags: {conditions}; охлаждение: {conditions.HasFlag(TransportConditions.Refrigerated)}");
    }

    public static void SubscribeConsole(DeliveryService service, ConsoleNotifier notifier)
    {
        service.OrderCreated += notifier.OnCreated;
        service.OrderStatusChanged += notifier.OnStatusChanged;
        service.VehicleOverloadAttempt += notifier.OnOverload;
        service.DeliveryCompleted += notifier.OnCompleted;
    }

    private static void SubscribeLogger(DeliveryService service, FileLogger logger)
    {
        service.OrderCreated += logger.OnCreated;
        service.OrderStatusChanged += logger.OnStatusChanged;
        service.VehicleOverloadAttempt += logger.OnOverload;
        service.DeliveryCompleted += logger.OnCompleted;
    }

    private static void UnsubscribeLogger(DeliveryService service, FileLogger logger)
    {
        service.OrderCreated -= logger.OnCreated;
        service.OrderStatusChanged -= logger.OnStatusChanged;
        service.VehicleOverloadAttempt -= logger.OnOverload;
        service.DeliveryCompleted -= logger.OnCompleted;
    }
}

public static class Menu
{
    public static void Run(ApplicationState state)
    {
        while (true)
        {
            Console.WriteLine("\n1 - создать простой заказ");
            Console.WriteLine("2 - показать отчёты");
            Console.WriteLine("3 - сохранить состояние");
            Console.WriteLine("4 - загрузить состояние");
            Console.WriteLine("0 - выход");
            Console.Write("Команда: ");
            string? command = Console.ReadLine();
            if (command == "0" || command is null) return;

            try
            {
                if (command == "1") CreateSimpleOrder(state);
                else if (command == "2") Demo.ShowReports(state.Orders, state.Customers);
                else if (command == "3") Save(state);
                else if (command == "4") Load(state);
                else Console.WriteLine("Неизвестная команда.");
            }
            catch (LogisticsException exception)
            {
                Console.WriteLine("Ошибка: " + exception.Message);
            }
        }
    }

    private static string StatePath => Path.Combine(AppContext.BaseDirectory, "state.json");

    private static void CreateSimpleOrder(ApplicationState state)
    {
        var cargo = new StandardCargo("Заказ из меню", 10, 0.1m, 1000);
        var route = new Route(new RoutePoint[] { new RoutePoint(55.75, 37.61), new RoutePoint(55.76, 37.62) });
        Order order = state.Service.CreateOrder(state.Customers[0], new Cargo[] { cargo }, route);
        state.Orders.Add(order);
        state.Service.AssignCheapest(order, new StandardTariff());
        state.Service.Start(order);
        state.Service.Complete(order);
        Console.WriteLine("Заказ создан и доставлен.");
    }

    private static void Save(ApplicationState state)
    {
        SystemSnapshot snapshot = StateStorage.CreateSnapshot(
            state.Vehicles, state.Customers, state.Orders, state.Service.Revenue);
        StateStorage.Save(StatePath, snapshot);
        Console.WriteLine("Состояние сохранено.");
    }

    private static void Load(ApplicationState state)
    {
        SystemSnapshot snapshot = StateStorage.Load(StatePath);
        state.Replace(StateStorage.Restore(snapshot));
        Console.WriteLine("Состояние загружено.");
    }
}
