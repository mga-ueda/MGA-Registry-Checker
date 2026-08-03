# MGA Registry Checker

任意のレジストリ場所をスナップショットとして記憶し、差分があれば通知・取捨選択できる Windows アプリです。常駐せず、起動〜確認〜終了のワンショット用途を想定しています。

## 機能概要

- レジストリパスを手入力（OK / NG で検証）
- **Key のみ**: 直下の全 Value ＋ 直下 1 階層のサブキー名（存在のみ。中身・更深層は見ない）
- **単一 Value**: 指定した 1 件だけ監視
- 起動時および手動で差分チェック
- 差分ダイアログで ACCEPT（受け入れる）/ REVERT（元に戻す）を行単位で選択
- ダークテーマ UI / Esc で終了
- **`--check`**: メイン画面なしで差分チェックのみ（他アプリ・タスクスケジューラ向け）

## 必要環境

- Windows
- .NET 8 Runtime（または SDK）以降

## 実行

```powershell
# 通常起動（メイン画面）
dotnet run -c Release

# 差分チェックのみ（メイン画面なし）
dotnet run -c Release -- --check
```

ビルド成果物の例:

```powershell
.\bin\Release\net8.0-windows\MGA-RegistryChecker.exe
.\bin\Release\net8.0-windows\MGA-RegistryChecker.exe --check
```

## コマンドライン（他アプリ連携）

| 引数 | 別名 | 動作 |
|------|------|------|
| `--check` | `/check` `-check` `--silent-check` | メインウィンドウを表示せず、保存済み監視場所をすべて比較する |

### `--check` の挙動

1. メインフォームは表示しない
2. `%LocalAppData%\MGA\MGA Registry Checker\state.json` の監視一覧を読み込む
3. **差分が 1 件もなければ、何も表示せず終了**（終了コード `0`）
4. **差分があれば**、通常と同じ取捨選択ダイアログを表示する（監視場所ごとに順次）
5. ダイアログ完了後もメインフォームは出さず終了（終了コード `0`）
6. 比較処理などでエラーが出た場合はメッセージを出し、終了コード `1`

### 他アプリからの呼び出し例

```powershell
# 起動後の戻りを待つ（推奨）
Start-Process -FilePath "C:\Path\MGA-RegistryChecker.exe" -ArgumentList "--check" -Wait -PassThru
```

```csharp
// Process.Start で同期待ち
using var p = Process.Start(new ProcessStartInfo
{
    FileName = @"C:\Path\MGA-RegistryChecker.exe",
    Arguments = "--check",
    UseShellExecute = false
});
p?.WaitForExit();
int code = p?.ExitCode ?? -1;
```

### 終了コード

| コード | 意味 |
|--------|------|
| `0` | 正常終了（差分なし、または差分ダイアログ処理完了／キャンセル含む） |
| `1` | 比較エラーなど、処理中に問題があった |

差分の有無そのものは終了コードでは区別しません（差分なしも `0`）。UI を出したかどうかは呼び出し側では不要な前提です。

### 注意（連携時）

- 先に通常起動で監視場所を登録しておく必要があります（`--check` だけでは監視の追加はできません）
- 複数監視があり複数箇所に差分がある場合、ダイアログは場所ごとに連続表示されます
- `HKEY_LOCAL_MACHINE` などへの REVERT には管理者権限が必要な場合があります
- 同時に複数プロセスを起動すると `state.json` の書き込みが競合する可能性があるため、1 プロセスずつ起動してください

## 通常起動の仕様

1. メイン画面を表示し、状態ファイルを読み込む
2. 起動直後に全監視場所を自動チェック（差分があればダイアログ）
3. 監視の追加・再取得・削除・選択行の手動チェックが可能
4. Esc またはウィンドウ閉じるで終了（常駐しない）

### 監視モード

| Value 入力 | モード表示 | 記録内容 |
|------------|------------|----------|
| 空 | Key + subkeys | 直下の全 Value ＋ 直下サブキー名（1 階層・空スナップショット） |
| あり | Single value | 指定 Value のみ |

### 単一値の監視例

| 項目 | 入力例 |
|------|--------|
| キー | `HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics` |
| 値名 | `BorderWidth` |

値名を空にすると、シェルアイコンオーバーレイ定義（サブキー追加）の検知などにも使えます。

### 差分ダイアログ

- 各行で ACCEPT（現状をスナップショットに取り込む）または REVERT（スナップショット側へレジストリを戻す）を選択
- 全行に選択が入ると APPLY 可能
- CANCEL でその場所の変更をスキップ（スナップショットは更新しない）
- 見出しチェックやドラッグで連続選択可能

## データの保存先

`%LocalAppData%\MGA\MGA Registry Checker\state.json`

## ビルド

```powershell
dotnet build -c Release
```

## 構成（開発者向け）

| 領域 | 主な型 / 配置 |
|------|----------------|
| 起動 | `App`（通常 UI / `--check`） |
| 差分オーケストレーション | `DiffSession` + `DiffApplyService` + `IDiffPresenter` |
| レジストリ I/O | `RegistrySnapshotService` / `RegistryPathHelper` / `RegistryValueCodec` |
| 比較 | `DiffEngine` / `RegistryValueDisplay` |
| 永続化 | `SnapshotStore` / `AppPaths` |
| UI | `MainWindow` / `DiffWindow` / `ViewModels/*` / `Themes/*` |

## 手動確認チェックリスト

- [ ] 通常起動 → メイン表示 → 起動時に全監視をチェック
- [ ] `--check` → 差分なしなら無表示で終了コード 0
- [ ] `--check` → 差分ありなら取捨選択ダイアログのみ（メイン非表示）
- [ ] Key + subkeys: 直下サブキー追加が KeyAdded になる
- [ ] ACCEPT / REVERT / Mixed / CANCEL 後の `state.json` とレジストリ

## その他の注意

- アクセス権のないサブキーはスキップされます
- DEBUG ビルドでは SIMULATE DIFF ボタンが使えます（レジストリは変更しません）
