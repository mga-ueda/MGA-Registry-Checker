# MGA Registry Checker

レジストリの「いま」を覚えておき、あとで変わっていたらお知らせする Windows 用アプリです。  
常駐しません。起動して確認したら終了する使い方を想定しています。

プログラミングの知識は不要です。下の「使い方」から始めてください。  
コマンドライン連携・ビルド・内部仕様などは [上級者向け](#上級者向け) にまとめています。

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

### 5. 終了する

**Esc** キー、またはウィンドウを閉じると終了します（バックグラウンドには残りません）。

## 画面の主な操作

| 操作 | 説明 |
|------|------|
| **ADD WATCH** | 入力した Key / Value を監視に追加する |
| **CHECK NOW** | 選択中の監視だけ、今すぐ比較する |
| **RECAPTURE** | 選択中の監視の「覚えている状態」を、今のレジストリで上書きする |
| **REMOVE**（または Del キー） | 監視をやめる（レジストリ自体は消しません） |
| 一覧の余白をクリック | 選択を解除する |

## データの保存場所

監視一覧とスナップショットは次のファイルに保存されます。

`%LocalAppData%\MGA\MGA Registry Checker\state.json`

エクスプローラーのアドレスバーに `%LocalAppData%\MGA\MGA Registry Checker` と貼ると開けます。

## 注意

- `HKEY_LOCAL_MACHINE` など、管理者権限が必要な場所を元に戻す（REVERT）ときは、アプリを管理者として実行する必要がある場合があります  
- アクセス権のないキーは読み飛ばします  
- 同時に複数のアプリを起動すると、保存ファイルがぶつかり合うことがあるので、1 つずつ起動してください  

## ライセンス

[MIT License](LICENSE) © 2026 MIYABI GAME AUDIO INC.

---

## 上級者向け

開発・自動化・仕様の詳細です。一般利用だけなら上の説明で十分です。

### 名前の対応

| 用途 | 名前 |
|------|------|
| 正式なアプリ名 / アセンブリ名 | **MGA Registry Checker** |
| ソース・名前空間・プロジェクトファイル | `MgaRegistryChecker` |
| GitHub リポジトリ / リリース資産名 | [MGA-Registry-Checker](https://github.com/mga-ueda/MGA-Registry-Checker) |
| 会社（著作権・Company） | MIYABI GAME AUDIO INC. |
| バージョン（csproj） | `1.0.0` |

### 必要環境

| 用途 | 要件 |
|------|------|
| リリース EXE の実行 | Windows（x64）。.NET の別インストール不要 |
| 開発・`dotnet run` / `dotnet build` | Windows + .NET 8 SDK |
| UI フレームワーク | WPF（`net8.0-windows`） |

### ダウンロードと実行（EXE）

```powershell
& ".\MGA Registry Checker.exe"          # 通常起動（メイン画面）
& ".\MGA Registry Checker.exe" --check  # 差分チェックのみ（後述）
```

### 開発時の実行・ビルド・単一 EXE 発行

```powershell
# 通常起動
dotnet run --project MgaRegistryChecker.csproj -c Release

# 差分チェックのみ
dotnet run --project MgaRegistryChecker.csproj -c Release -- --check

# ビルド
dotnet build MgaRegistryChecker.csproj -c Release

# self-contained 単一 EXE（win-x64）
dotnet publish MgaRegistryChecker.csproj -c Release -r win-x64
```

発行出力例:

`.\bin\Release\net8.0-windows\win-x64\publish\MGA Registry Checker.exe`

- `RuntimeIdentifier` 指定時の既定: `PublishSingleFile=true`、`SelfContained=true`（未指定時）、ネイティブライブラリ同梱、単一ファイル圧縮  
- 初回起動時に一時展開あり  

VS Code: `build` / `publish-single-file` タスク、起動構成「MGA Registry Checker」あり。

### 通常起動の仕様

1. メインウィンドウを表示し、`state.json` を読み込む  
2. 前回保存したメインウィンドウ位置があれば復元。なければ画面中央  
3. Loaded 後（`ApplicationIdle`）に、登録済みの全監視を自動チェック  
4. 差分があれば、全監視分をまとめた差分ダイアログを 1 回表示  
5. Esc またはウィンドウ閉じるで終了。閉じるときにメインの位置・サイズを `state.json` に保存  

常駐しないワンショット動作です。

### メイン画面の操作詳細

| 操作 | 詳細 |
|------|------|
| Key / Value 入力 | リアルタイム検証。OK のときだけ ADD WATCH が有効 |
| ADD WATCH | スナップショット取得して一覧に追加。同一 Path + Mode + ValueName（大文字小文字無視）は重複拒否 |
| CHECK NOW | 選択中の 1 件だけ比較 |
| RECAPTURE | 選択中のスナップショットを現在値で上書きして保存 |
| REMOVE / Del | 確認後、監視エントリのみ削除（レジストリは変更しない） |
| 余白クリック | 一覧の選択解除 |
| Esc | アプリ終了 |
| SIMULATE DIFF | **DEBUG ビルドのみ**表示。擬似差分ダイアログ。レジストリも `state.json` も変更しない |

### 監視モード

UI から追加できるのは次の 2 種です。

| Value 入力 | `WatchMode` | 一覧ラベル | 記録内容 |
|------------|-------------|------------|----------|
| 空 | `KeyOnly` | `Key + subkeys` | 直下の全 Value ＋ 直下 1 階層のサブキー名のみ（サブキーは存在監視。Values 空のスナップショット）。更深層は見ない |
| あり | `SingleValue` | `Single value` | 指定した 1 Value のみ。値名が空文字のときはレジストリの既定値（表示名 `(Default)`） |

**`WatchMode.Recursive`**: モデルおよび Capture / Revert 実装あり。深い階層まで再帰取得。**現行 UI からは追加不可**（レガシー / 将来用）。Recursive 時、アクセス権のないサブキーはスキップする。

#### 単一値の監視例

| 項目 | 入力例 |
|------|--------|
| Key | `HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics` |
| Value | `BorderWidth` |

Value を空にすると、シェルアイコンオーバーレイ定義などの「直下サブキー追加」検知にも使えます。

### 入力検証・パス正規化

- 対応ハイブ（正式名と略称。大文字小文字無視）:

  | 正式名 | 略称 |
  |--------|------|
  | `HKEY_CLASSES_ROOT` | `HKCR` |
  | `HKEY_CURRENT_USER` | `HKCU` |
  | `HKEY_LOCAL_MACHINE` | `HKLM` |
  | `HKEY_USERS` | `HKU` |
  | `HKEY_CURRENT_CONFIG` | `HKCC` |

- 保存・表示時は正式名に正規化  
- Key は必須かつ存在必須。Value 指定時はその Value が存在すること  
- Key・Value とも空のときはヒント表示のみ（ADD 不可）  

### 差分の種類

`DiffEngine` が検出する変更種別:

| 種別 | 意味 |
|------|------|
| `KeyAdded` | スナップショットに無かったキーが出現 |
| `KeyRemoved` | スナップショットにあったキーが消失 |
| `ValueAdded` | 値が追加された |
| `ValueRemoved` | 値が削除された |
| `ValueModified` | 値の内容または型が変わった |

監視ルート自体が消えた場合は、現在スナップショットが空になり、削除扱いの差分になり得ます。

比較・保存時、`REG_EXPAND_SZ` は環境変数展開せず生データのまま扱います（`DoNotExpandEnvironmentNames`）。既定値は `GetValueNames` に出ない環境があるため、Capture 時に明示取得します。

### 差分ダイアログの仕様

- 差分のある監視場所が複数あっても、ダイアログは **1 回だけ**開き、全差分を一覧表示する  
- 各行で ACCEPT / REVERT を選択（行単位で排他。同時オン不可）  
- 全行が ACCEPT または REVERT のどちらかに付いたときだけ APPLY 有効  
- 見出しチェックで列全体を一括選択可能  
- ドラッグ塗りで連続行に同じ選択を付けられる  
- CANCEL / Esc: 何も適用せず閉じる → スナップショット未更新のため、次回も同じ差分が出る  
- ウィンドウ位置: 常にプライマリディスプレイ中央。メイン位置とは独立で、位置は保存しない  
- `--check` 時の owner は null（メイン非表示）  

#### APPLY 後の副作用

監視場所ごとに行を振り分け、その場所内の選択から Accept / Revert / Mixed を判定して適用する。最後に `state.json` を 1 回保存する。

| Decision（監視ごと） | レジストリ | スナップショット（`state.json`） |
|----------|------------|----------------------------------|
| **Accept（その監視の全行 ACCEPT）** | 変更なし | `CurrentSnapshot` で置換、`CapturedAt` 更新 |
| **Revert（その監視の全行 REVERT）** | スナップショットへ書き戻し（余分キー削除含む） | 再 Capture して保存 |
| **Mixed** | REVERT 行だけ `RevertChanges` | ACCEPT 行だけスナップショットへ取り込み |
| **Cancel** | なし | なし |

#### Mixed 時の REVERT（行種別）

| 種別 | REVERT 時のレジストリ操作 |
|------|---------------------------|
| KeyAdded | キー削除 |
| KeyRemoved | スナップショットからキー復元 |
| ValueAdded | 値削除 |
| ValueRemoved / ValueModified | スナップショットの値を書き戻し |

#### エラー時

| 状況 | 挙動 |
|------|------|
| レジストリ書き込み失敗 | ダイアログを閉じない（commit 失敗） |
| Revert / Mixed 後のスナップショット更新失敗 | Warning 表示。commit 成功扱いで閉じる場合あり |
| Accept 後のスナップショット更新失敗 | エラー。閉じない |

### コマンドライン（`--check`）

| 引数 | 別名（大文字小文字無視） | 動作 |
|------|--------------------------|------|
| `--check` | `/check` `-check` `--silent-check` | メインを出さず、保存済み監視をすべて比較 |

#### 挙動

1. メインウィンドウは表示しない（`ShutdownMode.OnExplicitShutdown`）  
2. `state.json` の監視一覧を読み込む  
3. 監視ゼロ、または差分ゼロ → UI なしで終了コード `0`  
4. 差分あり → 通常と同じ取捨選択ダイアログを 1 回表示（複数監視の差分はまとめて報告）  
5. ダイアログ完了後もメインは出さず終了  
6. 比較例外など → メッセージ表示、終了コード `1`（起動時 catch も `1`）  

#### 終了コード

| コード | 意味 |
|--------|------|
| `0` | 正常（差分なし、ダイアログ完了、Cancel 含む） |
| `1` | 比較エラーなど処理中の問題 |

差分の有無そのものは終了コードでは区別しません。

#### 呼び出し例

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

#### 連携時の注意

- 先に通常起動で監視を登録しておくこと（`--check` だけでは追加不可）  
- 複数監視に差分がある場合も、ダイアログは 1 回にまとめて表示する  
- HKLM 等への REVERT には管理者権限が必要な場合あり  
- 同時複数プロセスは `state.json` 書き込みが競合し得る  

### 状態ファイル（`state.json`）

| 項目 | 内容 |
|------|------|
| パス | `%LocalAppData%\MGA\MGA Registry Checker\state.json` |
| 会社フォルダ | `MGA` |
| アプリフォルダ | `MGA Registry Checker` |
| 形式 | JSON（インデント、camelCase、enum は文字列） |
| 主な内容 | `locations[]`（監視エントリとスナップショット）、任意で `mainWindowBounds` |

`WatchedLocation` の主なフィールド: `id`, `path`, `valueName`, `mode`, `capturedAt`, `keys`。

### ウィンドウ位置

| ウィンドウ | 位置 |
|------------|------|
| メイン | `mainWindowBounds` に保存・復元（最大化フラグ含む）。無ければ中央 |
| 差分 | 常にプライマリ中央。非保存 |

ダークタイトルバー対応あり。

### プロジェクト構成（開発者向け）

| 領域 | 主な型 / 配置 |
|------|----------------|
| 起動 | `App`（通常 UI / `--check`） |
| 差分オーケストレーション | `DiffSession` + `DiffApplyService` + `IDiffPresenter` |
| レジストリ I/O | `RegistrySnapshotService` / `RegistryPathHelper` / `RegistryValueCodec` |
| 比較 | `DiffEngine` / `RegistryValueDisplay` |
| 永続化 | `SnapshotStore` / `AppPaths` / `AppJsonContext` |
| UI | `MainWindow` / `DiffWindow` / `ViewModels/*` / `Themes/*` |
| 文言 | `UiText` |

### 手動確認チェックリスト

- [ ] 通常起動 → メイン表示 → 起動時に全監視をチェック  
- [ ] `--check` → 差分なしなら無表示で終了コード 0  
- [ ] `--check` → 差分ありなら取捨選択ダイアログのみ（メイン非表示）  
- [ ] Key + subkeys: 直下サブキー追加が KeyAdded になる  
- [ ] ACCEPT / REVERT / Mixed / CANCEL 後の `state.json` とレジストリ  
- [ ] Esc で終了、メイン位置が次回復元される  
- [ ] DEBUG の SIMULATE DIFF がレジストリを変更しない  
