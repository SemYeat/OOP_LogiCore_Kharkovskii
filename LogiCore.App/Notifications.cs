using LogiCore.Domain;

namespace LogiCore.App;

public sealed class ConsoleNotifier
{
    public void OnCreated(object sender, OrderEventArgs args) => Console.WriteLine($"[Событие] Создан заказ {args.Order.Id.ToString()[..8]}");
    public void OnStatusChanged(object? sender, OrderStatusChangedEventArgs args) => Console.WriteLine($"[Событие] {args.OldStatus} -> {args.Order.Status}");
    public void OnOverload(object? sender, VehicleOverloadEventArgs args) => Console.WriteLine($"[Событие] Попытка перегруза {args.Order.Id.ToString()[..8]}");
    public void OnCompleted(object sender, OrderEventArgs args) => Console.WriteLine($"[Событие] Доставка завершена, цена {args.Order.TotalCost:F2}");
}

public sealed class FileLogger : IDisposable
{
    private readonly StreamWriter _writer;

    public FileLogger(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Для журнала нужен путь с каталогом.", nameof(path));
        Directory.CreateDirectory(directory);
        _writer = new StreamWriter(path, append: false);
    }

    public void OnCreated(object sender, OrderEventArgs args) => Write($"Создан {args.Order.Id}");
    public void OnStatusChanged(object? sender, OrderStatusChangedEventArgs args) => Write($"{args.OldStatus} -> {args.Order.Status}");
    public void OnOverload(object? sender, VehicleOverloadEventArgs args) => Write($"Перегруз {args.Order.Id}");
    public void OnCompleted(object sender, OrderEventArgs args) => Write($"Завершён {args.Order.Id}");

    private void Write(string message)
    {
        _writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}");
        _writer.Flush();
    }

    public void Dispose() => _writer.Dispose();
}
