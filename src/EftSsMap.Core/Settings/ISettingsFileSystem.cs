namespace EftSsMap.Core.Settings;

/// <summary>
/// File operations needed to load and atomically save settings.
/// </summary>
public interface ISettingsFileSystem
{
    bool FileExists(string path);

    string ReadAllText(string path);

    void WriteAllText(string path, string contents);

    void MoveFile(string sourcePath, string destinationPath);

    void ReplaceFile(string sourcePath, string destinationPath);

    void DeleteFile(string path);
}
