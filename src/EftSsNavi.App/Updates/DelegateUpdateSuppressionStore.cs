namespace EftSsNavi.App.Updates;

public sealed class DelegateUpdateSuppressionStore : IUpdateSuppressionStore
{
    private readonly Func<string, bool> save;

    public DelegateUpdateSuppressionStore(Func<string, bool> save)
    {
        ArgumentNullException.ThrowIfNull(save);
        this.save = save;
    }

    public bool TrySave(string normalizedVersion) => save(normalizedVersion);
}
