# MGA Registry Checker

任意のレジストリ場所の内容をスナップショットとして記憶し、次回起動時に差分があれば通知する Windows アプリです。

## 機能

- レジストリパスを手入力（入力内容を OK / NG で検証）
- キー直下の全値 / **単一の値** を監視可能（値名の有無で切替）
- 起動時および手動で差分チェック
- 変更内容をダイアログ表示し、「元に戻す」または「受け入れる」を選択
- ダークテーマ UI
- Esc で終了（常駐しない）

## 単一値の監視例

| 項目 | 入力例 |
|------|--------|
| キー | `HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics` |
| 値名 | `BorderWidth` |

値名を入れると、その値だけを監視します（他の値の変更は無視）。

## 必要環境

- Windows
- .NET 8 SDK 以降

## 実行

F5、または:

```powershell
dotnet run -c Debug
```

## データの保存先

`%LocalAppData%\MGA\MGA Registry Checker\state.json`

## 注意

- `HKEY_LOCAL_MACHINE` など保護されたキーの書き戻しには管理者権限が必要な場合があります。
- アクセス権のないサブキーはスキップされます。
