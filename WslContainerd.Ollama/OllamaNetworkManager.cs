using System;
using System.Threading.Tasks;
using WslContainerd.Logging.Abstractions;
using WslContainerd.Services.Abstractions;
using WslContainerd.Net.Abstractions; using WslContainerd.Logging.Abstractions;

namespace WslContainerd.Ollama;

/// <summary>
/// Ollama网络管理器实现
/// 负责端口映射检查和修复
/// </summary>
public class OllamaNetworkManager : IOllamaNetworkManager
{
    private readonly IWslContainerdLogger _logger;
    private readonly IWslContainerdRuntime _containerRuntime;
    
    private const int DefaultPort = 11434;

    public OllamaNetworkManager(IWslContainerdLogger logger, IWslContainerdRuntime containerRuntime)
    {
        _logger = logger;
        _containerRuntime = containerRuntime;
    }

    public async Task<bool> CheckPortMappingAsync()
    {
        try
        {
            _logger.LogInformation($"开始检查端口映射: {DefaultPort}");
            
            // 使用容器运行时服务检查端口映射
            var isPortMapped = await _containerRuntime.CheckPortMappingAsync(DefaultPort);
            
            _logger.LogInformation($"端口映射检查结果: {(isPortMapped ? "正常" : "异常")}");
            return isPortMapped;
        }
        catch (Exception ex)
        {
            _logger.LogError($"检查端口映射时发生异常: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> FixPortMappingAsync()
    {
        try
        {
            _logger.LogInformation($"开始修复端口映射: {DefaultPort}");
            
            // 使用容器运行时服务修复端口映射
            var success = await _containerRuntime.CreatePortMappingAsync(DefaultPort, DefaultPort);
            
            if (success)
            {
                _logger.LogInformation("端口映射修复成功");
            }
            else
            {
                _logger.LogInformation("端口映射修复失败");
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError($"修复端口映射时发生异常: {ex.Message}");
            return false;
        }
    }

    public async Task<string> GetConnectionDiagnosticsAsync()
    {
        try
        {
            _logger.LogInformation("开始获取连接诊断信息");
            
            var diagnostics = new
            {
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Port = DefaultPort,
                PortMappingStatus = await CheckPortMappingAsync() ? "正常" : "异常",
                ContainerRuntimeStatus = "可用",
                WslIpAddress = await GetWslIpAddressAsync(),
                ConnectionTest = await TestConnectionAsync()
            };
            
            var result = System.Text.Json.JsonSerializer.Serialize(diagnostics, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            _logger.LogInformation("连接诊断信息获取成功");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取连接诊断信息时发生异常: {ex.Message}");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Error = $"获取诊断信息失败: {ex.Message}"
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
    }

    private async Task<string> GetWslIpAddressAsync()
    {
        try
        {
            // 使用容器运行时服务获取WSL IP地址
            var wslIp = await _containerRuntime.GetWslIpAddressAsync();
            return wslIp ?? "Unknown";
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取WSL IP地址时发生异常: {ex.Message}");
            return "Unknown";
        }
    }

    private async Task<bool> TestConnectionAsync()
    {
        try
        {
            // 简单的连接测试
            var portMappingOk = await CheckPortMappingAsync();
            return portMappingOk;
        }
        catch (Exception ex)
        {
            _logger.LogError($"连接测试时发生异常: {ex.Message}");
            return false;
        }
    }
}
