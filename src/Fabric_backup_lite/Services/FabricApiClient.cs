using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Fabric_backup_lite.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Fabric_backup_lite.Services;

public class FabricApiClient : IFabricApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationService _authService;
    private readonly ILogger<FabricApiClient> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
    private readonly int _lroPollingInterval;
    private readonly TimeSpan _lroTimeout = TimeSpan.FromMinutes(5);

    public FabricApiClient(
        IAuthenticationService authService,
        IConfiguration configuration,
        ILogger<FabricApiClient> logger)
    {
        _authService = authService;
        _logger = logger;

        var baseUrl = configuration["Fabric:BaseUrl"] ?? "https://api.fabric.microsoft.com/v1";
        var timeout = configuration.GetValue<int>("Fabric:Timeout", 120);
        var retryAttempts = configuration.GetValue<int>("Fabric:RetryAttempts", 3);
        _lroPollingInterval = configuration.GetValue<int>("Fabric:LROPollingInterval", 2000);

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(timeout)
        };

        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r =>
                r.StatusCode == HttpStatusCode.TooManyRequests ||
                r.StatusCode >= HttpStatusCode.InternalServerError)
            .WaitAndRetryAsync(
                retryAttempts,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Request failed with {StatusCode}. Waiting {Delay}s before retry #{Retry}",
                        outcome.Result?.StatusCode,
                        timespan.TotalSeconds,
                        retryCount);
                });
    }

    public async Task<List<Workspace>> GetWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching workspaces");

        var request = new HttpRequestMessage(HttpMethod.Get, "workspaces");
        await AddAuthHeaderAsync(request, cancellationToken);

        var response = await ExecuteWithRetryAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<WorkspacesResponse>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        _logger.LogInformation("Found {Count} workspaces", result?.Value?.Count ?? 0);
        return result?.Value ?? new List<Workspace>();
    }

    public async Task<List<FabricItem>> GetWorkspaceItemsAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching items for workspace {WorkspaceId}", workspaceId);

        var request = new HttpRequestMessage(HttpMethod.Get, $"workspaces/{workspaceId}/items");
        await AddAuthHeaderAsync(request, cancellationToken);

        var response = await ExecuteWithRetryAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<ItemsResponse>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var items = result?.Value?.Select(item => new FabricItem
        {
            Id = item.Id,
            DisplayName = item.DisplayName,
            Type = ParseItemType(item.Type),
            Description = item.Description ?? string.Empty,
            WorkspaceId = workspaceId
        }).ToList() ?? new List<FabricItem>();

        _logger.LogInformation("Found {Count} items in workspace", items.Count);
        return items;
    }

    public async Task<List<(byte[] content, string partPath)>> GetItemDefinitionAsync(
        string workspaceId,
        string itemId,
        FabricItemType itemType,
        CancellationToken cancellationToken = default)
    {
        // Each Fabric item type has its own endpoint — the generic /items/{id}/getDefinition does not exist
        var typeSegment = GetTypeSegment(itemType)
            ?? throw new NotSupportedException(
                $"El tipo '{itemType}' no tiene endpoint de exportación en la API de Fabric.");

        // Notebooks require ?format=ipynb to get a standard Jupyter file.
        // Reports use PBIR format (multiple JSON parts) by default — no format param needed.
        var formatQuery = itemType == FabricItemType.Notebook ? "?format=ipynb" : string.Empty;
        var url = $"workspaces/{workspaceId}/{typeSegment}/{itemId}/getDefinition{formatQuery}";
        _logger.LogInformation("Getting definition for item {ItemId} ({Type}) via {Url}", itemId, itemType, url);

        // 1. Iniciar LRO con POST
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        await AddAuthHeaderAsync(request, cancellationToken);

        var response = await ExecuteWithRetryAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Accepted && response.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("GetDefinition failed: {StatusCode} - {Content}",
                response.StatusCode, errorContent);
            throw new HttpRequestException($"GetDefinition failed: {response.StatusCode}");
        }

        // Respuesta inmediata (200 OK sin LRO)
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var immediateContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var definition = JsonSerializer.Deserialize<DefinitionResponse>(immediateContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (definition?.Definition?.Parts != null && definition.Definition.Parts.Count > 0)
                return ExtractDefinitionContent(definition, itemType);

            throw new InvalidOperationException("No definition parts found in immediate response");
        }

        // 2. LRO (202 Accepted): obtener Location header para polling
        var locationHeader = response.Headers.Location
            ?? throw new InvalidOperationException("No Location header in 202 Accepted response");

        // 3. Polling hasta completar
        var pollingStartTime = DateTime.UtcNow;
        while (true)
        {
            if (DateTime.UtcNow - pollingStartTime > _lroTimeout)
                throw new TimeoutException($"LRO polling timeout after {_lroTimeout.TotalMinutes} minutes");

            await Task.Delay(_lroPollingInterval, cancellationToken);

            var pollRequest = new HttpRequestMessage(HttpMethod.Get, locationHeader);
            await AddAuthHeaderAsync(pollRequest, cancellationToken);

            var pollResponse = await ExecuteWithRetryAsync(pollRequest, cancellationToken);
            var pollContent  = await pollResponse.Content.ReadAsStringAsync(cancellationToken);

            var status = JsonSerializer.Deserialize<LROStatusResponse>(pollContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            _logger.LogDebug("LRO Status: {Status}", status?.Status);

            if (status?.Status == "Succeeded")
            {
                _logger.LogInformation("[DIAG] LRO Succeeded for {Type}/{ItemId}. Polling body: {Body}",
                    itemType, itemId,
                    pollContent.Length > 400 ? pollContent[..400] : pollContent);

                // Fabric API v1: la definición puede estar inline en el polling response…
                if (status.Definition?.Parts != null && status.Definition.Parts.Count > 0)
                    return ExtractDefinitionContent(status, itemType);

                // …o en la URL de resultado de la operación: GET {operationUrl}/result
                var resultUrl = locationHeader.ToString().TrimEnd('/') + "/result";
                var resultReq  = new HttpRequestMessage(HttpMethod.Get, new Uri(resultUrl));
                await AddAuthHeaderAsync(resultReq, cancellationToken);

                var resultResp = await ExecuteWithRetryAsync(resultReq, cancellationToken);
                var resultJson = await resultResp.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogInformation("[DIAG] Result URL {Url} → {Status} | Body: {Body}",
                    resultUrl,
                    (int)resultResp.StatusCode,
                    resultJson.Length > 800 ? resultJson[..800] : resultJson);

                if (!resultResp.IsSuccessStatusCode)
                    throw new InvalidOperationException(
                        $"Result URL returned {resultResp.StatusCode}: {resultJson}");

                var resultDef = JsonSerializer.Deserialize<DefinitionResponse>(resultJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (resultDef?.Definition?.Parts != null && resultDef.Definition.Parts.Count > 0)
                    return ExtractDefinitionContent(resultDef, itemType);

                // Include the raw JSON in the error so it appears in the UI log and helps diagnosis.
                var snippet = resultJson.Length > 300 ? resultJson[..300] : resultJson;
                throw new InvalidOperationException(
                    $"LRO succeeded but no definition parts found. HTTP {(int)resultResp.StatusCode}. Body: {snippet}");
            }

            if (status?.Status == "Failed")
            {
                var error = status.Error?.Message ?? "Unknown error";
                throw new Exception($"LRO failed: {error}");
            }

            // Continue polling (Running / NotStarted)
        }
    }

    // Maps FabricItemType to the Fabric REST API path segment for that item type.
    // Returns null for types that do not expose a getDefinition endpoint.
    private static string? GetTypeSegment(FabricItemType type) => type switch
    {
        FabricItemType.Notebook      => "notebooks",
        FabricItemType.DataPipeline  => "dataPipelines",
        FabricItemType.Report        => "reports",
        FabricItemType.SemanticModel => "semanticModels",
        FabricItemType.Dataflow      => "dataflows",
        _                            => null   // Lakehouse, Warehouse, KQLDatabase, Unknown
    };

    // Returns one entry per definition part: (decoded bytes, relative path as declared by the API).
    // Single-file items (Notebook, DataPipeline, Dataflow, SemanticModel) return a list with one entry.
    // Multi-file items (Report / PBIR) return multiple entries preserving the directory structure.
    private List<(byte[] content, string partPath)> ExtractDefinitionContent(
        DefinitionResponse definition,
        FabricItemType itemType)
    {
        var result = new List<(byte[] content, string partPath)>();

        foreach (var part in definition.Definition.Parts)
        {
            if (string.IsNullOrEmpty(part.Payload))
            {
                _logger.LogWarning("Skipping part '{Path}' — empty payload", part.Path);
                continue;
            }

            var bytes    = Convert.FromBase64String(part.Payload);
            // Use the path from the API when available; fall back to a sensible default.
            var partPath = !string.IsNullOrEmpty(part.Path)
                ? part.Path
                : $"definition{GetDefaultExtension(itemType)}";

            _logger.LogInformation("Extracted part '{Path}', {Size} bytes", partPath, bytes.Length);
            result.Add((bytes, partPath));
        }

        if (result.Count == 0)
            throw new InvalidOperationException("All definition parts had empty payloads");

        return result;
    }

    private static string GetDefaultExtension(FabricItemType itemType) => itemType switch
    {
        FabricItemType.Report        => ".json",
        FabricItemType.SemanticModel => ".bim",
        FabricItemType.Notebook      => ".ipynb",
        FabricItemType.DataPipeline  => ".json",
        FabricItemType.Dataflow      => ".json",
        _                            => ".json"
    };

    private FabricItemType ParseItemType(string type)
    {
        return type?.ToLowerInvariant() switch
        {
            "report" => FabricItemType.Report,
            "semanticmodel" => FabricItemType.SemanticModel,
            "notebook" => FabricItemType.Notebook,
            "datapipeline" => FabricItemType.DataPipeline,
            "dataflow" => FabricItemType.Dataflow,
            "lakehouse" => FabricItemType.Lakehouse,
            "warehouse" => FabricItemType.Warehouse,
            "kqldatabase" => FabricItemType.KQLDatabase,
            _ => FabricItemType.Unknown
        };
    }

    private async Task AddAuthHeaderAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _authService.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<HttpResponseMessage> ExecuteWithRetryAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            // Clone request porque HttpClient consume el request
            var clonedRequest = await CloneHttpRequestMessageAsync(request);
            return await _httpClient.SendAsync(clonedRequest, cancellationToken);
        });
    }

    private async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        if (request.Content != null)
        {
            var content = await request.Content.ReadAsStringAsync();
            clone.Content = new StringContent(content, Encoding.UTF8, "application/json");
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    // DTOs para deserialización
    private class WorkspacesResponse
    {
        public List<Workspace> Value { get; set; } = new();
    }

    private class ItemsResponse
    {
        public List<ItemDto> Value { get; set; } = new();
    }

    private class ItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    private class LROStatusResponse : DefinitionResponse
    {
        public string Status { get; set; } = string.Empty;
        public ErrorInfo? Error { get; set; }
    }

    private class DefinitionResponse
    {
        public DefinitionData Definition { get; set; } = new();
    }

    private class DefinitionData
    {
        public List<DefinitionPart> Parts { get; set; } = new();
    }

    private class DefinitionPart
    {
        public string Path { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public string PayloadType { get; set; } = string.Empty;
    }

    private class ErrorInfo
    {
        public string Message { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
    }
}
