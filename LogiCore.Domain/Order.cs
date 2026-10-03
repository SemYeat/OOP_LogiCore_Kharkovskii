namespace LogiCore.Domain;

public sealed class Customer : IEntity
{
    private readonly List<Order> _orders = new();

    public Customer(string name, string contact) : this(Guid.NewGuid(), name, contact) { }

    internal Customer(Guid id, string name, string contact)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(contact))
            throw new ArgumentException("Имя и контакт клиента обязательны.");
        Id = id;
        Name = name;
        Contact = contact;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string Contact { get; }
    public IReadOnlyCollection<Order> Orders => _orders.AsReadOnly();
    internal void AddOrder(Order order) => _orders.Add(order);
}

public sealed class Order : IEntity
{
    private readonly List<Cargo> _cargo;

    public Order(Customer customer, IEnumerable<Cargo> cargo, Route route)
        : this(Guid.NewGuid(), customer, cargo, route) { }

    internal Order(Guid id, Customer customer, IEnumerable<Cargo> cargo, Route route)
    {
        Customer = customer;
        _cargo = cargo.ToList();
        if (_cargo.Count == 0) throw new CargoValidationException("В заказе нет грузов.");
        Route = route;
        Id = id;
        customer.AddOrder(this);
    }

    public Guid Id { get; }
    public Customer Customer { get; }
    public IReadOnlyCollection<Cargo> Cargo => _cargo.AsReadOnly();
    public Route Route { get; }
    public Vehicle? Vehicle { get; private set; }
    public decimal TotalCost { get; private set; }
    public string CostDescription { get; private set; } = "Цена ещё не рассчитана";
    public OrderStatus Status { get; private set; } = OrderStatus.Created;

    public void Assign(Vehicle vehicle, decimal cost, string costDescription = "Базовая доставка")
    {
        EnsureState(OrderStatus.Created);
        if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost), "Цена не может быть отрицательной.");
        Vehicle = vehicle;
        TotalCost = cost;
        CostDescription = costDescription;
        Status = OrderStatus.Assigned;
    }

    public void StartDelivery()
    {
        EnsureState(OrderStatus.Assigned);
        Vehicle!.StartTrip();
        Status = OrderStatus.InTransit;
    }

    public void Complete()
    {
        EnsureState(OrderStatus.InTransit);
        Vehicle!.Release();
        Status = OrderStatus.Delivered;
    }

    public void Cancel()
    {
        if (Status is not (OrderStatus.Created or OrderStatus.Assigned))
            throw new InvalidOrderStateException($"Заказ нельзя отменить из состояния {Status}.");
        Vehicle?.Release();
        Status = OrderStatus.Cancelled;
    }

    internal void Restore(Vehicle? vehicle, decimal cost, string costDescription, OrderStatus status)
    {
        Vehicle = vehicle;
        TotalCost = cost;
        CostDescription = costDescription;
        Status = status;
    }

    private void EnsureState(OrderStatus expected)
    {
        if (Status != expected)
            throw new InvalidOrderStateException($"Ожидалось состояние {expected}, текущее состояние {Status}.");
    }
}
