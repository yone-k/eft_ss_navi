namespace EftSsNavi.App.Updates;

public interface IUpdateSuppressionStore
{
    bool TrySave(string normalizedVersion);
}
