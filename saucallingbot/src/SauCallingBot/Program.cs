using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Read settings
var config = builder.Configuration;

// CORS (optional if calling from Power Automate only)
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));

builder.Services.Configure<JsonOptions>(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = null;
});

builder.Services.AddSingleton<GraphHelper>();

builder.Services.AddControllers();

var app = builder.Build();
app.UseCors();
app.MapControllers();

// Health endpoint
app.MapGet("/", () => Results.Ok(new { status = "ok", service = "SauCallingBot" }));
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
