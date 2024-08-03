using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using SourceGenerator.Common;
using tools_server.Services;

namespace tools_server.Controllers;

[Args]
[Logger]
public partial class TransferHub : Hub
{
    private readonly UserService _userService;

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId == null)
        {
            throw new InvalidOperationException("Cannot connect without user identifier");
        }

        var ip = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
        if (ip == null)
        {
            throw new InvalidOperationException("Cannot connect without remote ip");
        }

        await _userService.CreateAsync(userId, Context.ConnectionId, ip);
        _logger.LogDebug("User {user} connected", userId);

        await Groups.AddToGroupAsync(Context.ConnectionId, ip);

        var users = await _userService.GetLanUsersAsync(ip);
        await Clients.Group(ip).SendAsync("Users", users, Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (userId == null)
        {
            throw new InvalidOperationException("Cannot connect without user identifier");
        }

        await _userService.DeleteAsync(userId);
        _logger.LogDebug("User {user} disconnected", userId);

        var ip = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
        if (ip == null)
        {
            throw new InvalidOperationException("Cannot connect without remote ip");
        }

        var users = _userService.GetLanUsersAsync(ip);
        await Clients.Group(ip).SendAsync("Users", users);
    }

    public async Task<JsonDocument> Connect(string userId, JsonDocument offer)
    {
        var connectionId = _userService.GetConnectionId(userId);

        return await Clients.Client(connectionId).InvokeAsync<JsonDocument>("Connect", Context.UserIdentifier, offer, Context.ConnectionAborted);
    }

    public async Task IceCandidate(string userId, JsonDocument candidate)
    {
        var connectionId = _userService.GetConnectionId(userId);

        await Clients.Client(connectionId).SendAsync("IceCandidate", Context.UserIdentifier, candidate, Context.ConnectionAborted);
    }
}
