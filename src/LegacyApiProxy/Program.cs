using LegacyApiProxy.Background;
using LegacyApiProxy.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Playwright-backed services
builder.Services.AddSingleton<ILegacySiteClient, LegacySiteClient>();
builder.Services.AddSingleton<SessionManager>();

// Background keepalive
builder.Services.AddHostedService<SessionKeepaliveService>();

// ── Pipeline ──────────────────────────────────────────────────────────────────

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Interactive API docs at /scalar/v1
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapControllers();

// Ensure SessionManager is disposed cleanly on shutdown
app.Lifetime.ApplicationStopping.Register(async () =>
{
    var sm = app.Services.GetRequiredService<SessionManager>();
    await sm.DisposeAsync();
});

app.Run();
