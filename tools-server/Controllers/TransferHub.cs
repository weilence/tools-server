using System.Collections.Concurrent;
using System.Text.Json;
using ConcurrentCollections;
using Microsoft.AspNetCore.SignalR;
using SourceGenerator.Common;

namespace tools_server.Controllers;

[Args]
[Logger]
public partial class TransferHub : Hub
{
    private readonly RoomStore _roomStore;

    public override async Task OnConnectedAsync()
    {
        var ip = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
        if (ip == null)
        {
            return;
        }
        Context.Items["room"] = ip;

        _logger.LogInformation("{connectionId} connected to {ip}", Context.ConnectionId, ip);

        var connections = _roomStore.Add(ip, Context.ConnectionId);
        await Clients.Clients(connections).SendAsync("Connections", connections, Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var ip = Context.Items["room"] as string;
        if (string.IsNullOrEmpty(ip))
        {
            return;
        }

        _roomStore.Remove(ip, Context.ConnectionId);

        var connections = _roomStore.GetConnections(ip);
        await Clients.Clients(connections).SendAsync("Connections", connections);

        _logger.LogInformation("{connectionId} disconnected to {ip}", Context.ConnectionId, ip);
    }

    public async Task<JsonDocument> Connect(string connectionId, JsonDocument offer)
    {
        return await Clients.Client(connectionId).InvokeAsync<JsonDocument>("Answer", connectionId, offer, Context.ConnectionAborted);
    }

    public async Task IceCandidate(string connectionId, JsonDocument candidate)
    {
        await Clients.Client(connectionId).SendAsync("IceCandidate", connectionId, candidate, Context.ConnectionAborted);
    }
}

public class RoomStore
{
    private readonly ConcurrentDictionary<string, ConcurrentHashSet<string>> _rooms = new();

    public ConcurrentHashSet<string> Add(string room, string connectionId)
    {
        return _rooms.AddOrUpdate(room, new ConcurrentHashSet<string> { connectionId }, (_, list) =>
        {
            list.Add(connectionId);
            return list;
        });
    }

    public ConcurrentHashSet<string> Remove(string room, string connectionId)
    {
        return _rooms.AddOrUpdate(room, new ConcurrentHashSet<string>(), (_, list) =>
        {
            list.TryRemove(connectionId);
            return list;
        });
    }

    public ConcurrentHashSet<string> GetConnections(string room)
    {
        return _rooms.GetOrAdd(room, _ => new ConcurrentHashSet<string>());
    }
}