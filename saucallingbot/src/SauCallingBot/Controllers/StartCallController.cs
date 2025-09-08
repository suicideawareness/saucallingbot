using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/startcall")]
public class StartCallController : ControllerBase
{
    private readonly GraphHelper _graph;
    private readonly IConfiguration _config;

    public StartCallController(GraphHelper graph, IConfiguration config)
    {
        _graph = graph;
        _config = config;
    }

    public class StartCallRequest
    {
        public List<string> Users { get; set; } = new();
        public string? AudioFileUrl { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> StartCall([FromBody] StartCallRequest req, CancellationToken ct)
    {
        if (req.Users == null || req.Users.Count < 2)
            return BadRequest("Provide at least two AAD object IDs in 'Users'.");

        var audioUrl = string.IsNullOrWhiteSpace(req.AudioFileUrl)
            ? _config["DefaultAudioUrl"]
            : req.AudioFileUrl;

        if (string.IsNullOrWhiteSpace(audioUrl))
            return BadRequest("AudioFileUrl is required.");

        // 1) Create the call (service-hosted media)
        var callbackUri = $"{_config["BotBaseUrl"]?.TrimEnd('/')}/api/calling";
        var callId = await _graph.CreateOutboundGroupCallAsync(req.Users, callbackUri!, ct);

        // 2) Poll until connected (simple demo)
        var timeout = TimeSpan.FromSeconds(60);
        var start = DateTimeOffset.UtcNow;
        string? state;
        do
        {
            await Task.Delay(2000, ct);
            state = await _graph.GetCallStateAsync(callId, ct);
            if (string.Equals(state, "connected", StringComparison.OrdinalIgnoreCase))
                break;
        } while (DateTimeOffset.UtcNow - start < timeout);

        if (!string.Equals(state, "connected", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new { callId, status = "Call not connected within timeout", state });
        }

        // 3) Play prompt (audio)
        var opId = await _graph.PlayPromptAsync(callId, audioUrl!, ct);

        return Ok(new
        {
            callId,
            playPromptOperationId = opId,
            status = "Audio prompt requested"
        });
    }
}
