# EFT Screenshot Map

Escape from Tarkov（EFT）が生成するスクリーンショットの**ファイル名**から現在座標とカメラ方向を読み取り、ユーザーが選択・校正したローカルマップ画像上へ最新位置を表示するWindowsアプリです。

スクリーンショット画像の内容、ゲームプロセス、ゲームメモリ、入力、ネットワークにはアクセスしません。マップ画像や座標データは同梱していません。

## 動作環境

- Windows 11 x64
- 入力するマップ画像: PNG、JPEG、WebP
- [.NET 10 Runtime（x64）](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Windows App SDK Runtime 2.4（x64）](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)
- [Microsoft Visual C++ Redistributable（x64）](https://aka.ms/vs/17/release/vc_redist.x64.exe)

Windows 10、Windows on ARM、x86、MSIX、インストーラー、単一EXEには対応していません。

## 初回設定

1. アプリを起動します。
2. `%USERPROFILE%\Documents\Escape from Tarkov\Screenshots` が存在する場合、自動的に監視を開始します。
3. 既定フォルダーが存在しない場合、上部の「監視先」右にある［変更］からEFTのスクリーンショットフォルダーを選択します。
4. 有効なフォルダーが確定すると、開始ボタンを押さずに監視が始まります。

監視先を変更すると、それまで表示していた位置は消去されます。新しい監視先へ切り替えた後に作成された次の有効なPNGまで位置は表示されません。監視開始前から存在するファイルは自動処理しません。

## マッププロファイルと3地点校正

EFTのスクリーンショット名だけではマップを判定できないため、現在のマップと座標対応をユーザーが指定します。

1. ［新規］を押し、大文字・小文字を含めて識別しやすいプロファイル名を入力します。名前の一意性判定では大文字・小文字を区別しません。
2. ローカルのPNG、JPEG、WebPマップ画像を選択します。
3. 「地点 1/3」で、その地点を撮影した既存EFTスクリーンショットを選択します。アプリは画像内容を開かず、ファイル名の`X, Z`だけを使います。
4. 選択したスクリーンショットと同じ地点をマップ上でクリックします。
5. 地点2、地点3でも同じ操作を繰り返します。
6. 3地点から有効なアフィン変換を算出できると、プロファイルが保存・選択されます。

校正点は、互いに離れた識別しやすい地点を選び、重複または同一直線上になる組み合わせを避けてください。重複・共線・退化した変換は保存されず、3地点の指定をやり直します。3地点校正はコミュニティ製画像の局所的な歪みや地点選択誤差を補正しません。

［再校正］では選択中プロファイルのマップ画像を選び直し、3地点を再指定します。［削除］では確認後に校正情報だけを削除し、元のマップ画像は削除しません。

## 現在位置を更新する

1. 上部の「現在のマップ」からプレイ中のマッププロファイルを明示的に選択します。
2. EFTで新しいスクリーンショットを撮影します。
3. 有効なファイル名を検出すると、最新位置の点と方向矢印、`X, Y, Z`、処理ファイル名を表示します。

マウスホイールでズーム、左ドラッグでパン、［全体表示］で画像全体へ戻れます。時刻と数値方位は表示しません。

プロファイル切替、監視先切替、選択中プロファイル削除、アプリ再起動では古い位置を復元しません。切替後または起動後の次の有効なスクリーンショットを待ちます。

## マップ画像の再検証

プロファイル選択時と起動時の最終選択復元時に、次を校正時の情報と照合します。

- 画像の絶対パス
- 画像の幅と高さ
- 画像内容のSHA-256

いずれかが一致しない場合は校正を無効として位置を表示しません。［再校正］で現在の画像を選び、3地点を指定し直してください。SHA-256はスクリーンショット通知ごとには計算しません。

## 状態メッセージ

画面右側には、監視中、プロファイル未選択、ファイル名解析失敗、設定読込・保存失敗、画像不存在・デコード失敗、画像不一致、校正無効を区別して表示します。

読込不能な`settings.json`を検出した場合、アプリはそのファイルを自動上書きしません。設定の保存先は次です。

```text
%LOCALAPPDATA%\EftSsMap\settings.json
```

アプリが保存するのは監視ディレクトリ、マッププロファイル、最後に選択したプロファイルです。位置履歴、スクリーンショット画像、ゲームアカウント情報は保存しません。選択したスクリーンショットとマップ画像をコピー、移動、削除しません。

## 開発環境

- Windows 11 x64
- .NET 10 SDK
- Windows向けビルドツール
- NuGetから復元されるMicrosoft.WindowsAppSDK Runtime 2.4.0 / WinUI 2.3.6
- NuGetから復元されるSkiaSharp.Views.WinUI 4.151.1

## ビルド

リポジトリルートで実行します。

```powershell
dotnet restore EftSsMap.slnx
dotnet build EftSsMap.slnx -c Release
```

## テスト

```powershell
dotnet test EftSsMap.slnx -c Release
```

テストはファイル名解析、クォータニオン方向、3地点校正、アフィン投影、プロファイル、JSON設定、原子的保存、通知重複排除、監視切替、画像整合性、表示変換、画面状態遷移を、ファイル名文字列と合成データで検証します。

## unpackaged framework-dependent ZIPを作成する

単一EXEにはまとめず、publish出力一式をZIP化します。配布物へ.NETとWindows App SDKのランタイムは含めません。

```powershell
dotnet publish src/EftSsMap.App/EftSsMap.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o artifacts/publish/win-x64

Compress-Archive `
  -Path artifacts/publish/win-x64/* `
  -DestinationPath artifacts/EftSsMap-win-x64.zip `
  -Force
```

利用者は.NET 10 Runtime（x64）、Windows App SDK Runtime 2.4（x64）、Microsoft Visual C++ Redistributable（x64）を事前にインストールします。その後、ZIPをWindows 11 x64へ展開し、`EftSsMap.App.exe`を起動します。

## ローカル専用資材

実際のコミュニティ製マップ画像と生のEFTスクリーンショットを、リポジトリや配布ZIPへ含めないでください。`samples/`と`maps/`は`.gitignore`で除外されています。マップ画像の著作権と利用条件は利用者が確認してください。
