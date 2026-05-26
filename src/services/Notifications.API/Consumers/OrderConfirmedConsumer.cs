using Microsoft.AspNetCore.SignalR;
using MassTransit;
using Notifications.API.Hubs;
using Shared.Contracts;

namespace Notifications.API.Consumers;

public class OrderConfirmedConsumer : IConsumer<OrderConfirmed>
{
    private readonly IHubContext<OrderStatusHub> _hub;
    private readonly ILogger<OrderConfirmedConsumer> _logger;

    public OrderConfirmedConsumer(IHubContext<OrderStatusHub> hub, ILogger<OrderConfirmedConsumer> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderConfirmed> context)
    {
        var message = context.Message;
        _logger.LogInformation("Received OrderConfirmed: {OrderId}", message.OrderId);

        // Отправляем уведомление всем клиентам в группе дилера
        await _hub.Clients
            .Group(message.DealerId.ToString())
            .SendAsync("OrderStatusChanged", new
            {
                orderId = message.OrderId,
                status = "Confirmed",
                message = "Ваш заказ подтверждён, товар зарезервирован",
                timestamp = message.ConfirmedAt
            });
    }
}