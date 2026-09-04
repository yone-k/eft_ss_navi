using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using EftSsNavi.App.About;
using EftSsNavi.App.Controls;
using EftSsNavi.App.Imaging;
using EftSsNavi.App.Monitoring;
using EftSsNavi.App.Pickers;
using EftSsNavi.App.Presentation;
using EftSsNavi.Core.Calibration;
using EftSsNavi.Core.Images;
using EftSsNavi.Core.Monitoring;
using EftSsNavi.Core.Observations;
using EftSsNavi.Core.Presentation;
using EftSsNavi.Core.Settings;
using EftSsNavi.Sharing.Coordination;
using EftSsNavi.Sharing.Protocol;
using EftSsNavi.Sharing.Session;
using EftSsNavi.Sharing.Signaling;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Ellipse = Microsoft.UI.Xaml.Shapes.Ellipse;

namespace EftSsNavi.App;

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
    private readonly DispatcherQueueTimer _partyRefreshTimer;
    private readonly CancellationTokenSource _manualUpdateCancellation = new();
    private readonly SemaphoreSlim _manualUpdateGate = new(1, 1);
    private IPartyCoordinator? _partyCoordinator;
    private PartyCoordinatorState _partyState = PartyCoordinatorState.Empty;
    private long _partyStateGeneration;
    private CancellationTokenSource? _partyOperationCancellation;
    private Task? _partyOperationTask;
    private bool _partyProjectionCalibrationValid;
    private string? _partyDisplayName;
    private string? _signalingWorkerUrl;
    private IReadOnlyList<string> _stunServers = ["stun:stun.l.google.com:19302"];
    private bool _partyCloseResumed;
    private bool _partyCloseInProgress;
    private bool _suppressNextSessionEnded;
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
            "EftSsNavi",
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
        _partyRefreshTimer = DispatcherQueue.CreateTimer();
        _partyRefreshTimer.Interval = PartyUiState.RefreshInterval;
        _partyRefreshTimer.Tick += OnPartyRefreshTimerTick;
        _partyRefreshTimer.Start();
        _profiles.CollectionChanged += OnProfilesChanged;
        AppWindow.Closing += OnAppWindowClosing;
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
        ConfigureParty(settings ?? new AppSettings(null, [], null));

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

    private async void OnChangeWatchDirectoryMenuClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "スクリーンショット監視先",
            Content = _watchDirectory ?? "未設定",
            PrimaryButtonText = "変更",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await ChooseWatchDirectoryAsync();
    }

    private async Task ChooseWatchDirectoryAsync()
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

    private void OnProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RebuildMapSelectionMenu();

    private void RebuildMapSelectionMenu()
    {
        SelectMapMenu.Items.Clear();
        if (_profiles.Count == 0)
        {
            SelectMapMenu.Items.Add(new MenuFlyoutItem
            {
                Text = "マップが登録されていません",
                IsEnabled = false,
            });
            return;
        }

        foreach (var profile in _profiles.OrderBy(
                     profile => profile.DisplayName,
                     StringComparer.OrdinalIgnoreCase))
        {
            var item = new RadioMenuFlyoutItem
            {
                Text = profile.DisplayName,
                Tag = profile,
                GroupName = "MapProfiles",
                IsChecked = NamesEqual(profile.DisplayName, _selectedProfile?.DisplayName),
            };
            item.Click += OnProfileMenuItemClick;
            SelectMapMenu.Items.Add(item);
        }
    }

    private void SetSelectedProfile(MapProfile? profile)
    {
        _selectedProfile = profile;
        ProfileMenuButton.Content = profile?.DisplayName ?? "マップを選択";
        RotateMapLeftButton.IsEnabled = profile is not null;
        RotateMapRightButton.IsEnabled = profile is not null;
        RotateMapLeftMenuItem.IsEnabled = profile is not null;
        RotateMapRightMenuItem.IsEnabled = profile is not null;
        UpdateCorrectionMenuAvailability(profile);
        RebuildMapSelectionMenu();
    }

    private async Task ActivateProfileAsync(MapProfile profile, bool persist)
    {
        ResetCorrectionMode();
        ResetProgressiveCalibration();
        _partyProjectionCalibrationValid = false;
        if (_partyCoordinator?.State.Role == PartyCoordinatorRole.Host)
        {
            await NotifyHostMapChangedAsync(profile.DisplayName);
        }

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
            _partyProjectionCalibrationValid = calibrationValid;
            SetBundledMapMarkers(profile);
            MapControl.FitToView();
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
        SetBundledMapMarkers(null);
        _profiles.Add(profile);
        SetSelectedProfile(profile);
        _progressiveCalibrationSession = new ProgressiveCalibrationSession(profile);
        _stateCoordinator.SelectProfile(profile, loadResult.Image.Fingerprint, calibrationValid: false);
        UpdateProgressiveCalibrationPrompt(waitingForMapClick: false);
        ApplyStateToView("マップを追加しました。スクリーンショットを検知すると校正位置を尋ねます。");
        PersistSettings();
        await NotifyHostMapChangedAsync(profile.DisplayName);
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
        if (_partyCoordinator?.State.Role == PartyCoordinatorRole.Host)
        {
            await NotifyHostMapChangedAsync(null);
        }

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
        ConfirmCorrectionMenuItem.Visibility = Visibility.Visible;
        ConfirmCorrectionMenuItem.IsEnabled = true;
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
        ConfirmCorrectionMenuItem.Visibility = Visibility.Collapsed;
        ConfirmCorrectionMenuItem.IsEnabled = false;
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
                StartCorrectionMenuItem.IsEnabled = false;
                SetStatus("検知した現在位置を、マップ上でクリックしてください。");
                return;
            }

            _notificationPresenter.Accept(observation, fileName, observationEpoch);
            if (_partyCoordinator?.State.Role is PartyCoordinatorRole.Host or PartyCoordinatorRole.Participant)
            {
                _ = SendPartyPositionAsync(observation);
            }

            ApplyStateToView();
        });
    }

    private async Task SendPartyPositionAsync(PositionObservation observation)
    {
        var coordinator = _partyCoordinator;
        if (coordinator is null)
        {
            return;
        }

        try
        {
            var direction = observation.HorizontalForward;
            await coordinator.SendPositionAsync(new PartyPosition(
                observation.Position.X,
                observation.Position.Y,
                observation.Position.Z,
                direction?.X,
                direction?.Y,
                observation.CapturedAt.ToUniversalTime(),
                _selectedProfile?.DisplayName));
        }
        catch (Exception exception)
        {
            EnqueueOnUi(() => SetStatus($"位置をグループへ送信できません。{exception.Message}"));
        }
    }

    private void ConfigureParty(AppSettings settings)
    {
        _partyDisplayName = settings.PartyDisplayName;
        _signalingWorkerUrl = settings.SignalingWorkerUrl;
        _stunServers = settings.StunServers.ToArray();
        PartyDisplayNameTextBox.Text = _partyDisplayName ?? string.Empty;

        _partyCoordinator = PartyCoordinatorFactory.Create(settings, TimeProvider.System);
        _partyCoordinator.StateChanged += OnPartyStateChanged;
        ApplyPartyCoordinatorState(_partyCoordinator.State);
    }

    private void OnPartyClick(object sender, RoutedEventArgs e)
    {
        ApplyPartyCoordinatorState(_partyCoordinator?.State ?? PartyCoordinatorState.Empty);
        if (sender is MenuFlyoutItem)
        {
            PartyFlyout.ShowAt(PartyButton, new FlyoutShowOptions
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
            });
        }
    }

    private async void OnHostPartyClick(object sender, RoutedEventArgs e)
    {
        var displayName = GetValidPartyDisplayName();
        if (displayName is null || _partyCoordinator is null)
        {
            return;
        }

        SetPartyActionsEnabled(false);
        try
        {
            _partyDisplayName = displayName;
            PersistSettings();
            await TrackPartyOperationAsync(
                cancellationToken => _partyCoordinator.StartHostAsync(
                    displayName,
                    _selectedProfile?.DisplayName,
                    cancellationToken));
            SetStatus("グループを開始しました。");
        }
        catch (OperationCanceledException) when (_partyCloseInProgress)
        {
            // Closing the window cancels an in-flight connection attempt.
        }
        catch (PartySignalingException exception) when (exception.RejectReason is { } rejectReason)
        {
            SetStatus(PartyStatusMessages.ForSignalingRejection(rejectReason));
        }
        catch (PartySignalingException)
        {
            SetStatus(PartyStatusMessages.HostSignalingFailure);
        }
        catch (Exception exception)
        {
            SetStatus($"グループを開始できません。{exception.Message}");
        }
        finally
        {
            SetPartyActionsEnabled(true);
        }
    }

    private async void OnJoinPartyClick(object sender, RoutedEventArgs e)
    {
        var displayName = GetValidPartyDisplayName();
        if (displayName is null || _partyCoordinator is null)
        {
            return;
        }

        if (!RoomCode.TryNormalize(PartyRoomCodeTextBox.Text, out var roomCode))
        {
            SetStatus("16文字の有効なルームコードを入力してください。");
            return;
        }

        SetPartyActionsEnabled(false);
        try
        {
            _partyDisplayName = displayName;
            PersistSettings();
            await TrackPartyOperationAsync(
                cancellationToken => _partyCoordinator.JoinAsync(displayName, roomCode, cancellationToken));
            ApplyJoinCompletionStatus(_partyCoordinator.State);
        }
        catch (PartyRejectedException exception)
        {
            SetStatus(PartyStatusMessages.ForRejection(exception.Reason));
        }
        catch (TimeoutException)
        {
            SetStatus(PartyStatusMessages.JoinTimeout);
        }
        catch (OperationCanceledException) when (_partyCloseInProgress)
        {
            // Closing the window cancels an in-flight connection attempt.
        }
        catch (PartySignalingException exception) when (exception.RejectReason is { } rejectReason)
        {
            SetStatus(PartyStatusMessages.ForSignalingRejection(rejectReason));
        }
        catch (PartySignalingException exception) when (
            exception.FailureKind == SignalingFailureKind.Timeout)
        {
            SetStatus(PartyStatusMessages.JoinTimeout);
        }
        catch (PartySignalingException)
        {
            SetStatus(PartyStatusMessages.ParticipantSignalingFailure);
        }
        catch (Exception exception)
        {
            SetStatus($"グループに参加できません。{exception.Message}");
        }
        finally
        {
            SetPartyActionsEnabled(true);
        }
    }

    private void OnCopyPartyCodeClick(object sender, RoutedEventArgs e)
    {
        if (_partyCoordinator?.State is not { Role: PartyCoordinatorRole.Host, RoomCode: { } roomCode })
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(RoomCode.Format(roomCode));
        Clipboard.SetContent(package);
        SetStatus("ルームコードをコピーしました。");
    }

    private async void OnReissuePartyCodeClick(object sender, RoutedEventArgs e)
    {
        if (_partyCoordinator?.State.Role != PartyCoordinatorRole.Host)
        {
            return;
        }

        try
        {
            await _partyCoordinator.ReissueRoomCodeAsync();
            SetStatus("ルームコードを再発行しました。");
        }
        catch (PartySignalingException)
        {
            SetStatus(PartyStatusMessages.RoomCodeReissueFailure);
        }
        catch (Exception)
        {
            SetStatus(PartyStatusMessages.RoomCodeReissueFailure);
        }
    }

    private async void OnEndPartyClick(object sender, RoutedEventArgs e)
    {
        if (_partyCoordinator is null)
        {
            return;
        }

        try
        {
            await _partyCoordinator.EndAsync();
            SetStatus("セッションを終了しました。");
        }
        catch (Exception exception)
        {
            SetStatus($"セッションを終了できません。{exception.Message}");
        }
    }

    private async void OnLeavePartyClick(object sender, RoutedEventArgs e)
    {
        if (_partyCoordinator is null)
        {
            return;
        }

        _suppressNextSessionEnded = true;
        try
        {
            await _partyCoordinator.LeaveAsync();
            SetStatus("グループから退出しました。");
        }
        catch (Exception exception)
        {
            _suppressNextSessionEnded = false;
            SetStatus($"グループから退出できません。{exception.Message}");
        }
    }

    private string? GetValidPartyDisplayName()
    {
        var displayName = PartyDisplayNameTextBox.Text.Trim();
        if (displayName.Length is < 1 or > 16)
        {
            SetStatus("表示名は1〜16文字で入力してください。");
            return null;
        }

        return displayName;
    }

    private void SetPartyActionsEnabled(bool enabled)
    {
        HostPartyButton.IsEnabled = enabled;
        JoinPartyButton.IsEnabled = enabled;
        ReissuePartyCodeButton.IsEnabled = enabled;
        EndPartyButton.IsEnabled = enabled;
        LeavePartyButton.IsEnabled = enabled;
    }

    private async Task TrackPartyOperationAsync(Func<CancellationToken, Task> operation)
    {
        using var cancellation = new CancellationTokenSource();
        _partyOperationCancellation = cancellation;
        var task = operation(cancellation.Token);
        _partyOperationTask = task;
        try
        {
            await task;
        }
        finally
        {
            if (ReferenceEquals(_partyOperationTask, task))
            {
                _partyOperationTask = null;
                _partyOperationCancellation = null;
            }
        }
    }

    private void ApplyJoinCompletionStatus(PartyCoordinatorState state)
    {
        var hasMatchingProfile = state.MapName is not null
            && _profiles.Any(profile => NamesEqual(profile.DisplayName, state.MapName));
        var mapStatus = PartyStatusMessagesForMap(state.MapName, hasMatchingProfile);
        SetStatus(string.IsNullOrEmpty(mapStatus) ? "グループに参加しました。" : mapStatus);
    }

    private void OnPartyStateChanged(PartyCoordinatorState state)
    {
        EnqueueOnUi(() =>
        {
            var generation = ++_partyStateGeneration;
            _ = ApplyPartyCoordinatorStateAsync(state, generation);
        });
    }

    private async Task ApplyPartyCoordinatorStateAsync(PartyCoordinatorState state, long generation)
    {
        var previousState = _partyState;
        var previousRole = previousState.Role;
        string? mapStatus = null;
        if (state.Role == PartyCoordinatorRole.Participant)
        {
            if (state.MapName is null)
            {
                mapStatus = PartyStatusMessagesForMap(state.MapName, hasMatchingProfile: false);
            }
            else if (!NamesEqual(_selectedProfile?.DisplayName, state.MapName))
            {
                var matchingProfile = _profiles.FirstOrDefault(profile => NamesEqual(profile.DisplayName, state.MapName));
                if (matchingProfile is null)
                {
                    mapStatus = PartyStatusMessagesForMap(state.MapName, hasMatchingProfile: false);
                }
                else
                {
                    SetSelectedProfile(matchingProfile);
                    await ActivateProfileAsync(matchingProfile, persist: true);
                }
            }
        }

        if (generation != _partyStateGeneration)
        {
            return;
        }

        if (_partyCoordinator is null || !Equals(state, _partyCoordinator.State))
        {
            return;
        }

        var membershipStatus = PartyStatusMessages.ForMembershipChange(previousState, state);
        ApplyPartyCoordinatorState(state);
        if (!string.IsNullOrEmpty(mapStatus))
        {
            SetStatus(mapStatus);
        }
        if (state.Role == PartyCoordinatorRole.None && previousRole == PartyCoordinatorRole.Participant)
        {
            if (_suppressNextSessionEnded)
            {
                _suppressNextSessionEnded = false;
            }
            else
            {
                SetStatus(PartyStatusMessages.SessionEnded);
            }
        }
        else if (!string.IsNullOrEmpty(membershipStatus))
        {
            SetStatus(membershipStatus);
        }
    }

    private void ApplyPartyCoordinatorState(PartyCoordinatorState state)
    {
        _partyState = state;
        var role = state.Role switch
        {
            PartyCoordinatorRole.Host => PartyUiRole.Host,
            PartyCoordinatorRole.Participant => PartyUiRole.Participant,
            _ => PartyUiRole.NotJoined,
        };
        var hasMatchingProfile = state.MapName is not null
            && _profiles.Any(profile => NamesEqual(profile.DisplayName, state.MapName));
        ApplyPartyUiState(new PartyUiState(role, state.MapName, hasMatchingProfile));
        RefreshPartyView();
    }

    private void ApplyPartyUiState(PartyUiState state)
    {
        ProfileMenuButton.IsEnabled = state.MapActionsEnabled;
        SelectMapMenu.IsEnabled = state.MapActionsEnabled;
        AddMapMenuItem.IsEnabled = state.MapActionsEnabled;
        DeleteMapMenuItem.IsEnabled = state.MapActionsEnabled;
        PartyNotJoinedPanel.Visibility = state.Role == PartyUiRole.NotJoined
            ? Visibility.Visible
            : Visibility.Collapsed;
        PartyHostPanel.Visibility = state.Role == PartyUiRole.Host
            ? Visibility.Visible
            : Visibility.Collapsed;
        PartyParticipantPanel.Visibility = state.Role == PartyUiRole.Participant
            ? Visibility.Visible
            : Visibility.Collapsed;
        PartyNotJoinedText.Visibility = state.Role == PartyUiRole.NotJoined
            ? Visibility.Visible
            : Visibility.Collapsed;
        PartySelfParticipantRow.Visibility = state.Role == PartyUiRole.NotJoined
            ? Visibility.Collapsed
            : Visibility.Visible;
        PartySectionTitleText.Text = state.GroupSectionTitle;
        if (!state.PartyMarkersVisible)
        {
            MapControl.SetPartyMarkers([]);
        }
    }

    private void OnPartyRefreshTimerTick(DispatcherQueueTimer sender, object args) => RefreshPartyView();

    private void RefreshPartyView()
    {
        var state = _partyState;
        if (state.Role == PartyCoordinatorRole.None)
        {
            PartyNotJoinedText.Text = "未参加";
            PartyParticipantList.Items.Clear();
            MapControl.SetPartyMarkers([]);
            return;
        }

        var formattedRoomCode = state.RoomCode is { } roomCode ? RoomCode.Format(roomCode) : null;
        PartyFlyoutRoomCodeText.Text = formattedRoomCode ?? "-------------------";
        PartySelfDisplayNameText.Text = state.LocalDisplayName ?? "自分";
        PartySelfStatusText.Text = "接続中";

        var now = DateTimeOffset.UtcNow;
        var remoteParticipants = state.Participants
            .Where(participant => participant.Id != state.LocalParticipantId)
            .ToArray();
        PartyParticipantList.Items.Clear();
        foreach (var participant in remoteParticipants)
        {
            PartyParticipantList.Items.Add(CreateParticipantRow(participant, now));
        }

        RefreshPartyMarkers(remoteParticipants, now);
    }

    private StackPanel CreateParticipantRow(SessionParticipant participant, DateTimeOffset now)
    {
        var hasPosition = participant.LatestPosition is not null;
        var isOnSelectedMap = hasPosition
            && NamesEqual(participant.LatestPosition!.MapName, _selectedProfile?.DisplayName);
        var age = participant.PositionReceivedAt is { } receivedAt
            ? now - receivedAt
            : TimeSpan.Zero;
        var status = PartyUiState.FormatParticipantPositionStatus(
            hasPosition,
            isOnSelectedMap,
            age,
            participant.LatestPosition?.MapName);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var markerSlot = new Canvas
        {
            Width = 24,
            Height = 20,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var marker = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = new SolidColorBrush(PartyColor(participant.ColorIndex)),
        };
        Canvas.SetLeft(marker, 6);
        Canvas.SetTop(marker, 4);
        markerSlot.Children.Add(marker);
        row.Children.Add(markerSlot);
        row.Children.Add(new TextBlock
        {
            Text = participant.DisplayName,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        row.Children.Add(new TextBlock
        {
            Text = status,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)),
        });
        return row;
    }

    private void RefreshPartyMarkers(
        IReadOnlyList<SessionParticipant> participants,
        DateTimeOffset now)
    {
        var uiState = new PartyUiState(
            _partyState.Role == PartyCoordinatorRole.Host ? PartyUiRole.Host : PartyUiRole.Participant,
            _partyState.MapName,
            _partyState.MapName is not null && NamesEqual(_selectedProfile?.DisplayName, _partyState.MapName));
        if (!uiState.PartyMarkersVisible || _selectedProfile is null)
        {
            MapControl.SetPartyMarkers([]);
            return;
        }

        var markers = new List<PartyMarkerVisual>();
        foreach (var participant in participants)
        {
            if (participant.LatestPosition is not { } position)
            {
                continue;
            }

            WorldPoint? direction = position.ForwardX.HasValue && position.ForwardZ.HasValue
                ? new WorldPoint(position.ForwardX.Value, position.ForwardZ.Value)
                : null;
            var projection = PartyMarkerProjector.Project(
                _selectedProfile,
                _partyProjectionCalibrationValid,
                position.MapName,
                new WorldPoint(position.X, position.Z),
                direction);
            if (projection is not { } projected)
            {
                continue;
            }

            var isStale = participant.PositionReceivedAt is { } receivedAt
                && now - receivedAt > PartySession.StaleThreshold;
            markers.Add(new PartyMarkerVisual(
                participant.DisplayName,
                projected.Position,
                projected.Direction,
                participant.ColorIndex,
                isStale));
        }

        MapControl.SetPartyMarkers(markers);
    }

    private static Windows.UI.Color PartyColor(int colorIndex) => colorIndex switch
    {
        0 => Windows.UI.Color.FromArgb(255, 47, 128, 237),
        1 => Windows.UI.Color.FromArgb(255, 242, 201, 76),
        2 => Windows.UI.Color.FromArgb(255, 155, 81, 224),
        3 => Windows.UI.Color.FromArgb(255, 255, 111, 181),
        4 => Windows.UI.Color.FromArgb(255, 245, 245, 245),
        _ => Windows.UI.Color.FromArgb(255, 255, 255, 255),
    };

    private static string PartyStatusMessagesForMap(string? mapName, bool hasMatchingProfile) =>
        new PartyUiState(PartyUiRole.Participant, mapName, hasMatchingProfile).MapStatusMessage;

    private async Task NotifyHostMapChangedAsync(string? mapName)
    {
        if (_partyCoordinator?.State.Role != PartyCoordinatorRole.Host)
        {
            return;
        }

        try
        {
            await _partyCoordinator.ChangeMapAsync(mapName);
        }
        catch (Exception exception)
        {
            SetStatus($"グループのマップを変更できません。{exception.Message}");
        }
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
        StartCorrectionMenuItem.IsEnabled =
            PositionCorrectionAvailability.IsAvailable(_selectedProfile, _bundledProfiles)
            &&
            _progressiveCalibrationSession is null
            && _stateCoordinator.SelectedProfile is not null
            && state.WorldPosition is not null
            && state.MarkerPosition is not null;
    }

    private void UpdateCorrectionMenuAvailability(MapProfile? profile)
    {
        var available = PositionCorrectionAvailability.IsAvailable(profile, _bundledProfiles);
        StartCorrectionMenuItem.IsEnabled = available;
        if (!available)
        {
            StartCorrectionMenuItem.IsEnabled = false;
        }
    }

    private void SetCorrectionMode(bool enabled)
    {
        MapControl.IsMarkerCorrectionEnabled = enabled;
        StartCorrectionMenuItem.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        CancelCorrectionMenuItem.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        CancelCorrectionMenuItem.IsEnabled = enabled;
        if (!enabled)
        {
            MapControl.HideCalibrationAnchors();
            ConfirmCorrectionButton.Visibility = Visibility.Collapsed;
            ConfirmCorrectionButton.IsEnabled = false;
            ConfirmCorrectionMenuItem.Visibility = Visibility.Collapsed;
            ConfirmCorrectionMenuItem.IsEnabled = false;
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

    private async void OnCheckForUpdatesClick(object sender, RoutedEventArgs e)
    {
        if (!await _manualUpdateGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            if (_isClosed)
            {
                return;
            }

            CheckForUpdatesMenuItem.IsEnabled = false;
            var launcherPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "EftSsNavi.exe"));
            if (!File.Exists(launcherPath))
            {
                await ShowManualUpdateErrorAsync("アップデート用ランチャーが見つかりません。");
                return;
            }

            var shutdownEventName = $"Local\\EftSsNavi.Shutdown.{Guid.NewGuid():N}";
            using var shutdownEvent = new EventWaitHandle(
                initialState: false,
                EventResetMode.ManualReset,
                shutdownEventName);
            using var currentProcess = Process.GetCurrentProcess();
            var startInfo = new ProcessStartInfo(launcherPath)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(launcherPath),
            };
            startInfo.ArgumentList.Add("--manual-update");
            startInfo.ArgumentList.Add("--caller-pid");
            startInfo.ArgumentList.Add(currentProcess.Id.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("--caller-session-id");
            startInfo.ArgumentList.Add(currentProcess.SessionId.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("--caller-path");
            startInfo.ArgumentList.Add(Environment.ProcessPath ?? string.Empty);
            startInfo.ArgumentList.Add("--shutdown-event");
            startInfo.ArgumentList.Add(shutdownEventName);

            using var launcherProcess = Process.Start(startInfo);
            if (launcherProcess is null)
            {
                await ShowManualUpdateErrorAsync("アップデート用ランチャーを起動できませんでした。");
                return;
            }

            using var manualUpdateWaitCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _manualUpdateCancellation.Token);
            var cancellationToken = manualUpdateWaitCancellation.Token;
            var exitTask = launcherProcess.WaitForExitAsync(cancellationToken);
            var shutdownTask = WaitForSignalAsync(shutdownEvent, cancellationToken);
            if (await Task.WhenAny(exitTask, shutdownTask) == shutdownTask)
            {
                if (await shutdownTask)
                {
                    Close();
                }

                await exitTask;
            }
            else
            {
                await exitTask;
                manualUpdateWaitCancellation.Cancel();
                try
                {
                    await shutdownTask;
                }
                catch (OperationCanceledException) when (manualUpdateWaitCancellation.IsCancellationRequested)
                {
                    // The launcher exited without requesting application shutdown.
                }
            }
        }
        catch (OperationCanceledException) when (_manualUpdateCancellation.IsCancellationRequested)
        {
            // Closing the window cancels launcher monitoring.
        }
        catch (Exception exception)
        {
            await ShowManualUpdateErrorAsync($"アップデート用ランチャーを起動できませんでした。{exception.Message}");
        }
        finally
        {
            _manualUpdateGate.Release();
            if (!_isClosed)
            {
                CheckForUpdatesMenuItem.IsEnabled = true;
            }
        }
    }

    private async Task ShowManualUpdateErrorAsync(string message)
    {
        if (_isClosed || RootGrid.XamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "アップデートエラー",
            Content = message,
            CloseButtonText = "閉じる",
        };
        await dialog.ShowAsync();
    }

    private static Task<bool> WaitForSignalAsync(WaitHandle waitHandle, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        RegisteredWaitHandle? registeredWait = null;
        CancellationTokenRegistration cancellationRegistration = default;
        registeredWait = ThreadPool.RegisterWaitForSingleObject(
            waitHandle,
            (_, timedOut) => completion.TrySetResult(!timedOut),
            null,
            Timeout.InfiniteTimeSpan,
            executeOnlyOnce: true);
        cancellationRegistration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        return AwaitAndCleanupAsync(completion.Task, registeredWait, cancellationRegistration);

        static async Task<bool> AwaitAndCleanupAsync(
            Task<bool> task,
            RegisteredWaitHandle registeredWait,
            CancellationTokenRegistration cancellationRegistration)
        {
            try
            {
                return await task;
            }
            finally
            {
                registeredWait.Unregister(null);
                cancellationRegistration.Dispose();
            }
        }
    }

    private async void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var coordinator = AboutCoordinator.CreateDefault(
            () => _isClosed ? null : RootGrid.XamlRoot,
            () => _isClosed);
        await coordinator.ShowAsync(_manualUpdateCancellation.Token);
    }

    private string? PersistSettings()
    {
        var selectedName = _selectedProfile?.DisplayName;
        var result = _settingsRepository.Save(new AppSettings(
            _watchDirectory,
            _profiles.ToArray(),
            selectedName,
            _bundledMapCatalogVersion,
            _partyDisplayName,
            _signalingWorkerUrl,
            _stunServers));
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
        _partyProjectionCalibrationValid = calibrationValid;
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

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_partyCloseResumed)
        {
            return;
        }

        args.Cancel = true;
        if (_partyCloseInProgress)
        {
            return;
        }

        _partyCloseInProgress = true;
        _partyOperationCancellation?.Cancel();
        if (_partyOperationTask is { } partyOperationTask)
        {
            try
            {
                await partyOperationTask;
            }
            catch
            {
                // The operation was canceled so party cleanup can proceed.
            }
        }

        if (_partyCoordinator is not null)
        {
            _suppressNextSessionEnded = true;
            try
            {
                await _partyCoordinator.EndAsync();
            }
            catch
            {
                // Window shutdown continues after best-effort host notification.
            }

            try
            {
                await _partyCoordinator.LeaveAsync();
            }
            catch
            {
                // Window shutdown continues after best-effort participant notification.
            }

            try
            {
                await _partyCoordinator.DisposeAsync();
            }
            catch
            {
                // Window shutdown continues after best-effort resource cleanup.
            }
        }

        _partyCloseResumed = true;
        Close();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _isClosed = true;
        _profiles.CollectionChanged -= OnProfilesChanged;
        _manualUpdateCancellation.Cancel();
        _manualUpdateCancellation.Dispose();
        AppWindow.Closing -= OnAppWindowClosing;
        _partyRefreshTimer.Stop();
        _partyRefreshTimer.Tick -= OnPartyRefreshTimerTick;
        if (_partyCoordinator is not null)
        {
            _partyCoordinator.StateChanged -= OnPartyStateChanged;
            _ = _partyCoordinator.DisposeAsync();
        }

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
