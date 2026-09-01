namespace EftSsMap.App.Presentation;

public static class StartupStatusResolver
{
    public const string ChooseWatchDirectoryMessage =
        "監視先が未設定です。［変更］からスクリーンショットフォルダーを選択してください。";

    public const string ChooseProfileMessage = "現在のマップを選択してください。";

    public static string Resolve(
        string? settingsFailureMessage,
        string? watchFailureMessage,
        bool watchDirectoryConfigured) =>
        settingsFailureMessage
        ?? watchFailureMessage
        ?? (watchDirectoryConfigured ? ChooseProfileMessage : ChooseWatchDirectoryMessage);
}
