using System.IO.Compression;
using System.Security.Cryptography;
using EftSsNavi.Launcher.Updates;

namespace EftSsNavi.Launcher.Tests;

public sealed class UpdatePackageStagerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "EftSsNaviTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ShouldRejectDigestMismatchBeforeExtraction()
    {
        var zip = CreateZip(("EftSsNavi.exe", "x"));
        var staging = Path.Combine(root, "staging");
        var result = await new UpdatePackageStager().StageAsync(zip, new string('0', 64), staging);
        Assert.Equal(PackageStageStatus.DigestMismatch, result.Status);
        Assert.False(Directory.Exists(staging));
    }

    [Fact]
    public async Task ShouldRejectPathTraversal()
    {
        var zip = CreateZip(("../escaped.txt", "bad"));
        var result = await new UpdatePackageStager().StageAsync(zip, Digest(zip), Path.Combine(root, "staging"));
        Assert.Equal(PackageStageStatus.InvalidArchive, result.Status);
        Assert.False(File.Exists(Path.Combine(root, "escaped.txt")));
    }

    [Fact]
    public async Task ShouldRejectUnexpectedRootEntry()
    {
        var zip = CreateZip(("EftSsNavi.exe", "x"), ("README.md", "x"), ("extra.txt", "x"));
        var result = await new UpdatePackageStager().StageAsync(zip, Digest(zip), Path.Combine(root, "staging"));
        Assert.Equal(PackageStageStatus.InvalidLayout, result.Status);
    }

    [Fact]
    public async Task ShouldStageValidDistributionWithMatchingVersions()
    {
        var binary = await File.ReadAllBytesAsync(typeof(UpdatePackageStager).Assembly.Location);
        Directory.CreateDirectory(root);
        var zip = Path.Combine(root, "valid.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            Add(archive, "EftSsNavi.exe", binary); Add(archive, "README.md", "readme"u8.ToArray());
            Add(archive, "app/EftSsNavi.App.exe", binary); Add(archive, "app/EftSsNavi.Sharing.dll", binary);
            Add(archive, "app/SIPSorcery.dll", binary); Add(archive, "app/THIRD-PARTY-NOTICES.md", "notices"u8.ToArray());
            Add(archive, "app/Assets/Maps/catalog.json", "{}"u8.ToArray());
        }
        var result = await new UpdatePackageStager().StageAsync(zip, Digest(zip), Path.Combine(root, "staging"), "1.0.0");
        Assert.Equal(PackageStageStatus.Succeeded, result.Status);
        Assert.True(File.Exists(Path.Combine(result.StagingDirectory!, "app", "EftSsNavi.App.exe")));
    }

    private string CreateZip(params (string Name, string Content)[] entries)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "package.zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (Name, Content) in entries) { using var writer = new StreamWriter(archive.CreateEntry(Name).Open()); writer.Write(Content); }
        return path;
    }
    private static string Digest(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static void Add(ZipArchive archive, string name, byte[] content) { using var stream = archive.CreateEntry(name).Open(); stream.Write(content); }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
