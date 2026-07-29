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

    public async Task<Dictionary<string, string>> GetWorkspaceMetadataJsonAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        // Each entry is captured best-effort; a failure (often InsufficientScopes or a
        // capacity-less workspace) is logged and skipped so it never fails the backup.
        var endpoints = new (string file, string url)[]
        {
            ("workspace.json",         $"workspaces/{workspaceId}"),
            ("roleAssignments.json",   $"workspaces/{workspaceId}/roleAssignments"),
            ("sparkSettings.json",     $"workspaces/{workspaceId}/spark/settings"),
            // Connections are tenant-scoped metadata (no secrets are ever returned); useful for
            // re-binding data sources after a cross-workspace restore.
            ("connections.json",       "connections"),
        };

        var result = new Dictionary<string, string>();

        foreach (var (file, url) in endpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await TryGetRawJsonAsync(url, cancellationToken);
            if (json != null)
                result[file] = json;
        }

        return result;
    }

    // GET that returns the response body on success, or null on any non-success/exception.
    private async Task<string?> TryGetRawJsonAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            await AddAuthHeaderAsync(request, cancellationToken);

            var response = await ExecuteWithRetryAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Workspace metadata GET {Url} → {Status} (skipped)", url, (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Workspace metadata GET {Url} failed (skipped)", url);
            return null;
        }
    }

    public async Task<List<(byte[] content, string partPath)>> GetItemDefinitionAsync(
        string workspaceId,
        string itemId,
        FabricItemType itemType,
        CancellationToken cancellationToken = default)
    {
        // Generic Core endpoint: works for ANY item type that supports the definition API,
        // so we don't have to hard-code a REST path segment per type. New Fabric item types
        // added by Microsoft are covered automatically. Types with no definition API (Warehouse)
        // are routed to OneLake by BackupService before reaching here.
        // Notebooks use ?format=ipynb for a portable Jupyter file.
        // Lakehouses require ?format=LakehouseDefinitionV1 (otherwise returns 400).
        // Reports (PBIR) and Semantic Models (TMDL) use their default multi-part format.
        var formatQuery = GetDefinitionFormatQuery(itemType);
        var url = $"workspaces/{workspaceId}/items/{itemId}/getDefinition{formatQuery}";
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
            // Include the response body so it's visible in the UI log and helps diagnosis.
            var snippet = errorContent.Length > 400 ? errorContent[..400] : errorContent;
            throw new HttpRequestException(
                $"GetDefinition failed ({(int)response.StatusCode} {response.StatusCode}): {snippet}");
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

        // 3. Poll until Succeeded
        var pollContent = await PollLroAsync(locationHeader, cancellationToken);
        var status = JsonSerializer.Deserialize<LROStatusResponse>(pollContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        _logger.LogInformation("[DIAG] LRO Succeeded for {Type}/{ItemId}. Polling body: {Body}",
            itemType, itemId,
            pollContent.Length > 400 ? pollContent[..400] : pollContent);

        // Fabric API v1: la definición puede estar inline en el polling response…
        if (status?.Definition?.Parts != null && status.Definition.Parts.Count > 0)
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

        var resultSnippet = resultJson.Length > 300 ? resultJson[..300] : resultJson;
        throw new InvalidOperationException(
            $"LRO succeeded but no definition parts found. HTTP {(int)resultResp.StatusCode}. Body: {resultSnippet}");
    }

    // ──────────────────────────────────────────────────────────────
    // GetCapacitiesAsync
    // ──────────────────────────────────────────────────────────────

    public async Task<List<FabricCapacity>> GetCapacitiesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching capacities");

        var request = new HttpRequestMessage(HttpMethod.Get, "capacities");
        await AddAuthHeaderAsync(request, cancellationToken);

        var response = await ExecuteWithRetryAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content  = await response.Content.ReadAsStringAsync(cancellationToken);
        var result   = JsonSerializer.Deserialize<CapacitiesResponse>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return result?.Value?.Select(c => new FabricCapacity
        {
            Id          = c.Id,
            DisplayName = c.DisplayName,
            Sku         = c.Sku,
            Region      = c.Region,
            State       = c.State
        }).ToList() ?? new List<FabricCapacity>();
    }

    // ──────────────────────────────────────────────────────────────
    // CreateWorkspaceAsync
    // ──────────────────────────────────────────────────────────────

    public async Task<Workspace> CreateWorkspaceAsync(
        string displayName,
        string capacityId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating workspace '{Name}' on capacity {CapacityId}", displayName, capacityId);

        var body    = JsonSerializer.Serialize(new { displayName, capacityId });
        var request = new HttpRequestMessage(HttpMethod.Post, "workspaces")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        await AddAuthHeaderAsync(request, cancellationToken);

        var response = await ExecuteWithRetryAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var ws = JsonSerializer.Deserialize<Workspace>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Could not parse created workspace response");

        _logger.LogInformation("Created workspace '{Name}' with ID {Id}", ws.Name, ws.Id);
        return ws;
    }

    // ──────────────────────────────────────────────────────────────
    // CreateItemWithDefinitionAsync
    // ──────────────────────────────────────────────────────────────

    public async Task<string> CreateItemWithDefinitionAsync(
        string workspaceId,
        FabricItemType itemType,
        string displayName,
        List<(byte[] content, string partPath)> parts,
        CancellationToken cancellationToken = default)
    {
        // Exact Fabric API type string. Enum member names are kept identical to the API's
        // `type` values, so ToString() is the correct wire value for the generic create endpoint.
        var apiType = itemType.ToString();

        _logger.LogInformation("Creating {Type} '{Name}' in workspace {WorkspaceId}", itemType, displayName, workspaceId);

        object bodyObj;
        if (itemType == FabricItemType.Lakehouse)
        {
            // Lakehouse is created empty — its definition holds only schema/shortcuts metadata,
            // and the data lives in OneLake (handled separately).
            bodyObj = new { displayName, type = apiType };
        }
        else
        {
            var partsArray = parts.Select(p => new
            {
                path        = p.partPath,
                payload     = Convert.ToBase64String(p.content),
                payloadType = "InlineBase64"
            }).ToArray();

            if (itemType == FabricItemType.Notebook)
                bodyObj = new { displayName, type = apiType, definition = new { format = "ipynb", parts = partsArray } };
            else
                bodyObj = new { displayName, type = apiType, definition = new { parts = partsArray } };
        }

        var body    = JsonSerializer.Serialize(bodyObj);
        // Generic Core endpoint — the `type` field in the body selects the item type,
        // so any definition-supporting type can be restored without a per-type route.
        var url     = $"workspaces/{workspaceId}/items";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        await AddAuthHeaderAsync(request, cancellationToken);

        var response = await ExecuteWithRetryAsync(request, cancellationToken);

        // 201 Created — item was created synchronously
        if (response.StatusCode == HttpStatusCode.Created)
        {
            var created = await response.Content.ReadAsStringAsync(cancellationToken);
            var item    = JsonSerializer.Deserialize<CreateItemResponse>(created,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            _logger.LogInformation("Created {Type} '{Name}' synchronously, new ID: {Id}", itemType, displayName, item?.Id);
            return item?.Id ?? string.Empty;
        }

        // 202 Accepted — LRO
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            var locationHeader = response.Headers.Location
                ?? throw new InvalidOperationException("No Location header in 202 Accepted response for CreateItem");

            var pollContent = await PollLroAsync(locationHeader, cancellationToken);
            var lroResult   = JsonSerializer.Deserialize<CreateItemLROResult>(pollContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var newId = lroResult?.CreatedItemId ?? lroResult?.ItemId ?? string.Empty;
            _logger.LogInformation("Created {Type} '{Name}' via LRO, new ID: {Id}", itemType, displayName, newId);
            return newId;
        }

        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"CreateItem failed ({(int)response.StatusCode} {response.StatusCode}): {errorContent}");
    }

    // ──────────────────────────────────────────────────────────────
    // PollLroAsync — shared LRO polling helper
    // ──────────────────────────────────────────────────────────────

    private async Task<string> PollLroAsync(Uri locationUri, CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        while (true)
        {
            if (DateTime.UtcNow - started > _lroTimeout)
                throw new TimeoutException($"LRO polling timeout after {_lroTimeout.TotalMinutes} minutes");

            await Task.Delay(_lroPollingInterval, cancellationToken);

            var pollRequest = new HttpRequestMessage(HttpMethod.Get, locationUri);
            await AddAuthHeaderAsync(pollRequest, cancellationToken);

            var pollResponse = await ExecuteWithRetryAsync(pollRequest, cancellationToken);
            var pollContent  = await pollResponse.Content.ReadAsStringAsync(cancellationToken);

            var status = JsonSerializer.Deserialize<LROStatusResponse>(pollContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            _logger.LogDebug("LRO Status: {Status}", status?.Status);

            if (status?.Status == "Succeeded") return pollContent;

            if (status?.Status == "Failed")
            {
                var error = status.Error?.Message ?? "Unknown error";
                throw new Exception($"LRO failed: {error}");
            }
            // Continue polling (Running / NotStarted)
        }
    }

    // Only a handful of item types accept a ?format= param on getDefinition; the rest use
    // their single implicit format. Returns the query string ("?format=...") or "".
    private static string GetDefinitionFormatQuery(FabricItemType type) => type switch
    {
        FabricItemType.Notebook  => "?format=ipynb",
        FabricItemType.Lakehouse => "?format=LakehouseDefinitionV1",
        _                        => string.Empty
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
        FabricItemType.Report            => ".json",
        FabricItemType.SemanticModel     => ".bim",
        FabricItemType.Notebook          => ".ipynb",
        FabricItemType.DataPipeline      => ".json",
        FabricItemType.Dataflow          => ".json",
        FabricItemType.Lakehouse         => ".json",
        FabricItemType.KQLDatabase       => ".json",
        FabricItemType.Eventhouse        => ".json",
        FabricItemType.Environment       => ".json",
        FabricItemType.SparkJobDefinition => ".json",
        FabricItemType.PaginatedReport   => ".rdl",
        _                                => ".json"
    };

    // Enum member names are kept identical to Fabric's API `type` strings, so a case-insensitive
    // Enum.TryParse maps every known (and future-added) type without a hand-maintained switch.
    private static FabricItemType ParseItemType(string type) =>
        Enum.TryParse<FabricItemType>(type, ignoreCase: true, out var parsed)
            ? parsed
            : FabricItemType.Unknown;

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

    private class CapacitiesResponse
    {
        public List<CapacityDto> Value { get; set; } = new();
    }

    private class CapacityDto
    {
        public string Id          { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Sku         { get; set; } = string.Empty;
        public string Region      { get; set; } = string.Empty;
        public string State       { get; set; } = string.Empty;
    }

    private class CreateItemResponse
    {
        public string Id          { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Type        { get; set; } = string.Empty;
    }

    private class CreateItemLROResult
    {
        // Some Fabric LRO responses use "createdItemId", others "itemId"
        public string? CreatedItemId { get; set; }
        public string? ItemId        { get; set; }
    }
}
