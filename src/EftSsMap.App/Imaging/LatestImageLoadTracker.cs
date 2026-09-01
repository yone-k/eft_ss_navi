namespace EftSsMap.App.Imaging;

public sealed class LatestImageLoadTracker
{
    private long generation;
    private int closed;

    public long Begin() => Interlocked.Increment(ref generation);

    public bool IsCurrent(long candidateGeneration) =>
        Volatile.Read(ref closed) == 0
        && candidateGeneration == Interlocked.Read(ref generation);

    public void Invalidate() => Interlocked.Increment(ref generation);

    public void Close()
    {
        Interlocked.Exchange(ref closed, 1);
        Invalidate();
    }
}
