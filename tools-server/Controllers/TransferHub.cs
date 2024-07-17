using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using ConcurrentCollections;
using Microsoft.AspNetCore.SignalR;
using SourceGenerator.Common;

namespace tools_server.Controllers;

[Args]
[Logger]
public partial class TransferHub : Hub
{
    private static readonly ConcurrentDictionary<string, Room> _rooms = new();

    public override async Task OnConnectedAsync()
    {
        if (Context.UserIdentifier == null)
        {
            _logger.LogError("{ConnectionId} connected without user identifier", Context.ConnectionId);
            return;
        }

        var ip = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
        if (ip == null)
        {
            _logger.LogError("{user} connected without remote ip", Context.UserIdentifier);
            return;
        }
        var room = _rooms.GetOrAdd(ip, _ => new Room { Name = ip, Users = new ConcurrentHashSet<RoomUser>(new RoomUser()) });
        Context.Items["room"] = ip;
        if (room.Users.Add(new RoomUser { Id = Context.UserIdentifier, ConnectionId = Context.ConnectionId }))
        {
            _logger.LogInformation("{user} connected to {ip}", Context.UserIdentifier, ip);
        }
        else
        {
            _logger.LogError("{user} already connected to {ip}", Context.UserIdentifier, ip);
        }

        var connectionIds = room.Users.Select(x => x.ConnectionId).ToList();
        await Clients.Clients(connectionIds).SendAsync("Connections", room.Users, Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.UserIdentifier == null)
        {
            _logger.LogError("{ConnectionId} disconnected without user identifier", Context.ConnectionId);
            return;
        }

        var ip = Context.Items["room"] as string;
        if (string.IsNullOrEmpty(ip))
        {
            _logger.LogError("{user} disconnected without room", Context.ConnectionId);
            return;
        }

        if (!_rooms.TryGetValue(ip, out var room))
        {
            _logger.LogError("{user} Room {ip} not found", Context.UserIdentifier, ip);
            return;
        }

        if (room.Users.TryRemove(new RoomUser { Id = Context.UserIdentifier }))
        {
            _logger.LogInformation("{user} disconnected to {ip}", Context.UserIdentifier, ip);
        }
        else
        {
            _logger.LogError("{user} already disconnected to {ip}", Context.UserIdentifier, ip);
        }

        var connections = room.Users.Select(x => x.ConnectionId).ToList();
        await Clients.Clients(connections).SendAsync("Connections", room.Users);
    }

    public async Task<JsonDocument> Connect(string userId, JsonDocument offer)
    {
        var connectionId = GetConnectionId(userId);

        return await Clients.Client(connectionId).InvokeAsync<JsonDocument>("Connect", Context.UserIdentifier, offer, Context.ConnectionAborted);
    }

    public async Task IceCandidate(string userId, JsonDocument candidate)
    {
        var connectionId = GetConnectionId(userId);

        await Clients.Client(connectionId).SendAsync("IceCandidate", Context.UserIdentifier, candidate, Context.ConnectionAborted);
    }

    private string GetConnectionId(string userId)
    {
        var ip = Context.Items["room"] as string;
        if (string.IsNullOrEmpty(ip))
        {
            throw new InvalidOperationException("Cannot connect without room");
        }

        if (!_rooms.TryGetValue(ip, out var room))
        {
            throw new InvalidOperationException("Room not found");
        }

        var user = room.Users.FirstOrDefault(x => x.Id == userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        return user.ConnectionId;
    }
}

public class Room
{
    public string Name { get; set; }
    public ConcurrentHashSet<RoomUser> Users { get; set; }
}

public class RoomUser : IEqualityComparer<RoomUser>
{
    public string Id { get; set; }
    public string ConnectionId { get; set; }

    public bool Equals(RoomUser? x, RoomUser? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x == null || y == null)
        {
            return false;
        }

        return x.Id == y.Id;
    }

    public int GetHashCode([DisallowNull] RoomUser obj)
    {
        return obj.Id.GetHashCode();
    }
}
