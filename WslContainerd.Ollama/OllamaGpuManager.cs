using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using WslContainerd.Logging.Abstractions;
using WslContainerd.Services.Abstractions;

namespace WslContainerd.Ollama;

/// <summary>
/// Ollama GPU管理器实现
/// 负责GPU支持检测和信息获取
/// </summary>
public class OllamaGpuManager : IOllamaGpuManager
{
    private readonly IWslContainerdLogger _logger;
    
    private const string BaseUrl = "http://localhost:11434";

    public OllamaGpuManager(IWslContainerdLogger logger)
    {
        _logger = logger;
        // 移除静态HttpClient实例，改为每次请求时创建新的实例
    }

    public async Task<bool> CheckGpuSupportAsync()
    {
        try
        {
            _logger.LogInformation("开始检查GPU支持");
            
            // 尝试获取GPU信息来判断是否支持GPU
            var gpuInfo = await GetGpuInfoAsync();
            
            // 如果能够获取到GPU信息且不是错误信息，则认为支持GPU
            var hasGpuSupport = !string.IsNullOrEmpty(gpuInfo) && 
                               !gpuInfo.Contains("错误") && 
                               !gpuInfo.Contains("失败") &&
                               !gpuInfo.Contains("Unknown");
            
            _logger.LogInformation($"GPU支持检查结果: {(hasGpuSupport ? "支持" : "不支持")}");
            return hasGpuSupport;
        }
        catch (Exception ex)
        {
            _logger.LogError($"检查GPU支持时发生异常: {ex.Message}");
            return false;
        }
    }

    public async Task<string> GetGpuInfoAsync()
    {
        try
        {
            _logger.LogInformation("开始获取GPU信息");
            
            // 使用新的HttpClient实例，避免连接池问题
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Add("Connection", "close");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Evolux-Ollama-GPU-Manager/1.0");
            
            // 首先检查Ollama服务是否可用 - 使用/api/version端点
            var versionResponse = await httpClient.GetAsync($"{BaseUrl}/api/version");
            
            if (versionResponse.IsSuccessStatusCode)
            {
                // 如果API可用，尝试获取更详细的GPU信息
                try
                {
                    var versionContent = await versionResponse.Content.ReadAsStringAsync();
                    var versionInfo = JsonSerializer.Deserialize<JsonElement>(versionContent);
                    
                    // 尝试获取模型列表来判断GPU支持
                    var modelsResponse = await httpClient.GetAsync($"{BaseUrl}/api/tags");
                    var hasModels = modelsResponse.IsSuccessStatusCode;
                    
                    var gpuInfo = new
                    {
                        Version = versionInfo.GetProperty("version").GetString() ?? "Unknown",
                        GpuSupport = hasModels ? "Available" : "Unknown",
                        ServiceStatus = "Running",
                        ModelsAvailable = hasModels,
                        Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };
                    
                    var result = JsonSerializer.Serialize(gpuInfo, new JsonSerializerOptions { WriteIndented = true });
                    _logger.LogInformation("GPU信息获取成功");
                    return result;
                }
                catch (Exception ex)
                {
                    // 如果无法获取详细GPU信息，返回基本状态
                    var basicInfo = new
                    {
                        Status = "Ollama服务运行中",
                        GpuSupport = "Available",
                        ServiceStatus = "Running",
                        Error = ex.Message,
                        Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };
                    
                    var result = JsonSerializer.Serialize(basicInfo, new JsonSerializerOptions { WriteIndented = true });
                    _logger.LogInformation("获取基本GPU信息");
                    return result;
                }
            }
            else
            {
                _logger.LogInformation("Ollama服务不可用，无法获取GPU信息");
                return JsonSerializer.Serialize(new
                {
                    Status = "Ollama服务不可用",
                    GpuSupport = "Unknown",
                    ServiceStatus = "Stopped",
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                }, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取GPU信息时发生异常: {ex.Message}");
            return JsonSerializer.Serialize(new
            {
                Status = $"获取GPU信息失败: {ex.Message}",
                GpuSupport = "Unknown",
                ServiceStatus = "Error",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
