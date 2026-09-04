using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace EftSsNavi.Launcher.Updates;

public enum PackageStageStatus { Succeeded, DigestMismatch, InvalidArchive, InvalidLayout, IoFailure, Canceled }
public sealed record PackageStageResult(PackageStageStatus Status, string? StagingDirectory = null);

public sealed class UpdatePackageStager
{
    private static readonly string[] RequiredFiles =
    [
        "EftSsNavi.exe", "README.md", "app/EftSsNavi.App.exe", "app/EftSsNavi.Sharing.dll",
        "app/SIPSorcery.dll", "app/THIRD-PARTY-NOTICES.md", "app/Assets/Maps/catalog.json",
    ];

    public async Task<PackageStageResult> StageAsync(
        string zipPath,
        string expectedSha256,
        string stagingDirectory,
        string? expectedVersion = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var input = File.OpenRead(zipPath);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken)).ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(expectedSha256)))
                return new(PackageStageStatus.DigestMismatch);

            var fullStaging = Path.GetFullPath(stagingDirectory);
            var work = fullStaging + ".processing";
            if (Directory.Exists(work)) Directory.Delete(work, true);
            Directory.CreateDirectory(work);
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var destination = Path.GetFullPath(Path.Combine(work, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!IsWithin(destination, work)) return Invalid(work, PackageStageStatus.InvalidArchive);
                    if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(destination); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    entry.ExtractToFile(destination, true);
                }
                if (!HasValidLayout(work, expectedVersion)) return Invalid(work, PackageStageStatus.InvalidLayout);
                if (Directory.Exists(fullStaging)) Directory.Delete(fullStaging, true);
                Directory.Move(work, fullStaging);
                return new(PackageStageStatus.Succeeded, fullStaging);
            }
            catch (InvalidDataException) { return Invalid(work, PackageStageStatus.InvalidArchive); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new(PackageStageStatus.Canceled); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException) { return new(PackageStageStatus.IoFailure); }
    }

    private static bool HasValidLayout(string root, string? expectedVersion)
    {
        var roots = Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName).Order().ToArray();
        if (!roots.SequenceEqual(new[] { "app", "EftSsNavi.exe", "README.md" }.Order())) return false;
        if (RequiredFiles.Any(relative => !File.Exists(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))))) return false;
        if (expectedVersion is null) return true;
        var launcher = FileVersionInfo.GetVersionInfo(Path.Combine(root, "EftSsNavi.exe")).FileVersion;
        var app = FileVersionInfo.GetVersionInfo(Path.Combine(root, "app", "EftSsNavi.App.exe")).FileVersion;
        return string.Equals(Normalize(launcher), expectedVersion, StringComparison.Ordinal)
            && string.Equals(Normalize(app), expectedVersion, StringComparison.Ordinal);
    }
    private static string? Normalize(string? version) => Version.TryParse(version, out var parsed) && parsed.Build >= 0 ? new Version(parsed.Major, parsed.Minor, parsed.Build).ToString(3) : null;
    private static bool IsWithin(string path, string root) => path.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static PackageStageResult Invalid(string work, PackageStageStatus status) { if (Directory.Exists(work)) Directory.Delete(work, true); return new(status); }
}
