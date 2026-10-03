using System.Collections;

namespace LogiCore.Domain;

public sealed class Repository<T> : IReadOnlyRepository<T>, IEnumerable<T> where T : class, IEntity
{
    private readonly List<T> _items = new();

    public T? this[Guid id] => GetById(id);

    public void Add(T item)
    {
        if (GetById(item.Id) is not null) throw new ArgumentException("Элемент уже добавлен.");
        _items.Add(item);
    }

    public bool Remove(T item) => _items.Remove(item);
    public IReadOnlyCollection<T> FindAll(Predicate<T> predicate) => _items.FindAll(predicate).AsReadOnly();
    public T? GetById(Guid id) => _items.FirstOrDefault(item => item.Id == id);
    public IEnumerable<T> GetAll() => this;

    public IEnumerator<T> GetEnumerator()
    {
        foreach (T item in _items) yield return item;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public static class ReportExtensions
{
    public static string ToReportTable<T>(this IEnumerable<T> items) => string.Join(Environment.NewLine, items);
}
