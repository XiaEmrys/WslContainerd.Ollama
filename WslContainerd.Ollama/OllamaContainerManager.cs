using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using WslContainerd.Logging.Abstractions;
using WslContainerd.Services.Abstractions;
using WslContainerd.Net.Abstractions;
using System.Linq; // Added for .Where() and .ToList()

namespace WslContainerd.Ollama;

/// <summary>
/// Ollama容器管理器实现
/// 负责Ollama容器的启动、停止、状态检查等生命周期操作
/// </summary>
public class OllamaContainerManager : IOllamaContainerManager
{
    private readonly IWslContainerdLogger _logger;
    private readonly IWslContainerdRuntime _containerRuntime;
    private readonly OllamaServiceOptions _options;
    private string _containerName = string.Empty;

    public OllamaContainerManager(
        IWslContainerdLogger logger,
        IWslContainerdRuntime containerRuntime,
        OllamaServiceOptions? options = null)
    {
        _logger = logger;
        _containerRuntime = containerRuntime;
        _options = options ?? new OllamaServiceOptions();
    }

    private string GenerateContainerName() => _options.ContainerName;

    /// <summary>
    /// 初始化容器名称
    /// </summary>
    private void InitializeContainerName()
    {
        if (string.IsNullOrEmpty(_containerName))
        {
            _containerName = GenerateContainerName();
            _logger.LogInformation($"生成Ollama容器名称: {_containerName}");
        }
    }

    /// <summary>
    /// 清理可能存在的冲突容器和端口映射
    /// </summary>
    private async Task CleanupExistingContainerAsync(Action<string>? logCallback = null)
    {
        try
        {
            // 检查是否有其他ollama容器在运行
            var existingContainers = await _containerRuntime.ListContainersAsync();
            var ollamaContainers = existingContainers.Where(c => c.Name.Contains("ollama")).ToList();
            
            logCallback?.Invoke($"🔍 发现 {ollamaContainers.Count} 个Ollama容器需要清理");
            
            foreach (var container in ollamaContainers)
            {
                logCallback?.Invoke($"🧹 清理冲突容器: {container.Name} (状态: {container.Status})");
                
                // 强制停止容器（无论是否在运行）
                await _containerRuntime.StopContainerAsync(container.Name);
                
                // 等待一下确保容器完全停止
                await Task.Delay(1000);
                
                // 删除容器
                await _containerRuntime.RemoveContainerAsync(container.Name);
                
                logCallback?.Invoke($"✅ 已清理容器: {container.Name}");
            }
            
            // 清理可能存在的端口映射
            logCallback?.Invoke("🧹 清理端口映射: 11434");
            await _containerRuntime.RemovePortMappingAsync(11434);
            
            // 等待一下确保端口释放
            await Task.Delay(2000);
            
            logCallback?.Invoke("✅ 容器和端口清理完成");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"清理冲突容器时出错: {ex.Message}");
            logCallback?.Invoke($"⚠️ 清理过程中出现异常: {ex.Message}");
        }
    }

    public async Task<bool> IsRunningAsync()
    {
        try
        {
            // 如果容器名称未初始化，说明容器未启动
            if (string.IsNullOrEmpty(_containerName))
            {
                return false;
            }

            // 首先检查容器是否正在运行
            var isContainerRunning = await _containerRuntime.IsContainerRunningAsync(_containerName);
            if (!isContainerRunning)
            {
                _logger.LogInformation("Ollama容器未在运行");
                return false;
            }

            // 然后检查API是否可访问
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Add("Connection", "close");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Evolux-Ollama-Manager/1.0");
            var response = await httpClient.GetAsync($"{_options.BaseUrl}/api/tags");
            var isApiAccessible = response.IsSuccessStatusCode;
            
            if (isApiAccessible)
            {
                _logger.LogInformation("Ollama服务运行正常，API可访问");
            }
            else
            {
                _logger.LogWarning("Ollama容器在运行但API不可访问");
            }
            
            return isApiAccessible;
        }
        catch (Exception ex)
        {
            _logger.LogError($"检查Ollama运行状态时出错: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> StartAsync(Action<string>? logCallback = null)
    {
        return await StartAsync(null, logCallback);
    }

    public async Task<bool> StartAsync(string? modelStoragePath, Action<string>? logCallback = null)
    {
        try
        {
            logCallback?.Invoke("启动 Ollama 服务...");
            _logger.LogInformation("开始启动Ollama容器");
            
            // 初始化容器名称
            InitializeContainerName();
            
            // 首先检查服务是否已经运行
            if (await IsRunningAsync())
            {
                logCallback?.Invoke("Ollama 服务已经在运行");
                _logger.LogInformation("Ollama服务已经在运行");
                return true;
            }
            
            // 清理可能存在的冲突容器
            await CleanupExistingContainerAsync(logCallback);
            
            // 检查容器是否存在
            var containerExists = await _containerRuntime.ContainerExistsAsync(_containerName);
            
            if (containerExists)
            {
                logCallback?.Invoke("Ollama 容器已存在，尝试启动...");
                
                // 容器存在，尝试启动
                var startSuccess = await _containerRuntime.StartContainerAsync(_containerName, logCallback);
                
                if (startSuccess)
                {
                    logCallback?.Invoke("Ollama 容器启动成功");
                    _logger.LogInformation("Ollama容器启动成功");
                    
                    // 等待服务就绪
                    logCallback?.Invoke("等待 Ollama 服务就绪...");
                    var isReady = await WaitForServiceReadyAsync(logCallback);
                    
                    if (isReady)
                    {
                        logCallback?.Invoke("Ollama 服务启动成功");
                        return true;
                    }
                    else
                    {
                        logCallback?.Invoke("Ollama 服务启动超时");
                        return false;
                    }
                }
                else
                {
                    logCallback?.Invoke("Ollama 容器启动失败");
                    _logger.LogError("Ollama容器启动失败");
                    return false;
                }
            }
            else
            {
                logCallback?.Invoke($"Ollama 容器不存在，开始创建: {_containerName}");
                
                // 创建并启动容器
                var ports = new Dictionary<string, string>
                {
                    { "11434", "11434" }
                };
                
                var environment = new Dictionary<string, string>
                {
                    { "OLLAMA_HOST", "0.0.0.0" }
                };
                
                // 配置卷挂载（模型存储路径）
                var volumes = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(modelStoragePath))
                {
                    volumes[modelStoragePath] = "/root/.ollama/models";  // 挂载到容器内的默认路径
                    logCallback?.Invoke($"📁 配置模型存储路径: {modelStoragePath} -> /root/.ollama/models");
                }
                
                var success = await _containerRuntime.RunContainerAsync(
                    _options.ImageName,
                    _containerName,
                    ports,
                    environment,
                    volumes,
                    logCallback);
                
                if (success)
                {
                    logCallback?.Invoke("Ollama 容器创建并启动成功");
                    _logger.LogInformation("Ollama容器创建并启动成功");
                    
                    // 等待服务就绪
                    logCallback?.Invoke("等待 Ollama 服务就绪...");
                    var isReady = await WaitForServiceReadyAsync(logCallback);
                    
                    if (isReady)
                    {
                        logCallback?.Invoke("Ollama 服务启动成功");
                        return true;
                    }
                    else
                    {
                        logCallback?.Invoke("Ollama 服务启动超时");
                        return false;
                    }
                }
                else
                {
                    logCallback?.Invoke("Ollama 容器创建失败");
                    _logger.LogError("Ollama容器创建失败");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            logCallback?.Invoke($"启动Ollama服务时出错: {ex.Message}");
            _logger.LogError($"启动Ollama容器时发生异常: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> StopAsync()
    {
        try
        {
            _logger.LogInformation("开始停止Ollama容器");
            
            if (string.IsNullOrEmpty(_containerName))
            {
                _logger.LogInformation("Ollama容器名称未初始化，无需停止");
                return true;
            }
            
            var success = await _containerRuntime.StopContainerAsync(_containerName);
            
            if (success)
            {
                _logger.LogInformation("Ollama容器停止成功");
            }
            else
            {
                _logger.LogInformation("Ollama容器停止失败");
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError($"停止Ollama容器时发生异常: {ex.Message}");
            return false;
        }
    }

    public async Task<ServiceStatus> GetStatusAsync()
    {
        try
        {
            if (await IsRunningAsync())
            {
                return new ServiceStatus("ollama", "Ollama", ServiceState.Running);
            }
            return new ServiceStatus("ollama", "Ollama", ServiceState.Stopped);
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取Ollama状态时发生异常: {ex.Message}");
            return new ServiceStatus("ollama", "Ollama", ServiceState.Unknown);
        }
    }

    public async Task<string> GetVersionAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Add("Connection", "close");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Evolux-Ollama-Manager/1.0");
            var response = await httpClient.GetStringAsync($"{_options.BaseUrl}/api/version");
            var versionInfo = JsonSerializer.Deserialize<JsonElement>(response);
            return versionInfo.GetProperty("version").GetString() ?? "Unknown";
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取Ollama版本时发生异常: {ex.Message}");
            return "Unknown";
        }
    }

    public async Task<string> GetServiceInfoAsync()
    {
        try
        {
            var version = await GetVersionAsync();
            var status = await GetStatusAsync();
            return $"Ollama {version} - {status.State}";
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取Ollama服务信息时发生异常: {ex.Message}");
            return "Ollama 服务信息获取失败";
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        return await IsRunningAsync();
    }

    /// <summary>
    /// 等待服务就绪
    /// </summary>
    private async Task<bool> WaitForServiceReadyAsync(Action<string>? logCallback = null)
    {
        const int maxAttempts = 30; // 最多等待30次
        const int delayMs = 2000;   // 每次等待2秒
        
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                logCallback?.Invoke($"🔍 尝试连接 API: {_options.BaseUrl}/api/tags (尝试 {attempt}/{maxAttempts})");
                
                // 使用更直接的HttpClient配置，避免连接池问题
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                httpClient.DefaultRequestHeaders.Add("Connection", "close");
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Evolux-Ollama-Manager/1.0");
                
                var response = await httpClient.GetAsync($"{_options.BaseUrl}/api/tags");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    logCallback?.Invoke($"✅ Ollama 服务就绪 (尝试 {attempt}/{maxAttempts}) - 响应: {content}");
                    return true;
                }
                else
                {
                    logCallback?.Invoke($"⚠️ API响应状态码: {response.StatusCode} (尝试 {attempt}/{maxAttempts})");
                }
            }
            catch (TaskCanceledException ex)
            {
                // 超时异常
                logCallback?.Invoke($"⏰ API请求超时: {ex.Message} (尝试 {attempt}/{maxAttempts})");
                _logger.LogWarning($"API请求超时 (尝试 {attempt}/{maxAttempts}): {ex.Message}");
            }
            catch (HttpRequestException ex)
            {
                // HTTP请求异常
                logCallback?.Invoke($"🌐 HTTP请求异常: {ex.Message} (尝试 {attempt}/{maxAttempts})");
                _logger.LogWarning($"HTTP请求异常 (尝试 {attempt}/{maxAttempts}): {ex.Message}");
            }
            catch (Exception ex)
            {
                // 其他异常
                logCallback?.Invoke($"❌ API请求异常: {ex.GetType().Name} - {ex.Message} (尝试 {attempt}/{maxAttempts})");
                _logger.LogWarning($"API请求异常 (尝试 {attempt}/{maxAttempts}): {ex.GetType().Name} - {ex.Message}");
            }
            
            logCallback?.Invoke($"⏳ 等待 Ollama 服务就绪... ({attempt}/{maxAttempts})");
            await Task.Delay(delayMs);
        }
        
        logCallback?.Invoke("❌ Ollama 服务启动超时");
        return false;
    }
}
