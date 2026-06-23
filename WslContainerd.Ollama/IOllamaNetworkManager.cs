using System.Threading.Tasks;

namespace WslContainerd.Ollama;

/// <summary>
/// Ollama网络管理接口
/// 负责端口映射检查和修复
/// </summary>
public interface IOllamaNetworkManager
{
    /// <summary>
    /// 检查端口映�?
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
}
