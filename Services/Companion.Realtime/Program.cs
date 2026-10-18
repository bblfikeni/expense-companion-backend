var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Companion.Realtime v1"));

// Stub health endpoint. Migration-gated health (checking database
// connectivity and pending migrations) is a later phase.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// SignalR hubs and endpoint mapping arrive in a later phase.

app.Run();
