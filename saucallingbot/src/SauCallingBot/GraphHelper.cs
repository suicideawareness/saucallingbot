using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

public class GraphHelper
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public GraphHelper(IConfiguration config)
    {
        _config = config;
        _http = new HttpClient();
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        var tenantId = _config["TenantId"] ?? throw new InvalidOperationException("TenantId missing");
        var clientId = _config["MicrosoftAppId"] ?? throw new InvalidOperationException("MicrosoftAppId missing");
        var clientSecret = _config["MicrosoftAppSecret"] ?? throw new InvalidOperationException("MicrosoftAppSecret missing");

        var tokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "client_credentials",
            ["scope"] = "https://graph.microsoft.com/.default"
        };
        using var resp = await _http.PostAsync(tokenEndpoint, new FormUrlEncodedContent(form), ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    private async Task<HttpClient> GetAuthedClientAsync(CancellationToken ct)
    {
        var token = await GetTokenAsync(ct);
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<string> CreateOutboundGroupCallAsync(List<string> userAadObjectIds, string callbackUri, CancellationToken ct)
    {
        var graph = await GetAuthedClientAsync(ct);
        var baseUrl = _config["GraphBaseUrl"] ?? "https://graph.microsoft.com/v1.0";

        var targets = userAadObjectIds.Select(id => new
        {
            @odata_type = "#microsoft.graph.invitationParticipantInfo",
            identity = new
            {
                @odata_type = "#microsoft.graph.identitySet",
                user = new
                {
                    @odata_type = "#microsoft.graph.identity",
                    id = id
                }
            }
        }).ToArray();

        var body = new
        {
            callbackUri = callbackUri,
            requestedModalities = new[] { "audio" },
            mediaConfig = new
            {
                @odata_type = "#microsoft.graph.serviceHostedMediaConfig"
            },
            targets = targets
        };

        var url = $"{baseUrl}/communications/calls";
        using var resp = await graph.PostAsJsonAsync(url, body, ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var callId = json.GetProperty("id").GetString();
        return callId!;
    }

    public async Task<string?> GetCallStateAsync(string callId, CancellationToken ct)
    {
        var graph = await GetAuthedClientAsync(ct);
        var baseUrl = _config["GraphBaseUrl"] ?? "https://graph.microsoft.com/v1.0";
        using var resp = await graph.GetAsync($"{baseUrl}/communications/calls/{callId}", ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        if (json.TryGetProperty("state", out var s))
            return s.GetString();
        return null;
    }

    public async Task<string> PlayPromptAsync(string callId, string audioUrl, CancellationToken ct)
    {
        var graph = await GetAuthedClientAsync(ct);
        var baseUrl = _config["GraphBaseUrl"] ?? "https://graph.microsoft.com/v1.0";

        var body = new
        {
            clientContext = Guid.NewGuid().ToString(),
            prompts = new object[]
            {
                new
                {
                    @odata_type = "#microsoft.graph.mediaPrompt",
                    mediaInfo = new
                    {
                        @odata_type = "#microsoft.graph.mediaInfo",
                        uri = audioUrl,
                        resourceId = Guid.NewGuid().ToString()
                    }
                }
            }
        };

        var url = $"{baseUrl}/communications/calls/{callId}/playPrompt";
        using var resp = await graph.PostAsJsonAsync(url, body, ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        // playPrompt returns an operation object; return its id (optional)
        var opId = json.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        return opId ?? string.Empty;
    }
}
