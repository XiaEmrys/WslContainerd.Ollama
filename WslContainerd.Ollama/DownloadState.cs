using System;

namespace WslContainerd.Ollama;

public enum DownloadState
{
    NotStarted = 0,
    Downloading = 1,
    Completed = 2,
    Cancelled = 3,
    Failed = 4
}
