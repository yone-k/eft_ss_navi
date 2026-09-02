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

namespace EftSsMap.App;

public sealed partial class MainWindow : Window
{
    private readonly MapCanvas MapControl;
    private readonly ObservableCollection<MapProfile> _profiles = [];
    private readonly IFilePickerService _pickerService = new FilePickerService();
    private readonly MainStateCoordinator _stateCoordinator = new();
    private readonly ScreenshotNotificationPresenter _notificationPresenter;
    private readonly SettingsFileSystem _settingsFileSystem = new();
    private readonly LatestImageLoadTracker _imageLoadTracker = new();
    private readonly ScreenshotMonitor _screenshotMonitor;
    private readonly SettingsRepository _settingsRepository;
    private readonly string _settingsPath;
    private CalibrationDraft? _calibrationDraft;
    private PositionCorrectionSession? _correctionSession;
    private string? _watchDirectory;
    private bool _initialized;
    private bool _suppressProfileSelection;
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
        ProfileComboBox.ItemsSource = _profiles;
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
        if (_settingsFileSystem.FileExists(_settingsPath))
        {
            var loadResult = _settingsRepository.Load();
            if (loadResult.IsSuccess)
            {
                settings = loadResult.Value;
            }
            else
            {
                settingsFailureMessage =
                    $"設定を読み込めません。元の設定ファイルは上書きしません。{loadResult.ErrorMessage}";
            }
        }

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

        var lastSelected = settings?.LastSelectedProfileName;
        var restoredProfile = _profiles.FirstOrDefault(profile => NamesEqual(profile.DisplayName, lastSelected));
        if (restoredProfile is not null)
        {
            _suppressProfileSelection = true;
            ProfileComboBox.SelectedItem = restoredProfile;
            _suppressProfileSelection = false;
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

    private async void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressProfileSelection || ProfileComboBox.SelectedItem is not MapProfile profile)
        {
            return;
        }

        CancelCalibration(clearMap: false);
        await ActivateProfileAsync(profile, persist: true);
    }

    private async Task ActivateProfileAsync(MapProfile profile, bool persist)
    {
        ResetCorrectionMode();
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
                calibrationValid: true);
            ApplyStateToView(loadResult.ErrorMessage ?? "マップ画像を読み込めません。再校正してください。");
            if (persist)
            {
                PersistSettings();
            }

            return;
        }

        var image = loadResult.Image;
        MapControl.SetImage(image);
        var calibrationValid = IsStoredTransformValid(profile.Transform);
        _stateCoordinator.SelectProfile(profile, image.Fingerprint, calibrationValid);
        var validation = ProfileImageValidator.Validate(FingerprintFor(profile), image.Fingerprint);
        ApplyStateToView(validation == ProfileImageValidationResult.Match && calibrationValid
            ? "マップを選択しました。次の有効なスクリーンショットを待っています。"
            : "画像または校正情報が校正時と一致しません。再校正してください。");
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

        var imageResult = await _pickerService.PickMapImageAsync(this);
        if (!imageResult.IsSuccess)
        {
            SetStatus(imageResult.ErrorMessage ?? "マップ画像を選択できませんでした。");
            return;
        }

        if (!imageResult.IsCanceled && imageResult.Path is not null)
        {
            await BeginCalibrationAsync(displayName, imageResult.Path, replaceIndex: null);
        }
    }

    private async void OnRecalibrateClick(object sender, RoutedEventArgs e)
    {
        if (ProfileComboBox.SelectedItem is not MapProfile selected)
        {
            SetStatus("再校正するマッププロファイルを選択してください。");
            return;
        }

        var imageResult = await _pickerService.PickMapImageAsync(this);
        if (!imageResult.IsSuccess)
        {
            SetStatus(imageResult.ErrorMessage ?? "マップ画像を選択できませんでした。");
            return;
        }

        if (!imageResult.IsCanceled && imageResult.Path is not null)
        {
            await BeginCalibrationAsync(selected.DisplayName, imageResult.Path, _profiles.IndexOf(selected));
        }
    }

    private async Task BeginCalibrationAsync(string displayName, string imagePath, int? replaceIndex)
    {
        ResetCorrectionMode();
        var generation = _imageLoadTracker.Begin();
        var loadResult = await SkiaMapImageLoader.LoadAsync(imagePath);
        if (!_imageLoadTracker.IsCurrent(generation))
        {
            loadResult.Image?.Dispose();
            return;
        }

        if (!loadResult.IsSuccess || loadResult.Image is null)
        {
            SetStatus(loadResult.ErrorMessage ?? "校正用マップ画像を読み込めませんでした。");
            return;
        }

        _suppressProfileSelection = true;
        ProfileComboBox.SelectedItem = null;
        _suppressProfileSelection = false;
        if (_stateCoordinator.SelectedProfile is { } previousProfile)
        {
            _stateCoordinator.DeleteProfile(previousProfile.DisplayName);
        }

        MapControl.SetImage(loadResult.Image);
        MapControl.SetMarker(null, null);
        _calibrationDraft = new CalibrationDraft(displayName, loadResult.Image.Fingerprint, replaceIndex);
        CalibrationPanel.Visibility = Visibility.Visible;
        ChooseCalibrationScreenshotButton.IsEnabled = true;
        UpdateCalibrationPrompt();
        SetStatus("3地点校正を開始しました。");
    }

    private async void OnChooseCalibrationScreenshotClick(object sender, RoutedEventArgs e)
    {
        if (_calibrationDraft is null)
        {
            return;
        }

        var result = await _pickerService.PickCalibrationScreenshotAsync(this);
        if (!result.IsSuccess)
        {
            SetStatus(result.ErrorMessage ?? "校正用スクリーンショットを選択できませんでした。");
            return;
        }

        if (result.IsCanceled || result.Path is null)
        {
            return;
        }

        var fileName = Path.GetFileName(result.Path);
        if (!ScreenshotFileNameParser.TryParse(fileName, out var observation) || observation is null)
        {
            SetStatus($"校正用スクリーンショットのファイル名を解析できません: {fileName}");
            return;
        }

        _calibrationDraft.PendingWorldPoint = new WorldPoint(observation.Position.X, observation.Position.Z);
        ChooseCalibrationScreenshotButton.IsEnabled = false;
        CalibrationStepText.Text = $"地点 {_calibrationDraft.Points.Count + 1}/3: マップ上の同じ地点をクリックしてください。";
    }

    private void OnMapImagePixelClicked(object? sender, MapImagePixelClickedEventArgs e)
    {
        if (_calibrationDraft?.PendingWorldPoint is not { } worldPoint)
        {
            return;
        }

        _calibrationDraft.Points.Add(new CalibrationPoint(worldPoint, e.ImagePixel));
        _calibrationDraft.PendingWorldPoint = null;
        if (_calibrationDraft.Points.Count < 3)
        {
            ChooseCalibrationScreenshotButton.IsEnabled = true;
            UpdateCalibrationPrompt();
            return;
        }

        CompleteCalibration();
    }

    private void CompleteCalibration()
    {
        var draft = _calibrationDraft;
        if (draft is null || !AffineCalibration.TryCreate(draft.Points, out var transform))
        {
            if (draft is not null)
            {
                draft.Points.Clear();
                draft.PendingWorldPoint = null;
            }

            ChooseCalibrationScreenshotButton.IsEnabled = true;
            UpdateCalibrationPrompt();
            SetStatus("3地点が重複、同一直線上、または変換が退化しています。互いに離れた別の3地点を指定してください。");
            return;
        }

        var fingerprint = draft.Fingerprint;
        var profile = new MapProfile(
            draft.DisplayName,
            fingerprint.Path,
            fingerprint.Width,
            fingerprint.Height,
            fingerprint.Sha256,
            draft.Points.ToArray(),
            transform);
        if (draft.ReplaceIndex is { } index && index >= 0 && index < _profiles.Count)
        {
            _profiles[index] = profile;
        }
        else
        {
            _profiles.Add(profile);
        }

        _calibrationDraft = null;
        CalibrationPanel.Visibility = Visibility.Collapsed;
        _suppressProfileSelection = true;
        ProfileComboBox.SelectedItem = profile;
        _suppressProfileSelection = false;
        _stateCoordinator.SelectProfile(profile, fingerprint, calibrationValid: true);
        ApplyStateToView("校正を保存しました。次の有効なスクリーンショットを待っています。");
        PersistSettings();
    }

    private async void OnDeleteProfileClick(object sender, RoutedEventArgs e)
    {
        if (ProfileComboBox.SelectedItem is not MapProfile selected)
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
        _suppressProfileSelection = true;
        ProfileComboBox.SelectedItem = null;
        _suppressProfileSelection = false;
        _stateCoordinator.DeleteProfile(selected.DisplayName);
        MapControl.SetImage(null);
        ApplyStateToView("プロファイルを削除しました。現在のマップを選択してください。");
        PersistSettings();
    }

    private void OnCancelCalibrationClick(object sender, RoutedEventArgs e)
    {
        CancelCalibration(clearMap: true);
        SetStatus("校正をキャンセルしました。現在のマップを選択してください。");
    }

    private void CancelCalibration(bool clearMap)
    {
        if (_calibrationDraft is null)
        {
            return;
        }

        _calibrationDraft = null;
        CalibrationPanel.Visibility = Visibility.Collapsed;
        if (clearMap)
        {
            MapControl.SetImage(null);
        }
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
            SetStatus("置き換える校正点を選べませんでした。再校正してください。");
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
            _calibrationDraft is null
            && _stateCoordinator.SelectedProfile is not null
            && state.WorldPosition is not null
            && state.MarkerPosition is not null;
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
    }

    private void SetStatus(string message) => StatusText.Text = message;

    private string? PersistSettings()
    {
        var selectedName = (ProfileComboBox.SelectedItem as MapProfile)?.DisplayName;
        var result = _settingsRepository.Save(new AppSettings(_watchDirectory, _profiles.ToArray(), selectedName));
        if (!result.IsSuccess)
        {
            var message = $"設定を保存できません。{result.ErrorMessage}";
            SetStatus(message);
            return message;
        }

        return null;
    }

    private void UpdateCalibrationPrompt()
    {
        if (_calibrationDraft is not null)
        {
            CalibrationStepText.Text = $"地点 {_calibrationDraft.Points.Count + 1}/3: EFTスクリーンショットを選択してください。";
        }
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
        MainViewStatus.ImageError => "マップ画像が校正時と一致しないか、読み込めません。再校正してください。",
        MainViewStatus.CalibrationError => "校正が無効です。再校正してください。",
        _ => "状態を確認できません。",
    };

    private sealed class CalibrationDraft(
        string displayName,
        ImageFingerprint fingerprint,
        int? replaceIndex)
    {
        public string DisplayName { get; } = displayName;

        public ImageFingerprint Fingerprint { get; } = fingerprint;

        public int? ReplaceIndex { get; } = replaceIndex;

        public List<CalibrationPoint> Points { get; } = [];

        public WorldPoint? PendingWorldPoint { get; set; }
    }
}
