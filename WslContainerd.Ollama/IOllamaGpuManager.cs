using System.Threading.Tasks;

namespace WslContainerd.Ollama;

/// <summary>
/// Ollama GPU管理接口
/// 负责GPU支持检测和信息获取
/// </summary>
public interface IOllamaGpuManager
{
    /// <summary>
    /// 检查GPU支持
    /// </summary>
    Task<bool> CheckGpuSupportAsync();
    
    /// <summary>
    /// 获取GPU信息
    /// </summary>
    Task<string> GetGpuInfoAsync();
}
