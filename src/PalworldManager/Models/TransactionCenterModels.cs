namespace PalworldManager.Models;

public sealed class TransactionHistoryRow
{
    public string TransactionId { get; set; } = "";
    public DateTime TimestampUtc { get; set; }
    public string Operation { get; set; } = "";
    public string State { get; set; } = "";
    public string Target { get; set; } = "";
    public string BackupPath { get; set; } = "";
    public string ReportPath { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public TimeSpan Duration { get; set; }
    public int WarningCount { get; set; }
    public bool RollbackAvailable { get; set; }
    public string Details { get; set; } = "";

    public string TimestampDisplay => TimestampUtc == default ? "—" : TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string DurationDisplay => Duration <= TimeSpan.Zero ? "—" : Duration.TotalSeconds < 1 ? "<1 sec" : Duration.ToString(@"hh\:mm\:ss");
    public string RollbackDisplay => RollbackAvailable ? "Available" : "—";
}

public sealed class TransactionHistorySnapshot
{
    public List<TransactionHistoryRow> Rows { get; set; } = [];
    public List<string> Diagnostics { get; set; } = [];
}
