using System.Text.Json;
using EftSsMap.Core.Calibration;
using EftSsMap.Core.Settings;

namespace EftSsMap.Core.Tests.Settings;

public sealed class SettingsRepositoryTests
{
    private const string DestinationPath = @"C:\Users\tester\AppData\Local\EftSsMap\settings.json";

    [Fact]
    public void ShouldRoundTripAllSettingsValuesThroughSystemTextJson()
    {
        // Given: Settings containing the watch directory, every map profile, and the last selection.
        var settings = new AppSettings(
            @"C:\EFT\Screenshots",
            [CreateProfile("Woods"), CreateProfile("Customs")],
            "Customs");

        // When: The public settings contract is serialized and deserialized with System.Text.Json.
        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<AppSettings>(json);

        // Then: Every persisted setting is restored with the same value.
        Assert.NotNull(restored);
        Assert.Equal(settings.WatchDirectory, restored.WatchDirectory);
        Assert.Equal(settings.LastSelectedProfileName, restored.LastSelectedProfileName);
        Assert.Equal(settings.MapProfiles.Count, restored.MapProfiles.Count);
        AssertProfileEquivalent(settings.MapProfiles[0], restored.MapProfiles[0]);
        AssertProfileEquivalent(settings.MapProfiles[1], restored.MapProfiles[1]);
    }

    [Fact]
    public void ShouldWriteTemporaryFileInDestinationDirectoryWhenSaving()
    {
        // Given: A repository whose destination directory is known.
        var fileSystem = new FakeSettingsFileSystem();
        var repository = new SettingsRepository(fileSystem, DestinationPath);

        // When: Settings are saved.
        var result = repository.Save(CreateSettings());

        // Then: The temporary write is located beside the destination file.
        Assert.True(result.IsSuccess);
        Assert.Equal(
            Path.GetDirectoryName(DestinationPath),
            Path.GetDirectoryName(Assert.Single(fileSystem.WritePaths)));
    }

    [Fact]
    public void ShouldMoveCompletedTemporaryFileWhenDestinationDoesNotExist()
    {
        // Given: No existing destination file.
        var fileSystem = new FakeSettingsFileSystem();
        var repository = new SettingsRepository(fileSystem, DestinationPath);

        // When: Settings are saved for the first time.
        var result = repository.Save(CreateSettings());

        // Then: The completed temporary file is moved into place.
        Assert.True(result.IsSuccess);
        var move = Assert.Single(fileSystem.MoveCalls);
        Assert.Equal(fileSystem.WritePaths.Single(), move.Source);
        Assert.Equal(DestinationPath, move.Destination);
        Assert.Empty(fileSystem.ReplaceCalls);
    }

    [Fact]
    public void ShouldReplaceDestinationWhenItAlreadyExists()
    {
        // Given: An existing destination file.
        var fileSystem = new FakeSettingsFileSystem();
        fileSystem.SeedFile(DestinationPath, "old settings");
        var repository = new SettingsRepository(fileSystem, DestinationPath);

        // When: Updated settings are saved.
        var result = repository.Save(CreateSettings());

        // Then: The destination is replaced from the completed temporary file.
        Assert.True(result.IsSuccess);
        var replace = Assert.Single(fileSystem.ReplaceCalls);
        Assert.Equal(fileSystem.WritePaths.Single(), replace.Source);
        Assert.Equal(DestinationPath, replace.Destination);
        Assert.Empty(fileSystem.MoveCalls);
    }

    [Fact]
    public void ShouldPreserveDestinationAndReportTemporaryWriteErrorWhenTemporaryWriteFails()
    {
        // Given: Existing settings and a file system that rejects the temporary write.
        var fileSystem = new FakeSettingsFileSystem { ThrowOnWrite = true };
        fileSystem.SeedFile(DestinationPath, "old settings");
        var repository = new SettingsRepository(fileSystem, DestinationPath);

        // When: Updated settings are saved.
        var result = repository.Save(CreateSettings());

        // Then: The existing destination is untouched and the cause is classified.
        Assert.False(result.IsSuccess);
        Assert.Equal(SettingsErrorKind.WriteTemporary, result.ErrorKind);
        Assert.Equal("old settings", fileSystem.GetFile(DestinationPath));
        Assert.Empty(fileSystem.MoveCalls);
        Assert.Empty(fileSystem.ReplaceCalls);
    }

    [Fact]
    public void ShouldLeaveDestinationAbsentAndReportMoveErrorWhenInitialMoveFails()
    {
        // Given: A first save whose final move will fail.
        var fileSystem = new FakeSettingsFileSystem { ThrowOnMove = true };
        var repository = new SettingsRepository(fileSystem, DestinationPath);

        // When: Settings are saved.
        var result = repository.Save(CreateSettings());

        // Then: No partial destination appears and the cause is classified.
        Assert.False(result.IsSuccess);
        Assert.Equal(SettingsErrorKind.Move, result.ErrorKind);
        Assert.False(fileSystem.FileExists(DestinationPath));
    }

    [Fact]
    public void ShouldPreserveDestinationAndReportReplaceErrorWhenReplaceFails()
    {
        // Given: Existing settings and a file system that rejects replacement.
        var fileSystem = new FakeSettingsFileSystem { ThrowOnReplace = true };
        fileSystem.SeedFile(DestinationPath, "old settings");
        var repository = new SettingsRepository(fileSystem, DestinationPath);

        // When: Updated settings are saved.
        var result = repository.Save(CreateSettings());

        // Then: The existing destination is untouched and the cause is classified.
        Assert.False(result.IsSuccess);
        Assert.Equal(SettingsErrorKind.Replace, result.ErrorKind);
        Assert.Equal("old settings", fileSystem.GetFile(DestinationPath));
    }

    [Fact]
    public void ShouldReportDeserializeErrorWhenJsonCannotBeLoaded()
    {
        // Given: A destination containing invalid JSON.
        var fileSystem = new FakeSettingsFileSystem();
        fileSystem.SeedFile(DestinationPath, "not-json");
        var repository = new SettingsRepository(fileSystem, DestinationPath);

        // When: Settings are loaded.
        var result = repository.Load();

        // Then: The failure identifies deserialization as its cause for the UI.
        Assert.False(result.IsSuccess);
        Assert.Equal(SettingsErrorKind.Deserialize, result.ErrorKind);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void ShouldLoadPersistedSettingsWhenJsonIsValid()
    {
        // Given: A destination containing valid settings JSON.
        var expected = CreateSettings();
        var fileSystem = new FakeSettingsFileSystem();
        fileSystem.SeedFile(DestinationPath, JsonSerializer.Serialize(expected));
        var repository = new SettingsRepository(fileSystem, DestinationPath);

        // When: Settings are loaded.
        var result = repository.Load();

        // Then: A successful result exposes the restored settings.
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(expected.WatchDirectory, result.Value.WatchDirectory);
        Assert.Equal(expected.LastSelectedProfileName, result.Value.LastSelectedProfileName);
        AssertProfileEquivalent(expected.MapProfiles.Single(), result.Value.MapProfiles.Single());
    }

    [Fact]
    public void ShouldRejectSettingsWithDuplicateProfileNamesIgnoringCase()
    {
        // Given: Syntactically valid JSON contains duplicate profile names.
        var fileSystem = new FakeSettingsFileSystem();
        var settings = new AppSettings(
            @"C:\EFT\Screenshots",
            [CreateProfile("Woods"), CreateProfile("WOODS")],
            "Woods");
        fileSystem.SeedFile(DestinationPath, JsonSerializer.Serialize(settings));
        var repository = new SettingsRepository(fileSystem, DestinationPath);

        // When
        var result = repository.Load();

        // Then
        Assert.False(result.IsSuccess);
        Assert.Equal(SettingsErrorKind.Deserialize, result.ErrorKind);
        Assert.Equal(SettingsErrorKind.SaveBlocked, repository.Save(CreateSettings()).ErrorKind);
    }

    [Fact]
    public void ShouldRejectSavingDuplicateProfileNamesIgnoringCase()
    {
        // Given
        var fileSystem = new FakeSettingsFileSystem();
        var repository = new SettingsRepository(fileSystem, DestinationPath);
        var settings = new AppSettings(
            @"C:\EFT\Screenshots",
            [CreateProfile("Woods"), CreateProfile("WOODS")],
            "Woods");

        // When
        var result = repository.Save(settings);

        // Then
        Assert.False(result.IsSuccess);
        Assert.Equal(SettingsErrorKind.Validation, result.ErrorKind);
        Assert.Empty(fileSystem.WritePaths);
        Assert.False(fileSystem.FileExists(DestinationPath));
    }

    [Fact]
    public void ShouldRejectSettingsWithNullProfileElement()
    {
        // Given: Syntactically valid JSON contains a null profile element.
        var fileSystem = new FakeSettingsFileSystem();
        fileSystem.SeedFile(
            DestinationPath,
            """
            {
              "WatchDirectory": "C:\\EFT\\Screenshots",
              "MapProfiles": [null],
              "LastSelectedProfileName": null
            }
            """);
        var repository = new SettingsRepository(fileSystem, DestinationPath);

        // When
        var result = repository.Load();

        // Then
        Assert.False(result.IsSuccess);
        Assert.Equal(SettingsErrorKind.Deserialize, result.ErrorKind);
        Assert.Equal(SettingsErrorKind.SaveBlocked, repository.Save(CreateSettings()).ErrorKind);
    }

    [Fact]
    public void ShouldReportReadErrorWhenSettingsFileCannotBeRead()
    {
        // Given: A destination that exists but cannot be read.
        var fileSystem = new FakeSettingsFileSystem { ThrowOnRead = true };
        fileSystem.SeedFile(DestinationPath, "unavailable");
        var repository = new SettingsRepository(fileSystem, DestinationPath);

        // When: Settings are loaded.
        var result = repository.Load();

        // Then: The failure identifies file reading as its cause for the UI.
        Assert.False(result.IsSuccess);
        Assert.Equal(SettingsErrorKind.Read, result.ErrorKind);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void ShouldBlockOrdinarySaveAfterJsonLoadFailure()
    {
        // Given: A repository that has observed invalid JSON at its destination.
        var fileSystem = new FakeSettingsFileSystem();
        fileSystem.SeedFile(DestinationPath, "not-json");
        var repository = new SettingsRepository(fileSystem, DestinationPath);
        Assert.False(repository.Load().IsSuccess);

        // When: An ordinary save is attempted.
        var result = repository.Save(CreateSettings());

        // Then: The original file is protected without attempting a write.
        Assert.False(result.IsSuccess);
        Assert.Equal(SettingsErrorKind.SaveBlocked, result.ErrorKind);
        Assert.Equal("not-json", fileSystem.GetFile(DestinationPath));
        Assert.Empty(fileSystem.WritePaths);
    }

    [Fact]
    public void ShouldAllowSaveAfterLoadFailureProtectionIsExplicitlyReset()
    {
        // Given: A repository whose failed-load protection is explicitly reset.
        var fileSystem = new FakeSettingsFileSystem();
        fileSystem.SeedFile(DestinationPath, "not-json");
        var repository = new SettingsRepository(fileSystem, DestinationPath);
        Assert.False(repository.Load().IsSuccess);
        repository.ResetLoadFailureProtection();

        // When: Settings are saved.
        var result = repository.Save(CreateSettings());

        // Then: Saving is allowed again.
        Assert.True(result.IsSuccess);
        Assert.Single(fileSystem.ReplaceCalls);
    }

    [Fact]
    public void ShouldAllowSaveFromNewRepositoryAfterAnotherRepositoryLoadFailed()
    {
        // Given: Invalid JSON was loaded by an earlier repository instance.
        var fileSystem = new FakeSettingsFileSystem();
        fileSystem.SeedFile(DestinationPath, "not-json");
        var failedRepository = new SettingsRepository(fileSystem, DestinationPath);
        Assert.False(failedRepository.Load().IsSuccess);
        var newRepository = new SettingsRepository(fileSystem, DestinationPath);

        // When: The new repository saves settings.
        var result = newRepository.Save(CreateSettings());

        // Then: Protection is scoped to the instance that observed the load failure.
        Assert.True(result.IsSuccess);
        Assert.Single(fileSystem.ReplaceCalls);
    }

    private static AppSettings CreateSettings() =>
        new(@"C:\EFT\Screenshots", [CreateProfile("Woods")], "Woods");

    private static MapProfile CreateProfile(string name)
    {
        CalibrationPoint[] points =
        [
            new(new WorldPoint(10, 20), new PixelPoint(100, 200)),
            new(new WorldPoint(30, 20), new PixelPoint(300, 200)),
            new(new WorldPoint(10, 40), new PixelPoint(100, 400)),
        ];

        return new MapProfile(
            name,
            $@"C:\maps\{name}.webp",
            7000,
            6800,
            new string('a', 64),
            points,
            new AffineTransform2D(10, 0, 0, 10, 0, 0));
    }

    private static void AssertProfileEquivalent(MapProfile expected, MapProfile actual)
    {
        Assert.Equal(expected.DisplayName, actual.DisplayName);
        Assert.Equal(expected.ImagePath, actual.ImagePath);
        Assert.Equal(expected.CalibratedImageWidth, actual.CalibratedImageWidth);
        Assert.Equal(expected.CalibratedImageHeight, actual.CalibratedImageHeight);
        Assert.Equal(expected.ImageSha256, actual.ImageSha256);
        Assert.Equal(expected.CalibrationPoints, actual.CalibrationPoints);
        Assert.Equal(expected.Transform, actual.Transform);
    }

    private sealed class FakeSettingsFileSystem : ISettingsFileSystem
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

        public bool ThrowOnWrite { get; init; }

        public bool ThrowOnRead { get; init; }

        public bool ThrowOnMove { get; init; }

        public bool ThrowOnReplace { get; init; }

        public List<string> WritePaths { get; } = [];

        public List<(string Source, string Destination)> MoveCalls { get; } = [];

        public List<(string Source, string Destination)> ReplaceCalls { get; } = [];

        public bool FileExists(string path) => _files.ContainsKey(path);

        public string ReadAllText(string path)
        {
            if (ThrowOnRead)
            {
                throw new IOException("Read failed.");
            }

            return _files[path];
        }

        public void WriteAllText(string path, string contents)
        {
            WritePaths.Add(path);
            if (ThrowOnWrite)
            {
                throw new IOException("Temporary write failed.");
            }

            _files[path] = contents;
        }

        public void MoveFile(string sourcePath, string destinationPath)
        {
            MoveCalls.Add((sourcePath, destinationPath));
            if (ThrowOnMove)
            {
                throw new IOException("Move failed.");
            }

            _files[destinationPath] = _files[sourcePath];
            _files.Remove(sourcePath);
        }

        public void ReplaceFile(string sourcePath, string destinationPath)
        {
            ReplaceCalls.Add((sourcePath, destinationPath));
            if (ThrowOnReplace)
            {
                throw new IOException("Replace failed.");
            }

            _files[destinationPath] = _files[sourcePath];
            _files.Remove(sourcePath);
        }

        public void DeleteFile(string path) => _files.Remove(path);

        public void SeedFile(string path, string contents) => _files[path] = contents;

        public string GetFile(string path) => _files[path];
    }
}
