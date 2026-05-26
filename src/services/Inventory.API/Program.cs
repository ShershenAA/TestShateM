using Inventory.API.Consumers;
using Inventory.API.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ──────────────────────────────────────────────
builder.Host.UseSerilog((ctx, config) =>
{
    config
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "Inventory.API")
        .WriteTo.Console();
});

// ── Controllers + Swagger ────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── PostgreSQL + EF Core ─────────────────────────────────
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// ── MassTransit + RabbitMQ ───────────────────────────────
builder.Services.AddMassTransit(x =>
{
    // Регистрируем consumer — MassTransit сам создаст очередь в RabbitMQ
    x.AddConsumer<OrderCreatedConsumer>();

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

// ── Автомиграция ─────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
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
app.UseHttpMetrics();
app.MapMetrics();
app.MapControllers();

app.Run();