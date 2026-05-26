using Microsoft.AspNetCore.SignalR;
using MassTransit;
using Notifications.API.Hubs;
using Shared.Contracts;

namespace Notifications.API.Consumers;

public class OrderRejectedConsumer : IConsumer<OrderRejected>
{
    private readonly IHubContext<OrderStatusHub> _hub;
    private readonly ILogger<OrderRejectedConsumer> _logger;

    public OrderRejectedConsumer(IHubContext<OrderStatusHub> hub, ILogger<OrderRejectedConsumer> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderRejected> context)
    {
        var message = context.Message;
        _logger.LogInformation("Received OrderRejected: {OrderId}, reason: {Reason}",
            message.OrderId, message.Reason);

        await _hub.Clients
            .Group(message.DealerId.ToString())
            .SendAsync("OrderStatusChanged", new
            {
                orderId = message.OrderId,
                status = "Rejected",
                message = $"Заказ отклонён: {message.Reason}",
                timestamp = message.RejectedAt
            });
    }
}