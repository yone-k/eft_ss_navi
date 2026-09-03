namespace EftSsNavi.Core.Settings;

/// <summary>
/// Uses the local file system for settings persistence.
/// </summary>
public sealed class SettingsFileSystem : ISettingsFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteAllText(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, contents);
    }

    public void MoveFile(string sourcePath, string destinationPath) =>
        File.Move(sourcePath, destinationPath);

    public void ReplaceFile(string sourcePath, string destinationPath) =>
        File.Replace(sourcePath, destinationPath, null);

    public void DeleteFile(string path) => File.Delete(path);
}
