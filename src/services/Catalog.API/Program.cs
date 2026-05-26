using Catalog.API.Data;
using Catalog.API.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Nest;
using Prometheus;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ──────────────────────────────────────────────
builder.Host.UseSerilog((ctx, config) =>
{
    config
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "Catalog.API")
        .WriteTo.Console();
});

// ── Controllers + Swagger ────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── PostgreSQL + EF Core ─────────────────────────────────
builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// ── Redis ────────────────────────────────────────────────
builder.Services.AddSingleton<ICacheService, RedisCacheService>();
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

// ── Elasticsearch ────────────────────────────────────────
var elasticUri = builder.Configuration["Elasticsearch:Uri"]!;
var settings = new ConnectionSettings(new Uri(elasticUri))
    .DefaultIndex("parts")
    .EnableDebugMode(); // убрать в продакшене

builder.Services.AddSingleton<IElasticClient>(new ElasticClient(settings));
builder.Services.AddScoped<ISearchService, ElasticsearchService>();

// ── MassTransit + RabbitMQ ───────────────────────────────
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((ctx, cfg) =>
    {
        var rmq = builder.Configuration.GetSection("RabbitMQ");
        cfg.Host(rmq["Host"], rmq["VHost"], h =>
        {
            h.Username(rmq["User"]!);
            h.Password(rmq["Pass"]!);
        });
        cfg.ConfigureEndpoints(ctx);
    });
});

var app = builder.Build();

// ── Автомиграция при старте ───────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    db.Database.Migrate();
}

// ── Middleware ────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();

// Prometheus — отдаёт метрики на /metrics
app.UseHttpMetrics();
app.MapMetrics();

app.MapControllers();

app.Run();