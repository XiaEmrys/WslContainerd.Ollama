using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Text;
using WslContainerd.Logging.Abstractions;
using WslContainerd.Services.Abstractions;
using WslContainerd.Net.Abstractions; using WslContainerd.Logging.Abstractions;
using System.Threading;
using System.Diagnostics;

namespace WslContainerd.Ollama;

/// <summary>
/// Ollama 服务实现
/// 负责 ollama 启动、ollama list、ollama pull、GPU 检测
/// </summary>
public class OllamaService : IOllamaService
{
    private readonly IWslContainerdLogger _logger;
    private readonly IOllamaContainerManager _containerManager;
    private readonly IOllamaModelManager _modelManager;
    private readonly IOllamaInferenceManager _inferenceManager;
    private readonly IOllamaGpuManager _gpuManager;
    private readonly IOllamaNetworkManager _networkManager;

    public string ServiceName => "ollama";
    public string DisplayName => "Ollama AI 推理";

    public OllamaService(
        IWslContainerdLogger logger,
        IOllamaContainerManager containerManager,
        IOllamaModelManager modelManager,
        IOllamaInferenceManager inferenceManager,
        IOllamaGpuManager gpuManager,
        IOllamaNetworkManager networkManager)
    {
        _logger = logger;
        _containerManager = containerManager;
        _modelManager = modelManager;
        _inferenceManager = inferenceManager;
        _gpuManager = gpuManager;
        _networkManager = networkManager;
    }

    public async Task<bool> IsRunningAsync()
    {
        return await _containerManager.IsRunningAsync();
    }

    public async Task<bool> StartAsync(Action<string>? logCallback = null)
    {
        return await _containerManager.StartAsync(logCallback);
    }

    public async Task<bool> StartAsync(string? modelStoragePath, Action<string>? logCallback = null)
    {
        return await _containerManager.StartAsync(modelStoragePath, logCallback);
    }

    public async Task<bool> StopAsync()
    {
        return await _containerManager.StopAsync();
    }

    public async Task<ServiceStatus> GetStatusAsync()
    {
        return await _containerManager.GetStatusAsync();
    }

    public async Task<bool> TestConnectionAsync()
    {
        return await _containerManager.TestConnectionAsync();
    }

    public async Task<string> GetVersionAsync()
    {
        return await _containerManager.GetVersionAsync();
    }

    public async Task<string> GetServiceInfoAsync()
    {
        return await _containerManager.GetServiceInfoAsync();
    }

    public async Task<string> ListModelsAsync()
    {
        return await _modelManager.ListModelsAsync();
    }

    public async Task<List<OllamaModel>> GetDownloadedModelsAsync()
    {
        return await _modelManager.GetDownloadedModelsAsync();
    }

    public async Task<bool> IsModelDownloadedAsync(string modelName)
    {
        return await _modelManager.IsModelDownloadedAsync(modelName);
    }

    public async Task<bool> DownloadModelAsync(string modelName, Action<DownloadProgressInfo>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        return await _modelManager.DownloadModelAsync(modelName, progressCallback, cancellationToken);
    }

    public async Task<bool> DeleteModelAsync(string modelName)
    {
        return await _modelManager.DeleteModelAsync(modelName);
    }

    public async Task<OllamaModel?> GetModelInfoAsync(string modelName)
    {
        return await _modelManager.GetModelInfoAsync(modelName);
    }

    public async Task<string> GenerateSimpleTextAsync(string model, string prompt, float temperature = 0.7f)
    {
        return await _inferenceManager.GenerateSimpleTextAsync(model, prompt, temperature);
    }

    public async Task<string> ChatSimpleAsync(string model, string message, float temperature = 0.7f)
    {
        return await _inferenceManager.ChatSimpleAsync(model, message, temperature);
    }

    public async Task StreamChatAsync(string model, string message, Action<string> progressCallback, float temperature = 0.7f)
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
        
        await _inferenceManager.StreamChatAsync(request, progressCallback);
    }

    public async Task<bool> CheckGpuSupportAsync()
    {
        return await _gpuManager.CheckGpuSupportAsync();
    }

    public async Task<string> GetGpuInfoAsync()
    {
        return await _gpuManager.GetGpuInfoAsync();
    }

    public async Task<bool> CheckPortMappingAsync()
    {
        return await _networkManager.CheckPortMappingAsync();
    }

    public async Task<bool> FixPortMappingAsync()
    {
        return await _networkManager.FixPortMappingAsync();
    }

    public async Task<string> GetConnectionDiagnosticsAsync()
    {
        return await _networkManager.GetConnectionDiagnosticsAsync();
    }

    public async Task<WarmupResult> WarmupAsync(IEnumerable<string> models, WarmupOptions options, Action<string>? logCallback = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new WarmupResult();
        
        if (!options.Enabled)
        {
            logCallback?.Invoke("[Warmup] 模型预热已禁用，跳过");
            _logger.LogInformation("[Warmup] 模型预热已禁用，跳过");
            result.Success = true;
            result.TotalDurationMs = 0;
            return result;
        }

        var modelList = models.ToList();
        if (modelList.Count == 0)
        {
            logCallback?.Invoke("[Warmup] 没有需要预热的模型");
            _logger.LogInformation("[Warmup] 没有需要预热的模型");
            result.Success = true;
            result.TotalDurationMs = 0;
            return result;
        }

        logCallback?.Invoke($"[Warmup] 开始预热 {modelList.Count} 个模型，并发度: {options.Parallelism}");
        _logger.LogInformation("[Warmup] 开始预热 {Count} 个模型，并发度: {Parallelism}", modelList.Count, options.Parallelism);

        var semaphore = new SemaphoreSlim(options.Parallelism, options.Parallelism);
        var tasks = modelList.Select(async model =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await _inferenceManager.WarmupModelAsync(model, options, logCallback);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        result.ModelResults = results.ToList();
        result.TotalDurationMs = stopwatch.ElapsedMilliseconds;
        result.Success = result.ModelResults.All(r => r.Success);

        var successCount = result.ModelResults.Count(r => r.Success);
        var failedCount = result.ModelResults.Count - successCount;
        
        logCallback?.Invoke($"[Warmup] 预热完成: 成功 {successCount}/{modelList.Count}，总耗时: {result.TotalDurationMs}ms");
        _logger.LogInformation("[Warmup] 预热完成: 成功 {Success}/{Total}，总耗时: {Duration}ms", 
            successCount, modelList.Count, result.TotalDurationMs);

        if (failedCount > 0)
        {
            var failedModels = result.ModelResults.Where(r => !r.Success).Select(r => $"{r.ModelName}({r.ErrorMessage})");
            logCallback?.Invoke($"[Warmup] 预热失败的模型: {string.Join(", ", failedModels)}");
            _logger.LogWarning("[Warmup] 预热失败的模型: {FailedModels}", string.Join(", ", failedModels));
        }

        return result;
    }
}

