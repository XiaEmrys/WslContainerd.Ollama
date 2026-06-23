using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WslContainerd.Logging.Abstractions;
using WslContainerd.Services.Abstractions;
using System.Linq;

namespace WslContainerd.Ollama;

/// <summary>
/// Ollama 模型信息
/// </summary>
public class OllamaModel
{
    public string Name { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string ModifiedAt { get; set; } = string.Empty;
    public string Digest { get; set; } = string.Empty;
    public bool IsDownloaded { get; set; }
    public string Status { get; set; } = "未下载";
}

/// <summary>
/// Ollama 生成请求参数
/// </summary>
public class OllamaGenerateRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;
    
    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
    
    [JsonPropertyName("temperature")]
    public float Temperature { get; set; } = 0.7f;
    
    [JsonPropertyName("top_p")]
    public float TopP { get; set; } = 0.9f;
    
    [JsonPropertyName("top_k")]
    public int TopK { get; set; } = 40;
    
    [JsonPropertyName("num_predict")]
    public int NumPredict { get; set; } = 128;
    
    [JsonPropertyName("raw")]
    public bool Raw { get; set; } = false;
}

/// <summary>
/// Ollama 生成响应
/// </summary>
public class OllamaGenerateResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;
    
    [JsonPropertyName("done")]
    public bool Done { get; set; }
    
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;
    
    [JsonPropertyName("total_duration")]
    public long TotalDuration { get; set; }
    
    [JsonPropertyName("load_duration")]
    public long LoadDuration { get; set; }
    
    [JsonPropertyName("prompt_eval_count")]
    public long PromptEvalCount { get; set; }
    
    [JsonPropertyName("prompt_eval_duration")]
    public long PromptEvalDuration { get; set; }
    
    [JsonPropertyName("eval_count")]
    public long EvalCount { get; set; }
    
    [JsonPropertyName("eval_duration")]
    public long EvalDuration { get; set; }
}

/// <summary>
/// Ollama 聊天消息
/// </summary>
public class OllamaChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Ollama 聊天请求
/// </summary>
public class OllamaChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonPropertyName("messages")]
    public List<OllamaChatMessage> Messages { get; set; } = new();
    
    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
    
    [JsonPropertyName("temperature")]
    public float Temperature { get; set; } = 0.7f;
    
    [JsonPropertyName("top_p")]
    public float TopP { get; set; } = 0.9f;
    
    [JsonPropertyName("top_k")]
    public int TopK { get; set; } = 40;
    
    [JsonPropertyName("num_predict")]
    public int NumPredict { get; set; } = 128;
}

/// <summary>
/// Ollama 聊天响应
/// </summary>
public class OllamaChatResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonPropertyName("message")]
    public OllamaChatMessage Message { get; set; } = new();
    
    [JsonPropertyName("done")]
    public bool Done { get; set; }
    
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;
    
    [JsonPropertyName("total_duration")]
    public long TotalDuration { get; set; }
    
    [JsonPropertyName("load_duration")]
    public long LoadDuration { get; set; }
    
    [JsonPropertyName("prompt_eval_count")]
    public long PromptEvalCount { get; set; }
    
    [JsonPropertyName("prompt_eval_duration")]
    public long PromptEvalDuration { get; set; }
    
    [JsonPropertyName("eval_count")]
    public long EvalCount { get; set; }
    
    [JsonPropertyName("eval_duration")]
    public long EvalDuration { get; set; }
}

/// <summary>
/// 模型预热选项
/// </summary>
public class WarmupOptions
{
    /// <summary>
    /// 是否启用预热（默认true）
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// 预热时生成的最大token数（默认1，仅触发加载）
    /// </summary>
    public int NumPredict { get; set; } = 1;
    
    /// <summary>
    /// 每个模型预热的最大超时时间（秒，默认60）
    /// </summary>
    public int TimeoutSecondsPerModel { get; set; } = 60;
    
    /// <summary>
    /// 并发预热的模型数量（默认1，避免内存压力）
    /// </summary>
    public int Parallelism { get; set; } = 1;
    
    /// <summary>
    /// 是否阻塞直到关键模型预热完成（默认true，确保首轮响应快速）
    /// </summary>
    public bool BlockUntilWarm { get; set; } = true;
}

/// <summary>
/// 模型预热结果
/// </summary>
public class WarmupResult
{
    /// <summary>
    /// 总体是否成功
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// 每个模型的预热结果
    /// </summary>
    public List<ModelWarmupResult> ModelResults { get; set; } = new();
    
    /// <summary>
    /// 总耗时（毫秒）
    /// </summary>
    public long TotalDurationMs { get; set; }
}

/// <summary>
/// 单个模型的预热结果
/// </summary>
public class ModelWarmupResult
{
    /// <summary>
    /// 模型名称
    /// </summary>
    public string ModelName { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// 耗时（毫秒）
    /// </summary>
    public long DurationMs { get; set; }
    
    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 模型加载耗时（纳秒，从Ollama响应中获取）
    /// </summary>
    public long LoadDurationNs { get; set; }
}

/// <summary>
/// Ollama 服务接口
/// </summary>
public interface IOllamaService : IServiceModule
{
    /// <summary>
    /// 列出可用的模型
    /// </summary>
    Task<string> ListModelsAsync();
    
    /// <summary>
    /// 获取已下载的模型列表
    /// </summary>
    Task<List<OllamaModel>> GetDownloadedModelsAsync();
    
    /// <summary>
    /// 检查模型是否已下载
    /// </summary>
    /// <param name="modelName">模型名称</param>
    Task<bool> IsModelDownloadedAsync(string modelName);
    
    /// <summary>
    /// 下载模型
    /// </summary>
    /// <param name="modelName">模型名称</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> DownloadModelAsync(string modelName, Action<DownloadProgressInfo>? progressCallback = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 删除模型
    /// </summary>
    /// <param name="modelName">模型名称</param>
    Task<bool> DeleteModelAsync(string modelName);
    
    /// <summary>
    /// 获取模型信息
    /// </summary>
    /// <param name="modelName">模型名称</param>
    Task<OllamaModel?> GetModelInfoAsync(string modelName);
    
    /// <summary>
    /// 生成文本
    /// </summary>
    /// <param name="model">模型名称</param>
    /// <param name="prompt">提示词</param>
    /// <param name="temperature">温度参数</param>
    Task<string> GenerateSimpleTextAsync(string model, string prompt, float temperature = 0.7f);
    
    /// <summary>
    /// 简单聊天
    /// </summary>
    /// <param name="model">模型名称</param>
    /// <param name="message">消息</param>
    /// <param name="temperature">温度参数</param>
    Task<string> ChatSimpleAsync(string model, string message, float temperature = 0.7f);
    
    /// <summary>
    /// 流式聊天
    /// </summary>
    /// <param name="model">模型名称</param>
    /// <param name="message">消息</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="temperature">温度参数</param>
    Task StreamChatAsync(string model, string message, Action<string> progressCallback, float temperature = 0.7f);
    
    /// <summary>
    /// 检查GPU支持
    /// </summary>
    Task<bool> CheckGpuSupportAsync();
    
    /// <summary>
    /// 获取GPU信息
    /// </summary>
    Task<string> GetGpuInfoAsync();
    
    /// <summary>
    /// 检查端口映射
    /// </summary>
    Task<bool> CheckPortMappingAsync();
    
    /// <summary>
    /// 修复端口映射
    /// </summary>
    Task<bool> FixPortMappingAsync();
    
    /// <summary>
    /// 获取连接诊断信息
    /// </summary>
    Task<string> GetConnectionDiagnosticsAsync();
    
    /// <summary>
    /// 启动Ollama容器（支持自定义模型存储路径）
    /// </summary>
    /// <param name="modelStoragePath">模型存储路径</param>
    /// <param name="logCallback">日志回调</param>
    Task<bool> StartAsync(string? modelStoragePath, Action<string>? logCallback = null);
    
    /// <summary>
    /// 预热模型（将模型加载到内存，避免首次推理延迟）
    /// </summary>
    /// <param name="models">要预热的模型名称列表</param>
    /// <param name="options">预热选项</param>
    /// <param name="logCallback">日志回调</param>
    /// <returns>预热结果，包含每个模型的预热状态</returns>
    Task<WarmupResult> WarmupAsync(IEnumerable<string> models, WarmupOptions options, Action<string>? logCallback = null);
}
