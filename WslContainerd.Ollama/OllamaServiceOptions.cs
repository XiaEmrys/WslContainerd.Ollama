namespace WslContainerd.Ollama;

/// <summary>
/// Configuration for Ollama container and API endpoints.
/// </summary>
public sealed class OllamaServiceOptions
{
    public string ImageName { get; set; } = "ollama/ollama:latest";
    public string ContainerName { get; set; } = "ollama";
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public int HostPort { get; set; } = 11434;
    public string UserAgent { get; set; } = "WslContainerd-Ollama/1.0";
}
