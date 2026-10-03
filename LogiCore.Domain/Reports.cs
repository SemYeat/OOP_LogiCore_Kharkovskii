namespace LogiCore.Domain;

public static class Reports
{
    public static IEnumerable<string> TopVehicles(IEnumerable<Order> orders) => orders
        .Where(order => order.Status == OrderStatus.Delivered && order.Vehicle is not null)
        .GroupBy(order => order.Vehicle!.RegistrationNumber)
        .Select(group => new { Vehicle = group.Key, Revenue = group.Sum(order => order.TotalCost) })
        .OrderByDescending(item => item.Revenue)
        .Take(3)
        .Select(item => $"{item.Vehicle}: {item.Revenue:F2}");

    public static IEnumerable<string> OrdersByStatus(IEnumerable<Order> orders) => orders
        .GroupBy(order => order.Status)
        .Select(group => $"{group.Key}: {group.Count()} заказов, {group.Sum(order => order.TotalCost):F2}");

    public static IEnumerable<string> AverageLoadByVehicleType(IEnumerable<Order> orders) => orders
        .Where(order => order.Vehicle is not null)
        .GroupBy(order => order.Vehicle!.GetType().Name)
        .Select(group => $"{group.Key}: {group.Average(order => order.Cargo.Sum(cargo => cargo.WeightKg) / order.Vehicle!.MaxLoadKg * 100):F1}%");

    public static IEnumerable<string> ExpensiveCustomers(IEnumerable<Customer> customers, decimal limit) =>
        customers.Select(customer => new { customer.Name, Total = customer.Orders.Sum(order => order.TotalCost) })
            .Where(customer => customer.Total > limit)
            .Select(customer => $"{customer.Name}: {customer.Total:F2}");

    public static IEnumerable<string> CargoOrderCustomer(IEnumerable<Order> orders)
    {
        var cargoRows = orders.SelectMany(order => order.Cargo.Select(cargo => new
        {
            Cargo = cargo,
            OrderId = order.Id
        }));

        var orderRows = orders.Select(order => new
        {
            OrderId = order.Id,
            CustomerName = order.Customer.Name
        });

        var query = from cargoRow in cargoRows
                    join orderRow in orderRows on cargoRow.OrderId equals orderRow.OrderId
                    select $"{cargoRow.Cargo.Description} -> {orderRow.OrderId} -> {orderRow.CustomerName}";
        return query;
    }

    public static Dictionary<int, int> DangerousCargoCount(IEnumerable<Order> orders) => orders
        .SelectMany(order => order.Cargo)
        .OfType<DangerousCargo>()
        .GroupBy(cargo => cargo.HazardClass)
        .ToDictionary(group => group.Key, group => group.Count());

    public static ILookup<OrderStatus, Order> OrderLookup(IEnumerable<Order> orders) =>
        orders.ToLookup(order => order.Status);
}
