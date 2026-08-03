# MGA Registry Checker

レジストリの「いま」を覚えておき、あとで変わっていたらお知らせする Windows 用アプリです。  
常駐しません。起動して確認したら終了する使い方を想定しています。

プログラミングの知識は不要です。下の「使い方」から始めてください。  
コマンドライン連携・ビルド・内部仕様などは [上級者向け](#上級者向け) にまとめています。

## 画面イメージ

メイン画面（監視の登録・一覧）:

![メイン画面](Assets/screenshots/main-window.png)

差分画面（複数監視の変更をまとめて確認）:

![差分画面](Assets/screenshots/diff-dialog.png)

## できること

- 気になるレジストリの場所を登録して、そのときの状態を保存する
- 起動時やボタン操作で、保存した状態と今の状態を比べる
- 違いがあれば一覧を表示し、「受け入れる」か「元に戻す」かを選べる

## 使い方（はじめての方向け）

### 1. アプリを用意する

1. [Releases](https://github.com/mga-ueda/MGA-Registry-Checker/releases) から最新の `MGA-Registry-Checker-win-x64.zip` をダウンロードする  
2. 解凍すると正式名称の `MGA Registry Checker.exe` が出てくるので、好きなフォルダに置いてダブルクリックで起動する  

Windows だけ必要です。別途 .NET を入れる必要はありません（リリース版はランタイム同梱です）。

### 2. 監視したい場所を登録する

1. **Key** にレジストリのキーを入力する  
   例: `HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics`
2. **Value** は次のどちらか  
   - **空欄** … そのキー直下のすべての値と、ひとつ下のサブキー名を監視（深い階層は見ません）  
   - **値の名前を入力** … その 1 件だけを監視  
     例: `BorderWidth`
3. 入力が正しければ「OK」と出るので、**ADD WATCH** を押す  

登録した瞬間のレジストリ状態がスナップショットとして保存されます。

### 3. 差分を確認する

- アプリを起動すると、登録済みの場所を自動でチェックします  
- 一覧から行を選んで **CHECK NOW** でも、その場所だけ再チェックできます  
- 違いがなければ何も起きません。違いがあれば差分画面が **1 回**出ます（複数の監視で違いがあってもまとめて表示）  

### 4. 差分画面での選択

| 選択 | 意味 |
|------|------|
| **ACCEPT** | 今の状態を「正しい」ものとして覚える（レジストリは変えません） |
| **REVERT** | 以前覚えた状態にレジストリを戻す |
| **CANCEL** | 今回は何もしない（次回起動でもまた同じ差分が出ます） |

すべての行に ACCEPT か REVERT を付けてから **APPLY** で確定します。  
行ごとに混ぜて選ぶこともできます。

**便利な操作（知らないと気づきにくい）**

- 見出しのチェックで、ACCEPT / REVERT をまとめて ON/OFF できます  
- **チェックを押しながらドラッグ**すると、隣の行へ連続して同じ ON/OFF を広げられます  

### 5. Windows 起動時に自動チェックする（任意）

メイン画面左下の **「スタートアップで差分チェック」** にチェックを入れると、Windows にログオンしたときにアプリが裏で起動します。

- メイン画面は出ません（`--check` と同じ動き）  
- 差分がなければ何も表示せず終了します  
- 差分があれば取捨選択ダイアログだけ出ます  
- チェックを外すと、スタートアップ登録は削除されます  

ショートカットは作りません。レジストリ（`HKCU\...\Run`）に、**チェックした時点の EXE の場所**を絶対パスで覚えます。  
そのため **EXE を別フォルダへ移したり、名前を変えたりすると起動できなくなります。** そのときはチェックをいったん外してから、新しい場所の EXE でもう一度入れてください。

### 6. 終了する

**Esc** キー、またはウィンドウを閉じると終了します（バックグラウンドには残りません）。

## 画面の主な操作

| 操作 | 説明 |
|------|------|
| **ADD WATCH** | 入力した Key / Value を監視に追加する |
| **CHECK NOW** | 選択中の監視だけ、今すぐ比較する |
| **RECAPTURE** | 選択中の監視の「覚えている状態」を、今のレジストリで上書きする |
| **REMOVE**（または Del キー） | 監視をやめる（レジストリ自体は消しません） |
| **スタートアップで差分チェック** | ON でログオン時に `--check` 起動、OFF で登録削除（EXE 移動後は再登録が必要） |
| 一覧の余白をクリック | 選択を解除する |

## データの保存場所

監視一覧とスナップショットは次のファイルに保存されます。

`%LocalAppData%\MGA\MGA Registry Checker\state.json`

エクスプローラーのアドレスバーに `%LocalAppData%\MGA\MGA Registry Checker` と貼ると開けます。

## 注意

- `HKEY_LOCAL_MACHINE` など、管理者権限が必要な場所を元に戻す（REVERT）ときは、アプリを管理者として実行する必要がある場合があります  
- アプリは二重起動しません。既に起動中にもう一度開くと、既存のメイン画面を前面に出します（`--check` 起動中でメインが無い場合は、追加の起動は行われません）  
- Windows の表示スケール（DPI）がモニター間で変わっても、レイアウト比率は維持されます（PerMonitorV2）  

## ライセンス

[MIT License](LICENSE) © 2026 MIYABI GAME AUDIO INC.

---

## 上級者向け

開発・自動化・内部仕様です。使い方の説明は上の一般向けを参照してください。

### 名前の対応

| 用途 | 名前 |
|------|------|
| 正式なアプリ名 / アセンブリ名 | **MGA Registry Checker** |
| ソース・名前空間・プロジェクトファイル | `MgaRegistryChecker` |
| GitHub リポジトリ / リリース資産名 | [MGA-Registry-Checker](https://github.com/mga-ueda/MGA-Registry-Checker) |
| 会社（著作権・Company） | MIYABI GAME AUDIO INC. |
| バージョン（csproj） | `1.0.0` |

### 開発環境・ビルド

| 用途 | 要件 |
|------|------|
| 開発・`dotnet run` / `dotnet build` | Windows + .NET 8 SDK |
| UI | WPF（`net8.0-windows`） |

```powershell
dotnet run --project MgaRegistryChecker.csproj -c Release
dotnet run --project MgaRegistryChecker.csproj -c Release -- --check
dotnet build MgaRegistryChecker.csproj -c Release
dotnet publish MgaRegistryChecker.csproj -c Release -r win-x64
```

発行出力例: `.\bin\Release\net8.0-windows\win-x64\publish\MGA Registry Checker.exe`

- `RuntimeIdentifier` 指定時の既定: `PublishSingleFile=true`、`SelfContained=true`（未指定時）、ネイティブライブラリ同梱、単一ファイル圧縮（初回起動時に一時展開あり）
- VS Code: `build` / `publish-single-file` タスク、起動構成「MGA Registry Checker」

### 起動フロー（実装）

**通常起動**

1. `state.json` 読込 → メイン位置復元（無ければ中央）  
2. Loaded 後（`ApplicationIdle`）に全監視を自動チェック  
3. 閉じるときにメインの位置・サイズを `mainWindowBounds` へ保存  

**`--check`**

| 引数 | 別名（大文字小文字無視） |
|------|--------------------------|
| `--check` | `/check` `-check` `--silent-check` |

1. メイン非表示（`ShutdownMode.OnExplicitShutdown`）  
2. 監視ゼロ / 差分ゼロ → UI なしで終了コード `0`  
3. 差分あり → 差分ダイアログ 1 回（`owner=null`）のあとメインなしで終了  
4. 比較例外など → 終了コード `1`  

| 終了コード | 意味 |
|------------|------|
| `0` | 正常（差分なし、ダイアログ完了、Cancel 含む）。差分の有無は区別しない |
| `1` | 比較エラーなど |

```powershell
Start-Process -FilePath "C:\Path\MGA Registry Checker.exe" -ArgumentList "--check" -Wait -PassThru
```

```csharp
using var p = Process.Start(new ProcessStartInfo
{
    FileName = @"C:\Path\MGA Registry Checker.exe",
    Arguments = "--check",
    UseShellExecute = false
});
p?.WaitForExit();
int code = p?.ExitCode ?? -1;
```

`--check` だけでは監視追加不可。二重起動は `SingleInstanceGuard`（Mutex）で抑止し、追加起動は既存メインを前面化して終了する。

### 監視・検証（実装）

| Value 入力 | `WatchMode` | 一覧ラベル | 記録内容 |
|------------|-------------|------------|----------|
| 空 | `KeyOnly` | `Key + subkeys` | 直下の全 Value ＋ 直下サブキー名のみ（Values 空のスナップショット） |
| あり | `SingleValue` | `Single value` | 指定 1 Value。空文字名は既定値（表示 `(Default)`） |

- 重複判定: Path + Mode + ValueName（大文字小文字無視）  
- ハイブ略称: `HKCR` / `HKCU` / `HKLM` / `HKU` / `HKCC` → 正式名へ正規化  
- Key 必須・存在必須。Value 指定時はその Value が存在すること  
- `REG_EXPAND_SZ` は展開せず生で保存・比較（`DoNotExpandEnvironmentNames`）  
- 既定値は Capture 時に明示取得（`GetValueNames` に出ない環境向け）  
- 監視ルート消失時は現在スナップショット空 → 削除扱いの差分になり得る  

### 差分適用（実装）

| 種別 | 意味 |
|------|------|
| `KeyAdded` / `KeyRemoved` | キーの出現・消失 |
| `ValueAdded` / `ValueRemoved` / `ValueModified` | 値の追加・削除・内容または型の変更 |

APPLY 時は監視ごとに行を振り分け、その場所内の選択から Accept / Revert / Mixed を判定。最後に `state.json` を 1 回保存。

| Decision（監視ごと） | レジストリ | スナップショット |
|----------|------------|------------------|
| **Accept** | 変更なし | `CurrentSnapshot` で置換、`CapturedAt` 更新 |
| **Revert** | スナップショットへ書き戻し（余分キー削除含む） | 再 Capture |
| **Mixed** | REVERT 行だけ `RevertChanges` | ACCEPT 行だけ取り込み |
| **Cancel** | なし | なし |

Mixed の REVERT: KeyAdded→キー削除、KeyRemoved→復元、ValueAdded→値削除、ValueRemoved/Modified→スナップショット値を書き戻し。

| エラー | 挙動 |
|--------|------|
| レジストリ書き込み失敗 | ダイアログを閉じない |
| Revert / Mixed 後のスナップショット更新失敗 | Warning。閉じる場合あり |
| Accept 後のスナップショット更新失敗 | エラー。閉じない |

差分ウィンドウ位置は常にプライマリ中央（非保存）。

### スタートアップ登録（実装）

| 操作 | 内容 |
|------|------|
| ON | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` に値名 `MGA Registry Checker`、データ `"<exe絶対パス>" --check` |
| OFF | 同名の値を削除 |

（EXE 移動時の再登録は一般向けの注意を参照）

### 永続化・ウィンドウ

| 項目 | 内容 |
|------|------|
| パス | `%LocalAppData%\MGA\MGA Registry Checker\state.json` |
| 形式 | JSON（インデント、camelCase、enum は文字列） |
| 内容 | `locations[]`（`id`, `path`, `valueName`, `mode`, `capturedAt`, `keys`）、任意で `mainWindowBounds` |
| 旧 `mode: Recursive` | 読込時に `KeyOnly` へ変換 |

| ウィンドウ | 位置 |
|------------|------|
| メイン | `mainWindowBounds`（最大化フラグ含む）。無ければ中央 |
| 差分 | プライマリ中央。非保存 |

ダークタイトルバー対応あり。

### DPI（実装）

- `ApplicationHighDpiMode=PerMonitorV2`  
- `UseLayoutRounding` / `SnapsToDevicePixels`  
- `TextFormattingMode=Ideal`  
- 差分ダイアログ列幅は `PixelsPerDip` 計測、`DpiChanged` で再計算  

### プロジェクト構成

| 領域 | 主な型 / 配置 |
|------|----------------|
| 起動 | `App`（通常 UI / `--check`） |
| 差分オーケストレーション | `DiffSession` + `DiffApplyService` + `IDiffPresenter` |
| レジストリ I/O | `RegistrySnapshotService` / `RegistryPathHelper` / `RegistryValueCodec` |
| 比較 | `DiffEngine` / `RegistryValueDisplay` |
| 永続化 | `SnapshotStore` / `AppPaths` / `AppJsonContext` |
| スタートアップ | `StartupRegistration` |
| 二重起動防止 | `SingleInstanceGuard` |
| UI | `MainWindow` / `DiffWindow` / `ViewModels/*` / `Themes/*` |
| 文言 | `UiText` |

### 手動確認チェックリスト

- [ ] 通常起動 → メイン表示 → 起動時に全監視をチェック  
- [ ] `--check` → 差分なしなら無表示で終了コード 0  
- [ ] `--check` → 差分ありなら取捨選択ダイアログのみ（メイン非表示）  
- [ ] Key + subkeys: 直下サブキー追加が KeyAdded になる  
- [ ] ACCEPT / REVERT / Mixed / CANCEL 後の `state.json` とレジストリ  
- [ ] Esc で終了、メイン位置が次回復元される  
- [ ] スタートアップ ON → Run キーに `"exe" --check` が入り、OFF で消える  
- [ ] 差分ダイアログでクリック＋ドラッグにより連続チェックできる  
- [ ] 二重起動時に既存メインが前面化される  
