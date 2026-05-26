using MassTransit;
using Notifications.API.Consumers;
using Notifications.API.Hubs;
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
        .Enrich.WithProperty("Service", "Notifications.API")
        .WriteTo.Console();
});

// ── Controllers + Swagger ────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── SignalR + Redis backplane ─────────────────────────────
// Redis нужен чтобы SignalR работал на нескольких инстансах
builder.Services.AddSignalR()
    .AddStackExchangeRedis(
        builder.Configuration.GetConnectionString("Redis")!,
        options => options.Configuration.ChannelPrefix =
            RedisChannel.Literal("autoparts"));

// ── MassTransit + RabbitMQ ───────────────────────────────
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderConfirmedConsumer>();
    x.AddConsumer<OrderRejectedConsumer>();

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

// ── CORS — нужен для SignalR из браузера ─────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();;
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseRouting();
app.UseHttpMetrics();
app.MapMetrics();
app.MapControllers();

// Регистрируем Hub — клиенты подключаются по этому адресу
app.MapHub<OrderStatusHub>("/hubs/orders");

app.Run();