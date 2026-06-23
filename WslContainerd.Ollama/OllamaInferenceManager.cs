using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WslContainerd.Logging.Abstractions;
using WslContainerd.Services.Abstractions;

namespace WslContainerd.Ollama;

/// <summary>
/// Ollama推理管理器实现
/// 负责AI文本生成、聊天等推理操作
/// </summary>
public class OllamaInferenceManager : IOllamaInferenceManager
{
    private readonly IWslContainerdLogger _logger;
    
    private const string BaseUrl = "http://localhost:11434";

    public OllamaInferenceManager(IWslContainerdLogger logger)
    {
        _logger = logger;
        // 移除静态HttpClient实例，改为每次请求时创建新的实例
    }

    public async Task<OllamaGenerateResponse> GenerateTextAsync(OllamaGenerateRequest request, Action<string>? progressCallback = null)
    {
        const int maxRetries = 3;
        const int retryDelayMs = 1000;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                progressCallback?.Invoke($"开始生成文本，模型: {request.Model} (尝试 {attempt}/{maxRetries})");
                _logger.LogInformation($"开始生成文本，模型: {request.Model} (尝试 {attempt}/{maxRetries})");
                
                // ✅ 调试日志：记录最终提示词
                _logger.LogInformation("=== Ollama 生成请求 - 最终提示词 ===\n模型: {Model}\n提示词长度: {PromptLength}\n提示词内容:\n{Prompt}\n温度: {Temperature}\n最大Token数: {NumPredict}\n================================",
                    request.Model,
                    request.Prompt?.Length ?? 0,
                    request.Prompt ?? "(空)",
                    request.Temperature,
                    request.NumPredict);
                
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(30); // 生成可能需要较长时间
                httpClient.DefaultRequestHeaders.Add("Connection", "close");
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Evolux-Ollama-Inference-Manager/1.0");
                
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync($"{BaseUrl}/api/generate", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"Ollama原始响应长度: {responseContent.Length}, 内容: [{responseContent}]");
                    
                    var generateResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseContent);
                    
                    progressCallback?.Invoke("文本生成完成");
                    _logger.LogInformation("文本生成完成");
                    
                    if (generateResponse == null)
                    {
                        _logger.LogWarning("JSON反序列化失败，返回空响应");
                        return new OllamaGenerateResponse { Response = "生成失败" };
                    }
                    
                    _logger.LogInformation($"反序列化后响应内容: [{generateResponse.Response}]");
                    return generateResponse;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"文本生成失败 (尝试 {attempt}/{maxRetries}): {errorContent}");
                    
                    if (attempt == maxRetries)
                    {
                        progressCallback?.Invoke($"文本生成失败: {errorContent}");
                        return new OllamaGenerateResponse { Response = $"生成失败: {errorContent}" };
                    }
                    
                    // 等待后重试
                    await Task.Delay(retryDelayMs * attempt);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"生成文本时发生异常 (尝试 {attempt}/{maxRetries}): {ex.Message}");
                
                if (attempt == maxRetries)
                {
                    progressCallback?.Invoke($"生成文本时出错: {ex.Message}");
                    return new OllamaGenerateResponse { Response = $"生成失败: {ex.Message}" };
                }
                
                // 等待后重试
                await Task.Delay(retryDelayMs * attempt);
            }
        }
        
        // 不应该到达这里，但为了安全起见
        return new OllamaGenerateResponse { Response = "生成失败: 达到最大重试次数" };
    }

    public async Task StreamGenerateTextAsync(OllamaGenerateRequest request, Action<string> progressCallback, CancellationToken cancellationToken = default)
    {
        try
        {
            progressCallback($"开始流式生成文本，模型: {request.Model}");
            _logger.LogInformation($"开始流式生成文本，模型: {request.Model}");
            
            // ✅ 调试日志：记录最终提示词
            _logger.LogInformation("=== Ollama 流式生成请求 - 最终提示词 ===\n模型: {Model}\n提示词长度: {PromptLength}\n提示词内容:\n{Prompt}\n温度: {Temperature}\n最大Token数: {NumPredict}\n================================",
                request.Model,
                request.Prompt?.Length ?? 0,
                request.Prompt ?? "(空)",
                request.Temperature,
                request.NumPredict);
            
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(30); // 生成可能需要较长时间
            httpClient.DefaultRequestHeaders.Add("Connection", "close");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Evolux-Ollama-Inference-Manager/1.0");
            
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync($"{BaseUrl}/api/generate", content, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                progressCallback(responseContent);
                _logger.LogInformation("流式文本生成完成");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                progressCallback($"流式生成失败: {errorContent}");
                _logger.LogError($"流式生成失败: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            progressCallback($"流式生成文本时出错: {ex.Message}");
            _logger.LogError($"流式生成文本时发生异常: {ex.Message}");
        }
    }

    public async Task<OllamaChatResponse> ChatAsync(OllamaChatRequest request, Action<string>? progressCallback = null)
    {
        try
        {
            progressCallback?.Invoke($"开始聊天，模型: {request.Model}");
            _logger.LogInformation($"开始聊天，模型: {request.Model}");
            
            // ✅ 调试日志：记录最终聊天消息
            var messagesSummary = string.Join("\n", request.Messages?.Select((m, i) => $"[{i + 1}] {m.Role}: {m.Content?.Substring(0, Math.Min(200, m.Content?.Length ?? 0))}{(m.Content?.Length > 200 ? "..." : "")}") ?? Array.Empty<string>());
            _logger.LogInformation("=== Ollama 聊天请求 - 最终消息列表 ===\n模型: {Model}\n消息数量: {MessageCount}\n消息内容:\n{Messages}\n温度: {Temperature}\n================================",
                request.Model,
                request.Messages?.Count ?? 0,
                messagesSummary,
                request.Temperature);
            
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(30); // 聊天可能需要较长时间
            httpClient.DefaultRequestHeaders.Add("Connection", "close");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Evolux-Ollama-Inference-Manager/1.0");
            
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync($"{BaseUrl}/api/chat", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(responseContent);
                
                progressCallback?.Invoke("聊天完成");
                _logger.LogInformation("聊天完成");
                
                return chatResponse ?? new OllamaChatResponse { Message = new OllamaChatMessage { Content = "聊天失败" } };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                progressCallback?.Invoke($"聊天失败: {errorContent}");
                _logger.LogError($"聊天失败: {errorContent}");
                
                return new OllamaChatResponse { Message = new OllamaChatMessage { Content = $"聊天失败: {errorContent}" } };
            }
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke($"聊天时出错: {ex.Message}");
            _logger.LogError($"聊天时发生异常: {ex.Message}");
            
            return new OllamaChatResponse { Message = new OllamaChatMessage { Content = $"聊天失败: {ex.Message}" } };
        }
    }

    public async Task StreamChatAsync(OllamaChatRequest request, Action<string> progressCallback, CancellationToken cancellationToken = default)
    {
        try
        {
            progressCallback($"开始流式聊天，模型: {request.Model}");
            _logger.LogInformation($"开始流式聊天，模型: {request.Model}");
            
            // ✅ 调试日志：记录最终聊天消息
            var messagesSummary = string.Join("\n", request.Messages?.Select((m, i) => $"[{i + 1}] {m.Role}: {m.Content?.Substring(0, Math.Min(200, m.Content?.Length ?? 0))}{(m.Content?.Length > 200 ? "..." : "")}") ?? Array.Empty<string>());
            _logger.LogInformation("=== Ollama 流式聊天请求 - 最终消息列表 ===\n模型: {Model}\n消息数量: {MessageCount}\n消息内容:\n{Messages}\n温度: {Temperature}\n================================",
                request.Model,
                request.Messages?.Count ?? 0,
                messagesSummary,
                request.Temperature);
            
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(30); // 聊天可能需要较长时间
            httpClient.DefaultRequestHeaders.Add("Connection", "close");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Evolux-Ollama-Inference-Manager/1.0");
            
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync($"{BaseUrl}/api/chat", content, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                progressCallback(responseContent);
                _logger.LogInformation("流式聊天完成");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                progressCallback($"流式聊天失败: {errorContent}");
                _logger.LogError($"流式聊天失败: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            progressCallback($"流式聊天时出错: {ex.Message}");
            _logger.LogError($"流式聊天时发生异常: {ex.Message}");
        }
    }

    public async Task<string> GenerateSimpleTextAsync(string model, string prompt, float temperature = 0.7f)
    {
        try
        {
            var request = new OllamaGenerateRequest
            {
                Model = model,
                Prompt = prompt,
                Temperature = temperature
            };
            
            var response = await GenerateTextAsync(request);
            return response.Response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"简单文本生成时发生异常: {ex.Message}");
            return $"生成失败: {ex.Message}";
        }
    }

    public async Task<string> ChatSimpleAsync(string model, string message, float temperature = 0.7f)
    {
        try
        {
            var request = new OllamaChatRequest
            {
                Model = model,
                Messages = new List<OllamaChatMessage>
                {
                    new OllamaChatMessage { Role = "user", Content = message }
                },
                Temperature = temperature
            };
            
            var response = await ChatAsync(request);
            return response.Message?.Content ?? "聊天失败";
        }
        catch (Exception ex)
        {
            _logger.LogError($"简单聊天时发生异常: {ex.Message}");
            return $"聊天失败: {ex.Message}";
        }
    }

    public async Task<ModelWarmupResult> WarmupModelAsync(string model, WarmupOptions options, Action<string>? logCallback = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new ModelWarmupResult
        {
            ModelName = model
        };

        try
        {
            logCallback?.Invoke($"[Warmup] 开始预热模型: {model}");
            _logger.LogInformation("[Warmup] 开始预热模型: {Model}", model);

            // 使用最小生成请求触发模型加载
            var request = new OllamaGenerateRequest
            {
                Model = model,
                Prompt = "ping",  // 最小提示词
                Stream = false,
                NumPredict = options.NumPredict,  // 默认1，仅触发加载
                Temperature = 0.0f  // 确定性输出
            };

            // 使用超时控制（通过 Task.WhenAny 实现）
            var generateTask = GenerateTextAsync(request, logCallback);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(options.TimeoutSecondsPerModel));
            
            var completedTask = await Task.WhenAny(generateTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                throw new TaskCanceledException("预热操作超时");
            }
            
            // 通过 GenerateTextAsync 触发模型加载
            var response = await generateTask;
            
            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;
            result.LoadDurationNs = response.LoadDuration;
            result.Success = true;

            logCallback?.Invoke($"[Warmup] 模型 {model} 预热完成，耗时: {result.DurationMs}ms，加载耗时: {result.LoadDurationNs / 1_000_000}ms");
            _logger.LogInformation("[Warmup] 模型 {Model} 预热完成，总耗时: {Duration}ms，加载耗时: {LoadDuration}ms", 
                model, result.DurationMs, result.LoadDurationNs / 1_000_000);

            return result;
        }
        catch (TaskCanceledException ex)
        {
            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;
            result.Success = false;
            result.ErrorMessage = $"预热超时 ({options.TimeoutSecondsPerModel}秒)";
            
            logCallback?.Invoke($"[Warmup] 模型 {model} 预热超时: {result.ErrorMessage}");
            _logger.LogWarning("[Warmup] 模型 {Model} 预热超时: {Error}", model, ex.Message);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;
            result.Success = false;
            result.ErrorMessage = ex.Message;
            
            logCallback?.Invoke($"[Warmup] 模型 {model} 预热失败: {ex.Message}");
            _logger.LogError(ex, "[Warmup] 模型 {Model} 预热失败", model);
            
            return result;
        }
    }
}
