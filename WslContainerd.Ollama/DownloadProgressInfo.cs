using System;

namespace WslContainerd.Ollama;

/// <summary>
/// Unified download progress payload for model download callbacks.
/// </summary>
public class DownloadProgressInfo
{
    public string ModelName { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public long CompletedSize { get; set; }
    public int ProgressPercentage { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public DownloadState State { get; set; } = DownloadState.Downloading;
    public string? ErrorMessage { get; set; }
    public bool IsCompleted => State == DownloadState.Completed;
    public bool IsFailed => State == DownloadState.Failed;
    public bool IsCancelled => State == DownloadState.Cancelled;
    public long DownloadSpeed { get; set; }
    public TimeSpan EstimatedTimeRemaining { get; set; }

    public static DownloadProgressInfo CreateStart(string modelName)
    {
        return new DownloadProgressInfo
        {
            ModelName = modelName,
            State = DownloadState.Downloading,
            StatusMessage = $"开始下载模型: {modelName}",
            ProgressPercentage = 0
        };
    }

    public static DownloadProgressInfo CreateProgress(string modelName, long totalSize, long completedSize, string statusMessage)
    {
        var progressPercentage = totalSize > 0 ? (int)((completedSize * 100) / totalSize) : 0;

        return new DownloadProgressInfo
        {
            ModelName = modelName,
            TotalSize = totalSize,
            CompletedSize = completedSize,
            ProgressPercentage = Math.Min(100, Math.Max(0, progressPercentage)),
            StatusMessage = statusMessage,
            State = DownloadState.Downloading
        };
    }

    public static DownloadProgressInfo CreateProgress(string modelName, long totalSize, long completedSize, long downloadSpeed, TimeSpan estimatedTimeRemaining, string statusMessage = "")
    {
        var progressPercentage = totalSize > 0 ? (int)((completedSize * 100) / totalSize) : 0;

        if (string.IsNullOrEmpty(statusMessage))
        {
            var speedText = DownloadSpeedCalculator.FormatSpeed(downloadSpeed);
            var timeText = DownloadSpeedCalculator.FormatTime(estimatedTimeRemaining);
            statusMessage = $"下载中... {speedText} 剩余: {timeText}";
        }

        return new DownloadProgressInfo
        {
            ModelName = modelName,
            TotalSize = totalSize,
            CompletedSize = completedSize,
            ProgressPercentage = Math.Min(100, Math.Max(0, progressPercentage)),
            StatusMessage = statusMessage,
            State = DownloadState.Downloading,
            DownloadSpeed = downloadSpeed,
            EstimatedTimeRemaining = estimatedTimeRemaining
        };
    }

    public static DownloadProgressInfo CreateCompleted(string modelName)
    {
        return new DownloadProgressInfo
        {
            ModelName = modelName,
            State = DownloadState.Completed,
            StatusMessage = $"模型 {modelName} 下载完成",
            ProgressPercentage = 100
        };
    }

    public static DownloadProgressInfo CreateFailed(string modelName, string errorMessage)
    {
        return new DownloadProgressInfo
        {
            ModelName = modelName,
            State = DownloadState.Failed,
            StatusMessage = $"模型 {modelName} 下载失败",
            ErrorMessage = errorMessage,
            ProgressPercentage = 0
        };
    }

    public static DownloadProgressInfo CreateCancelled(string modelName)
    {
        return new DownloadProgressInfo
        {
            ModelName = modelName,
            State = DownloadState.Cancelled,
            StatusMessage = $"模型 {modelName} 下载已取消",
            ProgressPercentage = 0
        };
    }

    public string GetFormattedTotalSize() => FormatFileSize(TotalSize);
    public string GetFormattedCompletedSize() => FormatFileSize(CompletedSize);
    public string GetFormattedDownloadSpeed() => $"{FormatFileSize(DownloadSpeed)}/s";

    public string GetFormattedTimeRemaining()
    {
        if (EstimatedTimeRemaining <= TimeSpan.Zero)
            return "计算中...";

        if (EstimatedTimeRemaining.TotalSeconds < 60)
            return $"{(int)EstimatedTimeRemaining.TotalSeconds}秒";

        if (EstimatedTimeRemaining.TotalHours < 1)
            return $"{(int)EstimatedTimeRemaining.TotalMinutes}分{EstimatedTimeRemaining.Seconds}秒";

        return $"{(int)EstimatedTimeRemaining.TotalHours}小时{EstimatedTimeRemaining.Minutes}分钟";
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";

        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";

        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";

        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }
}
