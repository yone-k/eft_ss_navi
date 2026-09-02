using System.Collections.ObjectModel;
using System.Globalization;
using EftSsMap.App.Controls;
using EftSsMap.App.Imaging;
using EftSsMap.App.Monitoring;
using EftSsMap.App.Pickers;
using EftSsMap.App.Presentation;
using EftSsMap.Core.Calibration;
using EftSsMap.Core.Images;
using EftSsMap.Core.Monitoring;
using EftSsMap.Core.Observations;
using EftSsMap.Core.Presentation;
using EftSsMap.Core.Settings;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace EftSsMap.App;

public sealed partial class MainWindow : Window
{
    private readonly MapCanvas MapControl;
    private readonly ObservableCollection<MapProfile> _profiles = [];
    private readonly IFilePickerService _pickerService = new FilePickerService();
    private readonly PickerDefaultDirectories _pickerDefaultDirectories = new(AppContext.BaseDirectory);
    private readonly MainStateCoordinator _stateCoordinator = new();
    private readonly ScreenshotNotificationPresenter _notificationPresenter;
    private readonly SettingsFileSystem _settingsFileSystem = new();
    private readonly LatestImageLoadTracker _imageLoadTracker = new();
    private readonly ScreenshotMonitor _screenshotMonitor;
    private readonly SettingsRepository _settingsRepository;
    private readonly string _settingsPath;
    private IReadOnlyDictionary<string, IReadOnlyList<MapMarker>> _bundledMapMarkers =
        new Dictionary<string, IReadOnlyList<MapMarker>>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<MapProfile> _bundledProfiles = [];
    private PositionCorrectionSession? _correctionSession;
    private ProgressiveCalibrationSession? _progressiveCalibrationSession;
    private PositionObservation? _pendingCalibrationObservation;
    private string? _pendingCalibrationFileName;
    private MapProfile? _selectedProfile;
    private string? _watchDirectory;
    private int _bundledMapCatalogVersion;
    private bool _initialized;
    private bool _isClosed;

    public MainWindow()
    {
        InitializeComponent();
        MapControl = new MapCanvas();
        MapControl.ImagePixelClicked += OnMapImagePixelClicked;
        MapControl.MarkerCorrectionRequested += OnMarkerCorrectionRequested;
        MapControl.CalibrationAnchorSelected += OnCalibrationAnchorSelected;
        MapHost.Children.Add(MapControl);
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EftSsMap",
            "settings.json");
        _settingsRepository = new SettingsRepository(_settingsFileSystem, _settingsPath);
        _notificationPresenter = new ScreenshotNotificationPresenter(_stateCoordinator);
        _screenshotMonitor = new ScreenshotMonitor(
            new FileSystemWatcherCreatedSourceFactory(),
            new ScreenshotFileNameParserAdapter(),
            new ScreenshotNotificationDeduplicator(TimeProvider.System));
        _screenshotMonitor.ObservationAccepted += OnObservationAccepted;
        _screenshotMonitor.FileNameRejected += OnFileNameRejected;
        _screenshotMonitor.MonitoringFailed += OnMonitoringFailed;
        Closed += OnWindowClosed;
    }

    private async void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        AppSettings? settings = null;
        string? settingsFailureMessage = null;
        string? watchFailureMessage = null;
        var settingsLoadFailed = false;
        var bundledCatalogUpdated = false;
        if (_settingsFileSystem.FileExists(_settingsPath))
        {
            var loadResult = _settingsRepository.Load();
            if (loadResult.IsSuccess)
            {
                settings = loadResult.Value;
            }
            else
            {
                settingsLoadFailed = true;
                settingsFailureMessage =
                    $"設定を読み込めません。元の設定ファイルは上書きしません。{loadResult.ErrorMessage}";
            }
        }

        if (!settingsLoadFailed)
        {
            try
            {
                var bundledCatalog = BundledMapCatalog.Load(_pickerDefaultDirectories.BundledMaps);
                _bundledMapMarkers = bundledCatalog.MarkersByProfileName;
                _bundledProfiles = bundledCatalog.Profiles;
                var settingsBeforeCatalog = settings ?? new AppSettings(null, [], null);
                settings = BundledProfileSeeder.Apply(
                    settingsBeforeCatalog,
                    bundledCatalog.Profiles,
                    bundledCatalog.Version,
                    bundledCatalog.ReplaceableImageFileNames);
                bundledCatalogUpdated = !ReferenceEquals(settings, settingsBeforeCatalog);
            }
            catch (Exception exception)
            {
                settingsFailureMessage = $"同梱マップ設定を読み込めません。{exception.Message}";
            }
        }

        _bundledMapCatalogVersion = settings?.BundledMapCatalogVersion ?? 0;

        if (settings is not null)
        {
            foreach (var profile in settings.MapProfiles)
            {
                if (!_profiles.Any(existing => NamesEqual(existing.DisplayName, profile.DisplayName)))
                {
                    _profiles.Add(profile);
                }
            }
        }

        var savedDirectory = settings?.WatchDirectory;
        if (!string.IsNullOrWhiteSpace(savedDirectory) && Directory.Exists(savedDirectory))
        {
            watchFailureMessage = SetWatchDirectory(savedDirectory, persist: false);
        }
        else
        {
            var defaultDirectory = new DefaultScreenshotDirectoryProvider().GetDefaultDirectory();
            if (defaultDirectory is not null)
            {
                watchFailureMessage = SetWatchDirectory(
                    defaultDirectory,
                    persist: settings is null || savedDirectory is null);
            }
            else if (settings is null || string.IsNullOrWhiteSpace(savedDirectory))
            {
                watchFailureMessage = StartupStatusResolver.ChooseWatchDirectoryMessage;
            }
            else
            {
                watchFailureMessage = "保存されている監視先が存在しません。新しいフォルダーを選択してください。";
            }
        }

        if (bundledCatalogUpdated)
        {
            settingsFailureMessage ??= PersistSettings();
        }

        var lastSelected = settings?.LastSelectedProfileName;
        var restoredProfile = _profiles.FirstOrDefault(profile => NamesEqual(profile.DisplayName, lastSelected));
        if (restoredProfile is not null)
        {
            SetSelectedProfile(restoredProfile);
            await ActivateProfileAsync(restoredProfile, persist: false);
            if ((settingsFailureMessage ?? watchFailureMessage) is { } startupFailureMessage)
            {
                SetStatus(startupFailureMessage);
            }
        }
        else
        {
            ApplyStateToView(StartupStatusResolver.Resolve(
                settingsFailureMessage,
                watchFailureMessage,
                _watchDirectory is not null));
        }
    }

    private async void OnChooseWatchDirectoryClick(object sender, RoutedEventArgs e)
    {
        var result = await _pickerService.PickFolderAsync(this);
        if (!result.IsSuccess)
        {
            SetStatus(result.ErrorMessage ?? "フォルダーを選択できませんでした。");
            return;
        }

        if (result.IsCanceled)
        {
            return;
        }

        if (result.Path is null || !Directory.Exists(result.Path))
        {
            SetStatus("選択した監視先が存在しません。");
            return;
        }

        _ = SetWatchDirectory(result.Path, persist: true);
    }

    private string? SetWatchDirectory(string directoryPath, bool persist)
    {
        try
        {
            _screenshotMonitor.SetDirectory(directoryPath);
            _watchDirectory = Path.GetFullPath(directoryPath);
            WatchDirectoryText.Text = _watchDirectory;
            _stateCoordinator.ChangeWatchDirectory(_watchDirectory);
            ApplyStateToView("監視を開始しました。次の有効なスクリーンショットを待っています。");
            if (persist)
            {
                return PersistSettings();
            }

            return null;
        }
        catch (Exception exception)
        {
            var message = $"監視を開始できません。{exception.Message}";
            SetStatus(message);
            return message;
        }
    }

    private void OnProfileMenuClick(object sender, RoutedEventArgs e)
    {
        var menu = new MenuFlyout();
        if (_profiles.Count == 0)
        {
            menu.Items.Add(new MenuFlyoutItem
            {
                Text = "追加済みのマップはありません",
                IsEnabled = false,
            });
        }
        else
        {
            foreach (var profile in _profiles.OrderBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                var item = new MenuFlyoutItem
                {
                    Text = profile.DisplayName,
                    Tag = profile,
                };
                item.Click += OnProfileMenuItemClick;
                menu.Items.Add(item);
            }
        }

        menu.ShowAt(ProfileMenuButton, new FlyoutShowOptions
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
        });
    }

    private async void OnProfileMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: MapProfile profile })
        {
            return;
        }

        ResetProgressiveCalibration();
        SetSelectedProfile(profile);
        await ActivateProfileAsync(profile, persist: true);
    }

    private void SetSelectedProfile(MapProfile? profile)
    {
        _selectedProfile = profile;
        ProfileMenuButton.Content = profile?.DisplayName ?? "マップを選択";
        RotateMapLeftButton.IsEnabled = profile is not null;
        RotateMapRightButton.IsEnabled = profile is not null;
        UpdateCorrectionModeButtonAvailability(profile);
    }

    private async Task ActivateProfileAsync(MapProfile profile, bool persist)
    {
        ResetCorrectionMode();
        ResetProgressiveCalibration();
        MapControl.SetImageRotation(profile.ImageRotationQuarterTurns);
        SetBundledMapMarkers(null);
        var generation = _imageLoadTracker.Begin();
        if (_stateCoordinator.SelectedProfile is { } previousProfile)
        {
            _stateCoordinator.DeleteProfile(previousProfile.DisplayName);
        }

        MapControl.SetMarker(null, null);
        SetStatus("マップ画像を検証しています。");
        var loadResult = await SkiaMapImageLoader.LoadAsync(profile.ImagePath);
        if (!_imageLoadTracker.IsCurrent(generation))
        {
            loadResult.Image?.Dispose();
            return;
        }

        if (!loadResult.IsSuccess || loadResult.Image is null)
        {
            MapControl.SetImage(null);
            _stateCoordinator.SelectProfile(
                profile,
                new ImageFingerprint(profile.ImagePath, 0, 0, string.Empty),
                calibrationValid: false);
            ApplyStateToView(loadResult.ErrorMessage ?? "マップ画像を読み込めません。マップを追加し直してください。");
            if (persist)
            {
                PersistSettings();
            }

            return;
        }

        var image = loadResult.Image;
        MapControl.SetImage(image);
        var calibrationComplete = profile.CalibrationPoints.Count == 3;
        var calibrationValid = calibrationComplete && IsStoredTransformValid(profile.Transform);
        _stateCoordinator.SelectProfile(profile, image.Fingerprint, calibrationValid);
        var validation = ProfileImageValidator.Validate(FingerprintFor(profile), image.Fingerprint);
        if (validation != ProfileImageValidationResult.Match)
        {
            ApplyStateToView("画像がマップ追加時と一致しません。マップを追加し直してください。");
        }
        else if (profile.CalibrationPoints.Count < 3)
        {
            _progressiveCalibrationSession = new ProgressiveCalibrationSession(profile);
            UpdateProgressiveCalibrationPrompt(waitingForMapClick: false);
            ApplyStateToView(CalibrationWaitingMessage(profile.CalibrationPoints.Count));
        }
        else if (calibrationValid)
        {
            SetBundledMapMarkers(profile);
            ApplyStateToView("マップを選択しました。次の有効なスクリーンショットを待っています。");
        }
        else
        {
            ApplyStateToView("保存されている校正情報が無効です。マップを追加し直してください。");
        }
        if (persist)
        {
            PersistSettings();
        }
    }

    private async void OnNewProfileClick(object sender, RoutedEventArgs e)
    {
        var displayName = await PromptForProfileNameAsync();
        if (displayName is null)
        {
            return;
        }

        if (_profiles.Any(profile => NamesEqual(profile.DisplayName, displayName)))
        {
            SetStatus("同じ名前のプロファイルが既に存在します。大文字・小文字だけの違いも同じ名前として扱います。");
            return;
        }

        var imageResult = await _pickerService.PickMapImageAsync(this, _pickerDefaultDirectories.BundledMaps);
        if (!imageResult.IsSuccess)
        {
            SetStatus(imageResult.ErrorMessage ?? "マップ画像を選択できませんでした。");
            return;
        }

        if (!imageResult.IsCanceled && imageResult.Path is not null)
        {
            await AddUncalibratedProfileAsync(displayName, imageResult.Path);
        }
    }

    private async Task AddUncalibratedProfileAsync(string displayName, string imagePath)
    {
        ResetCorrectionMode();
        ResetProgressiveCalibration();
        var generation = _imageLoadTracker.Begin();
        var loadResult = await SkiaMapImageLoader.LoadAsync(imagePath);
        if (!_imageLoadTracker.IsCurrent(generation))
        {
            loadResult.Image?.Dispose();
            return;
        }

        if (!loadResult.IsSuccess || loadResult.Image is null)
        {
            SetStatus(loadResult.ErrorMessage ?? "マップ画像を読み込めませんでした。");
            return;
        }

        if (_stateCoordinator.SelectedProfile is { } previousProfile)
        {
            _stateCoordinator.DeleteProfile(previousProfile.DisplayName);
        }

        var profile = MapProfile.CreateUncalibrated(displayName, loadResult.Image.Fingerprint);
        MapControl.SetImageRotation(profile.ImageRotationQuarterTurns);
        MapControl.SetImage(loadResult.Image);
        MapControl.SetMarker(null, null);
        _profiles.Add(profile);
        SetSelectedProfile(profile);
        _progressiveCalibrationSession = new ProgressiveCalibrationSession(profile);
        _stateCoordinator.SelectProfile(profile, loadResult.Image.Fingerprint, calibrationValid: false);
        UpdateProgressiveCalibrationPrompt(waitingForMapClick: false);
        ApplyStateToView("マップを追加しました。スクリーンショットを検知すると校正位置を尋ねます。");
        PersistSettings();
    }

    private void OnMapImagePixelClicked(object? sender, MapImagePixelClickedEventArgs e)
    {
        var session = _progressiveCalibrationSession;
        if (session is null)
        {
            return;
        }

        var observation = _pendingCalibrationObservation;
        var fileName = _pendingCalibrationFileName;
        var placement = session.Place(e.ImagePixel);
        _pendingCalibrationObservation = null;
        _pendingCalibrationFileName = null;
        switch (placement)
        {
            case ProgressiveCalibrationPlacement.NoPendingPosition:
                return;
            case ProgressiveCalibrationPlacement.InvalidAnchor:
                UpdateProgressiveCalibrationPrompt(waitingForMapClick: false);
                ApplyStateToView("その地点は既存点と重複または同一直線上です。離れた場所で次のスクリーンショットを撮ってください。");
                return;
            case ProgressiveCalibrationPlacement.AnchorAdded:
                ReplaceSelectedProfile(session.Profile, calibrationValid: false);
                UpdateProgressiveCalibrationPrompt(waitingForMapClick: false);
                ApplyStateToView(CalibrationWaitingMessage(session.Profile.CalibrationPoints.Count));
                PersistSettings();
                return;
            case ProgressiveCalibrationPlacement.Completed:
                ReplaceSelectedProfile(session.Profile, calibrationValid: true);
                _progressiveCalibrationSession = null;
                ProgressiveCalibrationPanel.Visibility = Visibility.Collapsed;
                if (observation is not null && fileName is not null)
                {
                    _stateCoordinator.ProcessObservation(observation, fileName);
                }

                ApplyStateToView("3地点の校正を保存しました。現在位置を表示しています。");
                PersistSettings();
                return;
        }
    }

    private async void OnDeleteProfileClick(object sender, RoutedEventArgs e)
    {
        if (_selectedProfile is not { } selected)
        {
            SetStatus("削除するマッププロファイルを選択してください。");
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "マッププロファイルを削除しますか？",
            Content = $"「{selected.DisplayName}」の校正情報を削除します。元のマップ画像は削除しません。",
            PrimaryButtonText = "削除",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _profiles.Remove(selected);
        ResetProgressiveCalibration();
        SetSelectedProfile(null);
        _stateCoordinator.DeleteProfile(selected.DisplayName);
        SetBundledMapMarkers(null);
        MapControl.SetImage(null);
        ApplyStateToView("プロファイルを削除しました。現在のマップを選択してください。");
        PersistSettings();
    }

    private void OnRotateMapLeftClick(object sender, RoutedEventArgs e) => RotateSelectedMap(-1);

    private void OnRotateMapRightClick(object sender, RoutedEventArgs e) => RotateSelectedMap(1);

    private void RotateSelectedMap(int quarterTurnDelta)
    {
        if (_selectedProfile is not { } selected)
        {
            SetStatus("回転するマップを選択してください。");
            return;
        }

        var profileIndex = _profiles.IndexOf(selected);
        if (profileIndex < 0)
        {
            SetStatus("選択中のマップ設定を更新できませんでした。");
            return;
        }

        ResetCorrectionMode();
        var requestedQuarterTurns = selected.ImageRotationQuarterTurns + quarterTurnDelta;
        MapProfile rotatedProfile;
        if (_progressiveCalibrationSession is { } calibrationSession)
        {
            calibrationSession.SetImageRotationQuarterTurns(requestedQuarterTurns);
            rotatedProfile = calibrationSession.Profile;
        }
        else
        {
            rotatedProfile = selected.WithImageRotationQuarterTurns(requestedQuarterTurns);
        }

        if (!_stateCoordinator.TryUpdateSelectedProfileDisplaySettings(rotatedProfile))
        {
            SetStatus("選択中のマップ設定を更新できませんでした。");
            return;
        }

        _profiles[profileIndex] = rotatedProfile;
        SetSelectedProfile(rotatedProfile);
        MapControl.SetImageRotation(rotatedProfile.ImageRotationQuarterTurns);
        PersistSettings();
        SetStatus(quarterTurnDelta < 0
            ? "マップを左へ90度回転しました。"
            : "マップを右へ90度回転しました。");
    }

    private void OnFitMapClick(object sender, RoutedEventArgs e) => MapControl.FitToView();

    private void OnCorrectionModeClick(object sender, RoutedEventArgs e)
    {
        if (MapControl.IsMarkerCorrectionEnabled)
        {
            CancelCorrectionPreview("位置補正をキャンセルしました。");
            return;
        }

        if (_stateCoordinator.SelectedProfile is not { } profile
            || _stateCoordinator.State.WorldPosition is not { } position
            || _stateCoordinator.State.MarkerPosition is null)
        {
            SetStatus("補正する現在位置が表示されていません。");
            return;
        }

        if (!PositionCorrectionSession.TryCreate(
            profile,
            new WorldPoint(position.X, position.Z),
            out var session))
        {
            SetStatus("置き換える校正点を選べませんでした。マップを追加し直してください。");
            return;
        }

        _correctionSession = session;
        MapControl.ShowCalibrationAnchors(profile.CalibrationPoints, session.ReplacementIndex);
        SetCorrectionMode(true);
        SetStatus(
            $"黄色の校正点 {session.ReplacementIndex + 1} が置き換わります。赤い現在位置マーカーを正しい位置へドラッグしてください。");
    }

    private void OnMarkerCorrectionRequested(
        object? sender,
        MarkerCorrectionRequestedEventArgs e)
    {
        var session = _correctionSession;
        if (session is null)
        {
            ResetCorrectionMode();
            ApplyStateToView("位置補正を開始し直してください。");
            return;
        }

        if (!session.TryPreview(e.ImagePixel) || session.PendingProfile is not { } previewProfile)
        {
            ApplyStateToView("補正点から有効な校正を計算できませんでした。");
            return;
        }

        MapControl.ShowCalibrationAnchors(
            previewProfile.CalibrationPoints,
            session.ReplacementIndex);
        SetBundledMapMarkers(previewProfile);
        ConfirmCorrectionButton.Visibility = Visibility.Visible;
        ConfirmCorrectionButton.IsEnabled = true;
        SetStatus("補正位置をプレビューしています。「補正を確定」で保存するか、「補正をキャンセル」で元に戻せます。");
    }

    private void OnConfirmCorrectionClick(object sender, RoutedEventArgs e)
    {
        var session = _correctionSession;
        if (session is null || !session.TryConfirm(out var correctedProfile))
        {
            SetStatus("確定する補正位置がありません。");
            return;
        }

        var profileIndex = _profiles.IndexOf(session.OriginalProfile);
        if (profileIndex < 0 || !_stateCoordinator.TryUpdateSelectedProfile(correctedProfile))
        {
            CancelCorrectionPreview("選択中のプロファイルが変わったため補正を適用できませんでした。");
            return;
        }

        _profiles[profileIndex] = correctedProfile;
        SetSelectedProfile(correctedProfile);
        SetBundledMapMarkers(correctedProfile);
        _correctionSession = null;
        SetCorrectionMode(false);
        ApplyStateToView(
            $"位置補正を保存しました。校正点 {session.ReplacementIndex + 1} を置き換えました。");
        PersistSettings();
    }

    private void OnCalibrationAnchorSelected(
        object? sender,
        CalibrationAnchorSelectedEventArgs e)
    {
        var session = _correctionSession;
        if (session is null || !session.TrySelectReplacement(e.AnchorIndex))
        {
            SetStatus("校正点を選択できませんでした。");
            return;
        }

        ConfirmCorrectionButton.Visibility = Visibility.Collapsed;
        ConfirmCorrectionButton.IsEnabled = false;
        ApplyStateToView(
            $"校正点 {session.ReplacementIndex + 1} を置換します。赤い現在位置マーカーを正しい位置へドラッグしてください。");
        MapControl.ShowCalibrationAnchors(
            session.OriginalProfile.CalibrationPoints,
            session.ReplacementIndex);
        SetBundledMapMarkers(session.OriginalProfile);
    }

    private async Task<string?> PromptForProfileNameAsync()
    {
        var input = new TextBox { PlaceholderText = "例: Woods" };
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "新しいマッププロファイル",
            Content = input,
            PrimaryButtonText = "次へ",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        var name = input.Text.Trim();
        if (name.Length == 0)
        {
            SetStatus("プロファイル名を入力してください。");
            return null;
        }

        return name;
    }

    private void OnObservationAccepted(PositionObservation observation, string fileName)
    {
        var observationEpoch = _stateCoordinator.Epoch;
        EnqueueOnUi(() =>
        {
            if (observationEpoch != _stateCoordinator.Epoch)
            {
                return;
            }

            var session = _progressiveCalibrationSession;
            if (session is not null)
            {
                if (!session.TryStage(new WorldPoint(observation.Position.X, observation.Position.Z)))
                {
                    SetStatus("先に検知した位置をマップ上でクリックしてください。");
                    return;
                }

                _pendingCalibrationObservation = observation;
                _pendingCalibrationFileName = fileName;
                MapControl.SetMarker(null, null);
                CoordinatesText.Text = FormatCoordinates(observation);
                FileNameText.Text = fileName;
                UpdateProgressiveCalibrationPrompt(waitingForMapClick: true);
                CorrectionModeButton.IsEnabled = false;
                SetStatus("検知した現在位置を、マップ上でクリックしてください。");
                return;
            }

            _notificationPresenter.Accept(observation, fileName, observationEpoch);
            ApplyStateToView();
        });
    }

    private void OnFileNameRejected(string fileName)
    {
        EnqueueOnUi(() =>
        {
            SetStatus(_notificationPresenter.RejectFileName(fileName));
        });
    }

    private void OnMonitoringFailed(Exception exception)
    {
        EnqueueOnUi(() => SetStatus(_notificationPresenter.MonitoringFailed(exception)));
    }

    private void EnqueueOnUi(Action action)
    {
        if (_isClosed)
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
        {
            if (!_isClosed)
            {
                action();
            }
        });
    }

    private void ApplyStateToView(string? messageOverride = null)
    {
        var state = _stateCoordinator.State;
        MapControl.SetMarker(state.MarkerPosition, state.MarkerDirection);
        CoordinatesText.Text = state.WorldPosition is { } position
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"X, Y, Z: {position.X:0.#####}, {position.Y:0.#####}, {position.Z:0.#####}")
            : "X, Y, Z: —";
        FileNameText.Text = state.FileName ?? "—";
        StatusText.Text = messageOverride ?? StatusMessage(state.Status);
        CorrectionModeButton.IsEnabled =
            PositionCorrectionAvailability.IsAvailable(_selectedProfile, _bundledProfiles)
            &&
            _progressiveCalibrationSession is null
            && _stateCoordinator.SelectedProfile is not null
            && state.WorldPosition is not null
            && state.MarkerPosition is not null;
    }

    private void UpdateCorrectionModeButtonAvailability(MapProfile? profile)
    {
        var available = PositionCorrectionAvailability.IsAvailable(profile, _bundledProfiles);
        CorrectionModeButton.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        if (!available)
        {
            CorrectionModeButton.IsEnabled = false;
        }
    }

    private void SetCorrectionMode(bool enabled)
    {
        MapControl.IsMarkerCorrectionEnabled = enabled;
        CorrectionModeButton.Content = enabled ? "補正をキャンセル" : "位置を補正";
        if (!enabled)
        {
            MapControl.HideCalibrationAnchors();
            ConfirmCorrectionButton.Visibility = Visibility.Collapsed;
            ConfirmCorrectionButton.IsEnabled = false;
        }
    }

    private void CancelCorrectionPreview(string message)
    {
        ResetCorrectionMode();
        ApplyStateToView(message);
    }

    private void ResetCorrectionMode()
    {
        _correctionSession?.Cancel();
        _correctionSession = null;
        SetCorrectionMode(false);
        SetBundledMapMarkers(_selectedProfile);
    }

    private void SetStatus(string message) => StatusText.Text = message;

    private string? PersistSettings()
    {
        var selectedName = _selectedProfile?.DisplayName;
        var result = _settingsRepository.Save(new AppSettings(_watchDirectory, _profiles.ToArray(), selectedName, _bundledMapCatalogVersion));
        if (!result.IsSuccess)
        {
            var message = $"設定を保存できません。{result.ErrorMessage}";
            SetStatus(message);
            return message;
        }

        return null;
    }

    private void ReplaceSelectedProfile(MapProfile profile, bool calibrationValid)
    {
        if (_selectedProfile is null)
        {
            return;
        }

        var profileIndex = _profiles.IndexOf(_selectedProfile);
        if (profileIndex < 0)
        {
            return;
        }

        _profiles[profileIndex] = profile;
        SetSelectedProfile(profile);
        _stateCoordinator.SelectProfile(profile, FingerprintFor(profile), calibrationValid);
        SetBundledMapMarkers(calibrationValid ? profile : null);
    }

    private void SetBundledMapMarkers(MapProfile? profile)
    {
        if (profile is not null &&
            profile.CalibrationPoints.Count == 3 &&
            IsStoredTransformValid(profile.Transform) &&
            _bundledMapMarkers.TryGetValue(profile.DisplayName, out var markers))
        {
            MapControl.SetMapMarkers(markers, profile.Transform);
            return;
        }

        MapControl.SetMapMarkers([], default);
    }

    private void ResetProgressiveCalibration()
    {
        _progressiveCalibrationSession = null;
        _pendingCalibrationObservation = null;
        _pendingCalibrationFileName = null;
        ProgressiveCalibrationPanel.Visibility = Visibility.Collapsed;
    }

    private void UpdateProgressiveCalibrationPrompt(bool waitingForMapClick)
    {
        if (_progressiveCalibrationSession is not { } session)
        {
            ProgressiveCalibrationPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var completedCount = session.Profile.CalibrationPoints.Count;
        ProgressiveCalibrationPanel.Visibility = Visibility.Visible;
        ProgressiveCalibrationProgressText.Text = $"マップ校正 {completedCount}/3";
        ProgressiveCalibrationInstructionText.Text = waitingForMapClick
            ? $"地点 {completedCount + 1}/3: 検知した位置をマップ上でクリックしてください。"
            : $"地点 {completedCount + 1}/3: ゲーム内でスクリーンショットを撮ってください。";
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _isClosed = true;
        _imageLoadTracker.Close();
        _screenshotMonitor.ObservationAccepted -= OnObservationAccepted;
        _screenshotMonitor.FileNameRejected -= OnFileNameRejected;
        _screenshotMonitor.MonitoringFailed -= OnMonitoringFailed;
        _screenshotMonitor.Dispose();
        MapControl.ImagePixelClicked -= OnMapImagePixelClicked;
        MapControl.MarkerCorrectionRequested -= OnMarkerCorrectionRequested;
        MapControl.CalibrationAnchorSelected -= OnCalibrationAnchorSelected;
        MapControl.Dispose();
    }

    private static bool NamesEqual(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static ImageFingerprint FingerprintFor(MapProfile profile) => new(
        profile.ImagePath,
        profile.CalibratedImageWidth,
        profile.CalibratedImageHeight,
        profile.ImageSha256);

    private static string CalibrationWaitingMessage(int completedCount) =>
        $"校正 {completedCount}/3。離れた場所でスクリーンショットを撮ってください。";

    private static string FormatCoordinates(PositionObservation observation) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"X, Y, Z: {observation.Position.X:0.#####}, {observation.Position.Y:0.#####}, {observation.Position.Z:0.#####}");

    private static bool IsStoredTransformValid(AffineTransform2D transform)
    {
        var determinant = (transform.M11 * transform.M22) - (transform.M12 * transform.M21);
        return double.IsFinite(transform.M11)
            && double.IsFinite(transform.M12)
            && double.IsFinite(transform.M21)
            && double.IsFinite(transform.M22)
            && double.IsFinite(transform.TranslationX)
            && double.IsFinite(transform.TranslationY)
            && double.IsFinite(determinant)
            && Math.Abs(determinant) > AffineCalibration.MinimumAbsoluteLinearDeterminant;
    }

    private static string StatusMessage(MainViewStatus status) => status switch
    {
        MainViewStatus.WaitingForObservation => "次の有効なスクリーンショットを待っています。",
        MainViewStatus.PositionAvailable => "最新位置を表示しています。",
        MainViewStatus.ProfileNotSelected => "現在のマップを選択してください。",
        MainViewStatus.ParseError => "スクリーンショットのファイル名を解析できません。",
        MainViewStatus.SettingsError => "設定を読み書きできません。",
        MainViewStatus.ImageError => "マップ画像が校正時と一致しないか、読み込めません。マップを追加し直してください。",
        MainViewStatus.CalibrationError => "校正が無効です。マップを追加し直してください。",
        _ => "状態を確認できません。",
    };

}
