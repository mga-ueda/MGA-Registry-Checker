namespace MGA_RegistryChecker;

/// <summary>
/// アプリ内の表示文言・文言構成を一箇所に集約する。
/// 翻訳や文面変更は原則このクラスのみを編集する。
/// </summary>
public static class UiText
{
    // ----- アプリ共通 -----
    public const string AppName = "MGA Registry Checker";
    public const string DefaultValueName = "(Default)";
    public const string Ready = "準備完了";

    public static string MainWindowTitle(string version) =>
        $"{AppName} - Version {version}";

    // ----- メイン画面ラベル -----
    public const string LabelKey = "Key:";
    public const string LabelValue = "Value:";
    public const string ButtonAddWatch = "ADD WATCH";
    public const string ButtonCheckNow = "CHECK NOW";
    public const string ButtonRecapture = "RECAPTURE";
    public const string ButtonRemove = "REMOVE";
    public const string ButtonSimulateDiff = "SIMULATE DIFF";
    public const string ButtonCancel = "CANCEL";
    public const string ButtonApply = "APPLY";

    public const string TooltipKeyPath =
        @"例: HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics";

    public const string TooltipValueName =
        "空欄: その Key 直下の全 Value を監視。例: BorderWidth（その Value だけ監視）";

    public const string TooltipRecapture =
        "選択中の場所のスナップショットを、現在のレジストリ状態で更新します";

    public const string TooltipSimulateDiff =
        "DEBUG 専用: 擬似差分ダイアログを表示（レジストリは変更しません）";

    public const string ValidationHintIdle =
        "Key を入力してください。Value が空のときは、その Key 直下のすべての Value を監視します（サブキーは含みません）。Value を指定すると、その1件だけを監視します。";

    public const string EmptyWatchList =
        "監視するレジストリ場所を追加してください";

    // ----- ダイアログタイトル -----
    public const string TitleInfo = "情報";
    public const string TitleWarning = "警告";
    public const string TitleError = "エラー";
    public const string TitleConfirm = "確認";
    public const string TitleRestoreError = "復元エラー";
    public const string TitleDiff = "レジストリの変更";
    public const string TitleDiffSimulation = "レジストリの変更（シミュレーション）";

    // ----- 差分ダイアログ -----
    public const string ColumnType = "Type";
    public const string ColumnKey = "Key";
    public const string ColumnValue = "Value";
    public const string ColumnOld = "Old";
    public const string ColumnNew = "New";
    public const string ColumnAccept = " ACCEPT";
    public const string ColumnRevert = " REVERT";

    public const string KindKeyAdded = "キー追加";
    public const string KindKeyRemoved = "キー削除";
    public const string KindValueAdded = "値追加";
    public const string KindValueRemoved = "値削除";
    public const string KindValueModified = "値変更";
    public const string KindUnknown = "?";

    public const string TooltipAcceptHeader =
        "ACCEPT: 現在のレジストリ状態を基準として受け入れます。見出しのチェックで全行を一括 ON/OFF できます。押しっぱなしでドラッグすると連続チェックできます。REVERT とは同時に選べません。";

    public const string TooltipAcceptHeaderCheck =
        "ACCEPT を全行チェック / 全解除します";

    public const string TooltipAcceptRow =
        "ACCEPT: 現在の値を新しい基準として保存します（レジストリは書き換えません）。押しっぱなしでドラッグすると連続チェックできます。";

    public const string TooltipRevertHeader =
        "REVERT: スナップショット側の内容でレジストリを元に戻します。見出しのチェックで全行を一括 ON/OFF できます。押しっぱなしでドラッグすると連続チェックできます。ACCEPT とは同時に選べません。";

    public const string TooltipRevertHeaderCheck =
        "REVERT を全行チェック / 全解除します";

    public const string TooltipRevertRow =
        "REVERT: スナップショットの内容でレジストリに書き戻します。押しっぱなしでドラッグすると連続チェックできます。";

    public const string TooltipCancel =
        "何も適用せず閉じます。レジストリも保存データも変更しないため、次回起動時（または再チェック時）に同じ差分通知が出ます。";

    public const string TooltipApply =
        "全行を ACCEPT または REVERT のいずれかに設定すると有効になります。ACCEPT は基準の更新、REVERT はレジストリへの書き戻しです。";

    public static string DiffDetected(string path) =>
        $"変更を検出: {path}";

    public static string DiffDetectedSim(string path) =>
        $"[SIM] 変更を検出: {path}";

    public static string DiffSubText(int count) =>
        $"{count} 件の差異があります。全行を ACCEPT（青）または REVERT（赤）に設定すると APPLY が有効になります。見出しで全選択可。CANCEL は何もせず閉じます（次回も通知されます）。";

    public static string DiffSubTextSim(int count) =>
        $"{count} 件の擬似差分です。全行を ACCEPT（青）または REVERT（赤）に設定すると APPLY が有効になります。見出しで全選択可。CANCEL は常に有効です。実レジストリ・保存データは変わりません。";

    // ----- MessageBox 本文 -----
    public const string MsgInputInvalid =
        "入力が不正です。Key と Value を確認してください。";

    public const string MsgAlreadyWatched =
        "この場所はすでに監視中です。";

    public static string MsgCaptureFailed(string detail) =>
        $"スナップショットの取得に失敗しました。\n{detail}";

    public static string MsgConfirmStopWatch(string path) =>
        $"この場所の監視を削除しますか？\n{path}";

    public static string MsgRecaptureFailed(string detail) =>
        $"再キャプチャに失敗しました。\n{detail}";

    public static string MsgCompareFailed(string path, string detail) =>
        $"比較に失敗しました。\n{path}\n{detail}";

    public static string MsgRecaptureAfterRevertFailed(string detail) =>
        $"元に戻したあとの再キャプチャに失敗しました。\n{detail}";

    public static string MsgMixedApplyFailed(string detail) =>
        $"個別適用に失敗しました。\n{detail}";

    public static string MsgRestoreFailed(string detail) =>
        $"レジストリの復元に失敗しました。\n管理者権限が必要な場合があります。\n\n{detail}";

    // ----- ステータス欄 -----
    public static string StatusStateFile(string path) =>
        $"状態ファイル: {path}";

    public static string StatusDebugSeed(int count, string path) =>
        $"DEBUG: 監視 {count} 件を投入（SIMULATE DIFF で差分 20 件） / {path}";

    public const string StatusCapturing = "スナップショットを取得中…";
    public const string StatusAddFailed = "追加に失敗しました";
    public const string StatusNoWatches = "監視中の場所がありません";
    public const string StatusNoDifferences = "差異はありません";

    public static string StatusAdded(string label) =>
        $"追加しました: {label}";

    public static string StatusRemoved(string path) =>
        $"削除しました: {path}";

    public static string StatusRecaptured(string path) =>
        $"再キャプチャしました: {path}";

    public static string StatusChecking(string path) =>
        $"確認中: {path}";

    public static string StatusAccepted(string path) =>
        $"受け入れました: {path}";

    public static string StatusReverted(string path) =>
        $"元に戻しました: {path}";

    public static string StatusMixedApplied(string path, int acceptCount, int revertCount) =>
        $"個別適用しました: {path}（ACCEPT {acceptCount} / REVERT {revertCount}）";

    public static string StatusSkipped(string path) =>
        $"スキップしました: {path}";

    public static string StatusSimAcceptAll(int count) =>
        $"シミュレート: ACCEPT ALL（未変更） / {count} 件";

    public static string StatusSimRevertAll(int count) =>
        $"シミュレート: REVERT ALL（未変更） / {count} 件";

    public static string StatusSimMixed(int acceptCount, int revertCount) =>
        $"シミュレート: APPLY（未変更） ACCEPT {acceptCount} / REVERT {revertCount}";

    public static string StatusSimCancel(int count) =>
        $"シミュレート: CANCEL / {count} 件";

    // ----- 監視リスト表示 -----
    public const string ModeRecursive = "Recursive";
    public const string ModeKeyOnly = "Key only";
    public const string ModeSingleValue = "Single value";
    public const string CountOneValue = "1 value";

    public static string CountKeys(int count) =>
        $"{count} keys";

    public static string SingleValueDisplayPath(string path, string? valueName) =>
        $"{path} → {(string.IsNullOrEmpty(valueName) ? DefaultValueName : valueName)}";

    // ----- 入力検証 -----
    public const string ValidateKeyEmpty = "NG - Key が未入力です";
    public const string ValidateKeyFormat = "NG - Key の形式が不正です（例: HKCU\\Software\\...）";
    public const string ValidateKeyMissing = "NG - Key が存在しないか、アクセスできません";

    public static string ValidateKeyOkAllValues(string normalized) =>
        $"OK - Key: {normalized}（直下の全 Value を監視）";

    public static string ValidateKeyOkSingleValue(string normalized, string label) =>
        $"OK - Key: {normalized} → Value: {label}";

    public static string ValidateValueMissing(string label) =>
        $"NG - Value が見つかりません: {label}";

    public static string DisplayValueLabel(string? valueName) =>
        string.IsNullOrEmpty(valueName) ? DefaultValueName : valueName;

    // ----- 値表示（差分 Old/New など） -----
    public const string ValueNull = "(null)";
    public const string ValueEmpty = "(empty)";

    public static string ValueBinarySummary(int byteLength, string truncated) =>
        $"[{byteLength} bytes] {truncated}";
}
