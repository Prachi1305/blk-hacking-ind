// ============================================================
//  Program.cs  –  ASP.NET Core 8 Minimal Hosting bootstrap
//  Port: 5477 (as required by the challenge)
// ============================================================

using BlkHackingInd.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        // keep property names camelCase (default), pretty-print in dev
        opt.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddSingleton<TransactionService>();
builder.Services.AddSingleton<ReturnsService>();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "BlackRock Auto-Save API",
        Version     = "v1",
        Description = "Production-grade retirement micro-savings API"
    });
    // Allow colon in route via operationId override
    c.CustomOperationIds(e =>
        $"{e.ActionDescriptor.RouteValues["controller"]}_{e.ActionDescriptor.RouteValues["action"]}");
});

// ── Port binding ──────────────────────────────────────────
builder.WebHost.UseUrls("http://*:5477");

// ── Pipeline ──────────────────────────────────────────────
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json",
                                        "BlackRock Auto-Save API v1"));

app.UseRouting();
app.MapControllers();

app.Run();
