using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WslContainerd.Logging.Abstractions;
using WslContainerd.Services.Abstractions;

namespace WslContainerd.Ollama;

/// <summary>
/// Ollama模型管理接口
/// 负责模型的列表、下载、删除、信息查询等操作
/// </summary>
public interface IOllamaModelManager
{
    /// <summary>
    /// 列出所有模�?
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
    /// 流式下载模型
    /// </summary>
    /// <param name="modelName">模型名称</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> StreamDownloadModelAsync(string modelName, Action<DownloadProgressInfo> progressCallback, CancellationToken cancellationToken = default);
    
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
}
