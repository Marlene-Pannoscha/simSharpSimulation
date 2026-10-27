using PraxisSimulation.Server;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(20),
    KeepAliveTimeout = TimeSpan.FromSeconds(20)
});
app.UseDefaultFiles();
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".data"] = "application/octet-stream";
contentTypes.Mappings[".wasm"] = "application/wasm";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

using var slots = new SemaphoreSlim(16);
app.Map("/ws/simulation", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }
    // Browser-Testclient und WebSocket-Endpunkt gehören zum selben Host.
    var origin = context.Request.Headers.Origin.ToString();
    if (origin.Length > 0 && (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        || !string.Equals(uri.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase)
        || uri.Scheme != context.Request.Scheme))
    {
        context.Response.StatusCode = 403;
        return;
    }
    if (!await slots.WaitAsync(0, context.RequestAborted))
    {
        context.Response.StatusCode = 503;
        return;
    }
    try
    {
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        await new SimulationSocket(socket, app.Logger).RunAsync(context.RequestAborted);
    }
    finally { slots.Release(); }
});
app.Run();
