using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/calling")]
public class CallingController : ControllerBase
{
    // Teams/Graph platform may POST call events to this endpoint.
    // For this simplified sample we just return 200 OK.
    [HttpPost]
    public IActionResult Post([FromBody] object? body)
    {
        // You can log the body if needed.
        return Ok();
    }

    // Some validators send GET with validation tokens - return 200.
    [HttpGet]
    public IActionResult Get() => Ok("calling-callback");
}
