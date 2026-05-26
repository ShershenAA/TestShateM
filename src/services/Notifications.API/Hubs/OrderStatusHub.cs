using Microsoft.AspNetCore.SignalR;

namespace Notifications.API.Hubs;

public class OrderStatusHub : Hub
{
    private readonly ILogger<OrderStatusHub> _logger;

    public OrderStatusHub(ILogger<OrderStatusHub> logger)
    {
        _logger = logger;
    }

    // Клиент подписывается на уведомления по своему dealerId
    public async Task SubscribeToDealer(string dealerId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, dealerId);
        _logger.LogInformation("Client {ConnectionId} subscribed to dealer {DealerId}",
            Context.ConnectionId, dealerId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}