using System;

namespace WslContainerd.Ollama;

/// <summary>
/// Download speed calculator for model downloads.
/// </summary>
public class DownloadSpeedCalculator
{
    private DateTime _lastUpdateTime;
    private long _lastDownloadedBytes;
    private long _lastCalculatedSpeed;
    private readonly int _updateIntervalSeconds;

    public DownloadSpeedCalculator(int updateIntervalSeconds = 1)
    {
        _updateIntervalSeconds = updateIntervalSeconds;
    }

    public long CalculateSpeed(long currentBytes, DateTime currentTime)
    {
        if (_lastUpdateTime == default)
        {
            _lastUpdateTime = currentTime;
            _lastDownloadedBytes = currentBytes;
            _lastCalculatedSpeed = 0;
            return 0;
        }

        var timeDiff = (currentTime - _lastUpdateTime).TotalSeconds;

        if (timeDiff < _updateIntervalSeconds)
        {
            return _lastCalculatedSpeed;
        }

        var bytesDiff = currentBytes - _lastDownloadedBytes;
        var speed = timeDiff > 0 ? (long)(bytesDiff / timeDiff) : 0;

        _lastUpdateTime = currentTime;
        _lastDownloadedBytes = currentBytes;
        _lastCalculatedSpeed = Math.Max(0, speed);

        return _lastCalculatedSpeed;
    }

    public TimeSpan CalculateRemainingTime(long totalSize, long downloadedSize, long currentSpeed)
    {
        if (currentSpeed <= 0 || downloadedSize >= totalSize)
        {
            return TimeSpan.Zero;
        }

        var remainingBytes = totalSize - downloadedSize;
        var remainingSeconds = (double)remainingBytes / currentSpeed;

        return TimeSpan.FromSeconds(Math.Max(0, remainingSeconds));
    }

    public void Reset()
    {
        _lastUpdateTime = default;
        _lastDownloadedBytes = 0;
        _lastCalculatedSpeed = 0;
    }

    public static string FormatSpeed(long speedBytesPerSecond)
    {
        if (speedBytesPerSecond <= 0)
        {
            return "0 B/s";
        }

        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        if (speedBytesPerSecond >= GB)
        {
            return $"{speedBytesPerSecond / (double)GB:F1} GB/s";
        }

        if (speedBytesPerSecond >= MB)
        {
            return $"{speedBytesPerSecond / (double)MB:F1} MB/s";
        }

        if (speedBytesPerSecond >= KB)
        {
            return $"{speedBytesPerSecond / (double)KB:F1} KB/s";
        }

        return $"{speedBytesPerSecond} B/s";
    }

    public static string FormatTime(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
        {
            return $"{(int)timeSpan.TotalDays}天 {timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }

        if (timeSpan.TotalHours >= 1)
        {
            return $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }

        if (timeSpan.TotalMinutes >= 1)
        {
            return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }

        return $"{timeSpan.Seconds}秒";
    }
}
