namespace UserService.Application.UserProfiles;

public sealed class ProcessedMessageIds
{
    private const int Capacity = 10_000;

    private readonly object _lock = new();
    private readonly HashSet<Guid> _ids = new();
    private readonly Queue<Guid> _order = new();

    public bool Contains(Guid id)
    {
        lock (_lock)
        {
            return _ids.Contains(id);
        }
    }

    public void Add(Guid id)
    {
        lock (_lock)
        {
            if (!_ids.Add(id))
            {
                return;
            }

            _order.Enqueue(id);
            if (_order.Count > Capacity)
            {
                _ids.Remove(_order.Dequeue());
            }
        }
    }
}
