using System;
using System.Threading.Tasks;
using WslContainerd.Logging.Abstractions;
using WslContainerd.Services.Abstractions;

namespace WslContainerd.Ollama;

/// <summary>
/// Ollama容器管理接口
/// 负责Ollama容器的启动、停止、状态检查等生命周期操作
/// </summary>
public interface IOllamaContainerManager
{
    /// <summary>
    /// 检查Ollama容器是否正在运行
    /// </summary>
    Task<bool> IsRunningAsync();
    
    /// <summary>
    /// 启动Ollama容器
    /// </summary>
    /// <param name="logCallback">日志回调</param>
    Task<bool> StartAsync(Action<string>? logCallback = null);
    
    /// <summary>
    /// 启动Ollama容器（支持自定义模型存储路径）
    /// </summary>
    /// <param name="modelStoragePath">模型存储路径</param>
    /// <param name="logCallback">日志回调</param>
    Task<bool> StartAsync(string? modelStoragePath, Action<string>? logCallback = null);
    
    /// <summary>
    /// 停止Ollama容器
    /// </summary>
    Task<bool> StopAsync();
    
    /// <summary>
    /// 获取Ollama容器状�?
    /// </summary>
    Task<ServiceStatus> GetStatusAsync();
    
    /// <summary>
    /// 获取Ollama版本信息
    /// </summary>
    Task<string> GetVersionAsync();
    
    /// <summary>
    /// 获取Ollama服务信息
    /// </summary>
    Task<string> GetServiceInfoAsync();
    
    /// <summary>
    /// 测试Ollama连接
    /// </summary>
    Task<bool> TestConnectionAsync();
}
