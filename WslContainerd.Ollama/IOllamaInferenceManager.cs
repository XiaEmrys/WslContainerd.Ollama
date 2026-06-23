using System;
using System.Threading;
using System.Threading.Tasks;

namespace WslContainerd.Ollama;

/// <summary>
/// Ollama推理服务接口
/// 负责AI文本生成、聊天等推理操作
/// </summary>
public interface IOllamaInferenceManager
{
    /// <summary>
    /// 生成文本
    /// </summary>
    /// <param name="request">生成请求</param>
    /// <param name="progressCallback">进度回调</param>
    Task<OllamaGenerateResponse> GenerateTextAsync(OllamaGenerateRequest request, Action<string>? progressCallback = null);
    
    /// <summary>
    /// 流式生成文本
    /// </summary>
    /// <param name="request">生成请求</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task StreamGenerateTextAsync(OllamaGenerateRequest request, Action<string> progressCallback, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 聊天
    /// </summary>
    /// <param name="request">聊天请求</param>
    /// <param name="progressCallback">进度回调</param>
    Task<OllamaChatResponse> ChatAsync(OllamaChatRequest request, Action<string>? progressCallback = null);
    
    /// <summary>
    /// 流式聊天
    /// </summary>
    /// <param name="request">聊天请求</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task StreamChatAsync(OllamaChatRequest request, Action<string> progressCallback, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 简单文本生�?
    /// </summary>
    /// <param name="model">模型名称</param>
    /// <param name="prompt">提示�?/param>
    /// <param name="temperature">温度</param>
    Task<string> GenerateSimpleTextAsync(string model, string prompt, float temperature = 0.7f);
    
    /// <summary>
    /// 简单聊�?
    /// </summary>
    /// <param name="model">模型名称</param>
    /// <param name="message">消息</param>
    /// <param name="temperature">温度</param>
    Task<string> ChatSimpleAsync(string model, string message, float temperature = 0.7f);
    
    /// <summary>
    /// 预热模型（通过最小生成触发模型加载到内存）
    /// </summary>
    /// <param name="model">模型名称</param>
    /// <param name="options">预热选项</param>
    /// <param name="logCallback">日志回调</param>
    /// <returns>预热结果，包含加载耗时等信息</returns>
    Task<ModelWarmupResult> WarmupModelAsync(string model, WarmupOptions options, Action<string>? logCallback = null);
}
