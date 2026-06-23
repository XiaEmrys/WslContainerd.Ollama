using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using WslContainerd.Logging.Abstractions;
using WslContainerd.Services.Abstractions;

namespace WslContainerd.Ollama;

/// <summary>
/// Ollama模型管理器实现
/// 负责模型的列表、下载、删除、信息查询等操作
/// </summary>
public class OllamaModelManager : IOllamaModelManager
{
    private readonly IWslContainerdLogger _logger;
    
    private const string BaseUrl = "http://localhost:11434";

    public OllamaModelManager(IWslContainerdLogger logger)
    {
        _logger = logger;
        // 移除静态HttpClient实例，改为每次请求时创建新的实例
    }

    public async Task<string> ListModelsAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Add("Connection", "close");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Evolux-Ollama-Model-Manager/1.0");
            
            var response = await httpClient.GetStringAsync($"{BaseUrl}/api/tags");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取模型列表时发生异常: {ex.Message}");
            return "[]";
        }
    }

    public async Task<List<OllamaModel>> GetDownloadedModelsAsync()
    {
        var models = new List<OllamaModel>();
        
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Add("Connection", "close");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Evolux-Ollama-Model-Manager/1.0");
            
            var response = await httpClient.GetStringAsync($"{BaseUrl}/api/tags");
            var tagsResponse = JsonSerializer.Deserialize<JsonElement>(response);
            
            if (tagsResponse.TryGetProperty("models", out var modelsElement))
            {
                foreach (var modelElement in modelsElement.EnumerateArray())
                {
                    var model = new OllamaModel
                    {
                        Name = modelElement.GetProperty("name").GetString() ?? "",
                        Size = modelElement.GetProperty("size").GetInt64().ToString(),
                        ModifiedAt = modelElement.GetProperty("modified_at").GetString() ?? "",
                        Digest = modelElement.GetProperty("digest").GetString() ?? "",
                        IsDownloaded = true,
                        Status = "已下载"
                    };
                    models.Add(model);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取已下载模型列表时发生异常: {ex.Message}");
        }
        
        return models;
    }

    public async Task<bool> IsModelDownloadedAsync(string modelName)
    {
        try
        {
            var models = await GetDownloadedModelsAsync();
            return models.Any(m => m.Name == modelName);
        }
        catch (Exception ex)
        {
            _logger.LogError($"检查模型下载状态时发生异常: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DownloadModelAsync(string modelName, Action<DownloadProgressInfo>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        try
        {
            progressCallback?.Invoke(DownloadProgressInfo.CreateStart(modelName));
            _logger.LogInformation($"开始下载模型: {modelName}");
            
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(30); // 下载可能需要更长时间
            httpClient.DefaultRequestHeaders.Add("Connection", "close");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Evolux-Ollama-Model-Manager/1.0");
            
            var request = new
            {
                name = modelName
            };
            
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            // 使用流式处理，不等待完整响应
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/pull")
            {
                Content = content
            };
            
            // 发送请求但不等待完整响应，传递取消令牌
            using var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                // 立即开始读取流式响应
                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);
                
                long totalSize = 0;
                long completedSize = 0;
                
                // 初始化速度计算器
                var speedCalculator = new DownloadSpeedCalculator();
                
                progressCallback?.Invoke(DownloadProgressInfo.CreateProgress(modelName, 0, 0, "开始接收下载数据..."));
                
                var startTime = DateTime.UtcNow;
                var timeout = TimeSpan.FromMinutes(30); // 30分钟超时
                
                while (!reader.EndOfStream)
                {
                    // 检查取消令牌
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 检查超时
                    if (DateTime.UtcNow - startTime > timeout)
                    {
                        _logger.LogError("下载超时");
                        progressCallback?.Invoke(DownloadProgressInfo.CreateFailed(modelName, "下载超时"));
                        return false;
                    }
                    
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) continue;
                    
                    _logger.LogDebug($"收到Ollama响应: {line}");
                    
                    try
                    {
                        var jsonElement = JsonSerializer.Deserialize<JsonElement>(line);
                        
                        if (jsonElement.TryGetProperty("status", out var statusElement))
                        {
                            var status = statusElement.GetString();
                            _logger.LogDebug($"处理状态: {status}");
                            
                            // 处理下载进度状态（包括 "downloading"、"pulling xxx" 和 "downloading xxx" 格式）
                            if (status == "downloading" || status?.StartsWith("pulling ") == true || status?.StartsWith("downloading ") == true)
                            {
                                try
                                {
                                    if (jsonElement.TryGetProperty("total", out var totalElement))
                                    {
                                        if (totalElement.ValueKind == JsonValueKind.Number)
                                        {
                                            totalSize = totalElement.GetInt64();
                                        }
                                        else if (totalElement.ValueKind == JsonValueKind.String)
                                        {
                                            var totalString = totalElement.GetString();
                                            if (long.TryParse(totalString, out var parsedTotal))
                                            {
                                                totalSize = parsedTotal;
                                            }
                                        }
                                    }
                                    
                                    if (jsonElement.TryGetProperty("completed", out var completedElement))
                                    {
                                        if (completedElement.ValueKind == JsonValueKind.Number)
                                        {
                                            completedSize = completedElement.GetInt64();
                                        }
                                        else if (completedElement.ValueKind == JsonValueKind.String)
                                        {
                                            var completedString = completedElement.GetString();
                                            if (long.TryParse(completedString, out var parsedCompleted))
                                            {
                                                completedSize = parsedCompleted;
                                            }
                                        }
                                        
                                        if (totalSize > 0)
                                        {
                                            // 计算下载速度和剩余时间
                                            var currentTime = DateTime.UtcNow;
                                            var downloadSpeed = speedCalculator.CalculateSpeed(completedSize, currentTime);
                                            var estimatedTimeRemaining = speedCalculator.CalculateRemainingTime(totalSize, completedSize, downloadSpeed);
                                            
                                            // 使用带速度信息的进度创建方法
                                            var progressInfo = DownloadProgressInfo.CreateProgress(
                                                modelName, 
                                                totalSize, 
                                                completedSize, 
                                                downloadSpeed, 
                                                estimatedTimeRemaining
                                            );
                                            
                                            progressCallback?.Invoke(progressInfo);
                                            _logger.LogDebug($"下载进度: {progressInfo.ProgressPercentage}% ({completedSize}/{totalSize} bytes) - {DownloadSpeedCalculator.FormatSpeed(downloadSpeed)}");
                                        }
                                    }
                                }
                                catch (Exception parseEx)
                                {
                                    _logger.LogWarning($"解析下载进度数据失败: {parseEx.Message}");
                                }
                            }
                            // 处理验证状态（包括 "verifying" 和 "verifying xxx" 格式）
                            else if (status == "verifying" || status?.StartsWith("verifying ") == true)
                            {
                                progressCallback?.Invoke(DownloadProgressInfo.CreateProgress(modelName, totalSize, completedSize, "正在验证模型文件..."));
                                _logger.LogInformation("开始验证模型文件");
                            }
                            // 处理成功状态（包括 "success"、"done" 等）
                            else if (status == "success" || status == "done" || status?.StartsWith("success") == true)
                            {
                                progressCallback?.Invoke(DownloadProgressInfo.CreateCompleted(modelName));
                                _logger.LogInformation($"模型 {modelName} 下载完成");
                                return true;
                            }
                            // 处理错误状态（包括 "error"、"failed" 等）
                            else if (status == "error" || status == "failed" || status?.StartsWith("error") == true)
                            {
                                if (jsonElement.TryGetProperty("error", out var errorElement))
                                {
                                    var error = errorElement.GetString();
                                    progressCallback?.Invoke(DownloadProgressInfo.CreateFailed(modelName, error ?? "未知错误"));
                                    _logger.LogError($"模型 {modelName} 下载失败: {error}");
                                    return false;
                                }
                                else
                                {
                                    progressCallback?.Invoke(DownloadProgressInfo.CreateFailed(modelName, status ?? "未知错误"));
                                    _logger.LogError($"模型 {modelName} 下载失败: {status}");
                                    return false;
                                }
                            }
                            // 处理其他未知状态
                            else
                            {
                                _logger.LogDebug($"收到未知状态: {status}");
                                // 对于未知状态，尝试解析进度信息（某些版本可能使用不同的状态名）
                                try
                                {
                                    if (jsonElement.TryGetProperty("total", out var totalElement) && 
                                        jsonElement.TryGetProperty("completed", out var completedElement))
                                    {
                                        long tempTotal = 0, tempCompleted = 0;
                                        
                                        if (totalElement.ValueKind == JsonValueKind.Number)
                                            tempTotal = totalElement.GetInt64();
                                        else if (totalElement.ValueKind == JsonValueKind.String && long.TryParse(totalElement.GetString(), out var parsedTotal))
                                            tempTotal = parsedTotal;
                                            
                                        if (completedElement.ValueKind == JsonValueKind.Number)
                                            tempCompleted = completedElement.GetInt64();
                                        else if (completedElement.ValueKind == JsonValueKind.String && long.TryParse(completedElement.GetString(), out var parsedCompleted))
                                            tempCompleted = parsedCompleted;
                                            
                                        if (tempTotal > 0)
                                        {
                                            var progressMessage = $"下载进度: {tempCompleted}/{tempTotal} bytes";
                                            var progressInfo = DownloadProgressInfo.CreateProgress(modelName, tempTotal, tempCompleted, progressMessage);
                                            progressCallback?.Invoke(progressInfo);
                                            _logger.LogDebug($"下载进度: {progressInfo.ProgressPercentage}% ({tempCompleted}/{tempTotal} bytes)");
                                        }
                                    }
                                }
                                catch (Exception parseEx)
                                {
                                    _logger.LogDebug($"解析未知状态进度数据失败: {parseEx.Message}");
                                }
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning($"解析下载进度JSON失败: {ex.Message}, 原始数据: {line}");
                    }
                }
                
                progressCallback?.Invoke(DownloadProgressInfo.CreateCompleted(modelName));
                _logger.LogInformation($"模型 {modelName} 下载完成");
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                progressCallback?.Invoke(DownloadProgressInfo.CreateFailed(modelName, errorContent));
                _logger.LogError($"模型 {modelName} 下载失败: {errorContent}");
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation($"模型 {modelName} 下载已取消");
            progressCallback?.Invoke(DownloadProgressInfo.CreateCancelled(modelName));
            throw; // 重新抛出取消异常，让上层处理
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke(DownloadProgressInfo.CreateFailed(modelName, ex.Message));
            _logger.LogError($"下载模型 {modelName} 时发生异常: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> StreamDownloadModelAsync(string modelName, Action<DownloadProgressInfo> progressCallback, CancellationToken cancellationToken = default)
    {
        try
        {
            progressCallback(DownloadProgressInfo.CreateStart(modelName));
            _logger.LogInformation($"开始流式下载模型: {modelName}");
            
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(30); // 下载可能需要更长时间
            httpClient.DefaultRequestHeaders.Add("Connection", "close");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Evolux-Ollama-Model-Manager/1.0");
            
            var request = new
            {
                name = modelName
            };
            
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            // 使用流式处理，不等待完整响应
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/pull")
            {
                Content = content
            };
            
            // 发送请求但不等待完整响应
            using var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                // 立即开始读取流式响应
                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);
                
                long totalSize = 0;
                long completedSize = 0;
                
                // 初始化速度计算器
                var speedCalculator = new DownloadSpeedCalculator();
                var startTime = DateTime.UtcNow;
                var timeout = TimeSpan.FromMinutes(30); // 30分钟超时
                
                progressCallback(DownloadProgressInfo.CreateProgress(modelName, 0, 0, "开始接收下载数据..."));
                
                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    // 检查超时
                    if (DateTime.UtcNow - startTime > timeout)
                    {
                        _logger.LogError("下载超时");
                        progressCallback(DownloadProgressInfo.CreateFailed(modelName, "下载超时"));
                        return false;
                    }
                    
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) continue;
                    
                    _logger.LogDebug($"收到Ollama响应: {line}");
                    
                    try
                    {
                        var jsonElement = JsonSerializer.Deserialize<JsonElement>(line);
                        
                        if (jsonElement.TryGetProperty("status", out var statusElement))
                        {
                            var status = statusElement.GetString();
                            _logger.LogDebug($"处理状态: {status}");
                            
                            // 处理下载进度状态（包括 "downloading"、"pulling xxx" 和 "downloading xxx" 格式）
                            if (status == "downloading" || status?.StartsWith("pulling ") == true || status?.StartsWith("downloading ") == true)
                            {
                                try
                                {
                                    if (jsonElement.TryGetProperty("total", out var totalElement))
                                    {
                                        if (totalElement.ValueKind == JsonValueKind.Number)
                                        {
                                            totalSize = totalElement.GetInt64();
                                        }
                                        else if (totalElement.ValueKind == JsonValueKind.String)
                                        {
                                            var totalString = totalElement.GetString();
                                            if (long.TryParse(totalString, out var parsedTotal))
                                            {
                                                totalSize = parsedTotal;
                                            }
                                        }
                                    }
                                    
                                    if (jsonElement.TryGetProperty("completed", out var completedElement))
                                    {
                                        if (completedElement.ValueKind == JsonValueKind.Number)
                                        {
                                            completedSize = completedElement.GetInt64();
                                        }
                                        else if (completedElement.ValueKind == JsonValueKind.String)
                                        {
                                            var completedString = completedElement.GetString();
                                            if (long.TryParse(completedString, out var parsedCompleted))
                                            {
                                                completedSize = parsedCompleted;
                                            }
                                        }
                                        
                                        if (totalSize > 0)
                                        {
                                            var progressMessage = $"下载进度: {completedSize}/{totalSize} bytes";
                                            var progressInfo = DownloadProgressInfo.CreateProgress(modelName, totalSize, completedSize, progressMessage);
                                            progressCallback(progressInfo);
                                            _logger.LogDebug($"下载进度: {progressInfo.ProgressPercentage}% ({completedSize}/{totalSize} bytes)");
                                        }
                                    }
                                }
                                catch (Exception parseEx)
                                {
                                    _logger.LogWarning($"解析下载进度数据失败: {parseEx.Message}");
                                }
                            }
                            // 处理验证状态（包括 "verifying" 和 "verifying xxx" 格式）
                            else if (status == "verifying" || status?.StartsWith("verifying ") == true)
                            {
                                progressCallback(DownloadProgressInfo.CreateProgress(modelName, totalSize, completedSize, "正在验证模型文件..."));
                                _logger.LogInformation("开始验证模型文件");
                            }
                            // 处理成功状态（包括 "success"、"done" 等）
                            else if (status == "success" || status == "done" || status?.StartsWith("success") == true)
                            {
                                progressCallback(DownloadProgressInfo.CreateCompleted(modelName));
                                _logger.LogInformation($"模型 {modelName} 流式下载完成");
                                return true;
                            }
                            // 处理错误状态（包括 "error"、"failed" 等）
                            else if (status == "error" || status == "failed" || status?.StartsWith("error") == true)
                            {
                                if (jsonElement.TryGetProperty("error", out var errorElement))
                                {
                                    var error = errorElement.GetString();
                                    progressCallback(DownloadProgressInfo.CreateFailed(modelName, error ?? "未知错误"));
                                    _logger.LogError($"模型 {modelName} 流式下载失败: {error}");
                                    return false;
                                }
                                else
                                {
                                    progressCallback(DownloadProgressInfo.CreateFailed(modelName, status ?? "未知错误"));
                                    _logger.LogError($"模型 {modelName} 流式下载失败: {status}");
                                    return false;
                                }
                            }
                            // 处理其他未知状态
                            else
                            {
                                _logger.LogDebug($"收到未知状态: {status}");
                                // 对于未知状态，尝试解析进度信息（某些版本可能使用不同的状态名）
                                try
                                {
                                    if (jsonElement.TryGetProperty("total", out var totalElement) && 
                                        jsonElement.TryGetProperty("completed", out var completedElement))
                                    {
                                        long tempTotal = 0, tempCompleted = 0;
                                        
                                        if (totalElement.ValueKind == JsonValueKind.Number)
                                            tempTotal = totalElement.GetInt64();
                                        else if (totalElement.ValueKind == JsonValueKind.String && long.TryParse(totalElement.GetString(), out var parsedTotal))
                                            tempTotal = parsedTotal;
                                            
                                        if (completedElement.ValueKind == JsonValueKind.Number)
                                            tempCompleted = completedElement.GetInt64();
                                        else if (completedElement.ValueKind == JsonValueKind.String && long.TryParse(completedElement.GetString(), out var parsedCompleted))
                                            tempCompleted = parsedCompleted;
                                            
                                        if (tempTotal > 0)
                                        {
                                            var progressMessage = $"下载进度: {tempCompleted}/{tempTotal} bytes";
                                            var progressInfo = DownloadProgressInfo.CreateProgress(modelName, tempTotal, tempCompleted, progressMessage);
                                            progressCallback(progressInfo);
                                            _logger.LogDebug($"下载进度: {progressInfo.ProgressPercentage}% ({tempCompleted}/{tempTotal} bytes)");
                                        }
                                    }
                                }
                                catch (Exception parseEx)
                                {
                                    _logger.LogDebug($"解析未知状态进度数据失败: {parseEx.Message}");
                                }
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning($"解析下载进度JSON失败: {ex.Message}, 原始数据: {line}");
                    }
                }
                
                if (cancellationToken.IsCancellationRequested)
                {
                    progressCallback(DownloadProgressInfo.CreateCancelled(modelName));
                    return false;
                }
                
                progressCallback(DownloadProgressInfo.CreateCompleted(modelName));
                _logger.LogInformation($"模型 {modelName} 流式下载完成");
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                progressCallback(DownloadProgressInfo.CreateFailed(modelName, errorContent));
                _logger.LogError($"模型 {modelName} 流式下载失败: {errorContent}");
                return false;
            }
        }
        catch (Exception ex)
        {
            progressCallback(DownloadProgressInfo.CreateFailed(modelName, ex.Message));
            _logger.LogError($"流式下载模型 {modelName} 时发生异常: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteModelAsync(string modelName)
    {
        try
        {
            _logger.LogInformation($"开始删除模型: {modelName}");
            
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Add("Connection", "close");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Evolux-Ollama-Model-Manager/1.0");
            
            var request = new
            {
                name = modelName
            };
            
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            // 使用 DELETE 请求删除模型，Ollama API 支持 DELETE 方法携带请求体
            using var requestMessage = new HttpRequestMessage(HttpMethod.Delete, $"{BaseUrl}/api/delete")
            {
                Content = content
            };
            var response = await httpClient.SendAsync(requestMessage);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"模型 {modelName} 删除成功");
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"模型 {modelName} 删除失败: {errorContent}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"删除模型 {modelName} 时发生异常: {ex.Message}");
            return false;
        }
    }

    public async Task<OllamaModel?> GetModelInfoAsync(string modelName)
    {
        try
        {
            var models = await GetDownloadedModelsAsync();
            return models.FirstOrDefault(m => m.Name == modelName);
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取模型信息时发生异常: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 判断是否为下载进度状态
    /// </summary>
    private static bool IsDownloadProgressStatus(string? status)
    {
        if (string.IsNullOrEmpty(status)) return false;
        
        // 常见的下载进度状态格式
        var downloadStatuses = new[]
        {
            "downloading",
            "pulling",
            "download",
            "pull"
        };
        
        return downloadStatuses.Any(s => status.StartsWith(s, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 判断是否为验证状态
    /// </summary>
    private static bool IsVerifyingStatus(string? status)
    {
        if (string.IsNullOrEmpty(status)) return false;
        
        return status.StartsWith("verifying", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断是否为成功状态
    /// </summary>
    private static bool IsSuccessStatus(string? status)
    {
        if (string.IsNullOrEmpty(status)) return false;
        
        var successStatuses = new[]
        {
            "success",
            "done",
            "complete",
            "finished"
        };
        
        return successStatuses.Any(s => status.StartsWith(s, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 判断是否为错误状态
    /// </summary>
    private static bool IsErrorStatus(string? status)
    {
        if (string.IsNullOrEmpty(status)) return false;
        
        var errorStatuses = new[]
        {
            "error",
            "failed",
            "failure",
            "abort",
            "cancelled"
        };
        
        return errorStatuses.Any(s => status.StartsWith(s, StringComparison.OrdinalIgnoreCase));
    }
}
