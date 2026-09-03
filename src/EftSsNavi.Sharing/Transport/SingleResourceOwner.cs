namespace EftSsNavi.Sharing.Transport;

internal sealed class SingleResourceOwner<T>(Action<T> discard)
    where T : class
{
    private readonly object _sync = new();
    private T? _current;
    private bool _sealed;

    public T? Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public bool TryAcquire(T candidate, Action<T> initialize)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(initialize);

        lock (_sync)
        {
            if (!_sealed && _current is null)
            {
                try
                {
                    initialize(candidate);
                    _current = candidate;
                    return true;
                }
                catch
                {
                    discard(candidate);
                    throw;
                }
            }
        }

        discard(candidate);
        return false;
    }

    public void ReleaseAndSeal(Action<T> uninitialize)
    {
        ArgumentNullException.ThrowIfNull(uninitialize);

        T? released;
        lock (_sync)
        {
            _sealed = true;
            released = _current;
            _current = null;
            if (released is not null)
            {
                uninitialize(released);
            }
        }

        if (released is not null)
        {
            discard(released);
        }
    }
}
