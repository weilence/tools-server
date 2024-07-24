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
    private static readonly ConcurrentDictionary<string, User> _users = new();

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId == null)
        {
            throw new InvalidOperationException("Cannot connect without user identifier");
        }

        var roomId = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
        if (roomId == null)
        {
            throw new InvalidOperationException("Cannot connect without remote ip");
        }

        var user = _users.AddOrUpdate(
            userId,
            _ => new User { Id = userId, ConnectionId = Context.ConnectionId, RoomIds = [] },
            (_, user) =>
            {
                user.ConnectionId = Context.ConnectionId;
                return user;
            }
        );

        await joinRoom(user, roomId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (userId == null)
        {
            throw new InvalidOperationException("Cannot connect without user identifier");
        }

        if (_users.TryRemove(userId, out var user))
        {
            foreach (var roomId in user.RoomIds)
            {
                await leaveRoom(user, roomId);
            }
        }
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

    public async Task JoinRoom(string roomId)
    {
        var userId = Context.UserIdentifier;
        if (userId == null)
        {
            throw new InvalidOperationException("Cannot connect without user identifier");
        }

        var user = _users[userId];
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        await joinRoom(user, roomId);
    }

    private async Task joinRoom(User user, string roomId)
    {
        user.RoomIds.Add(roomId);
        var room = _rooms.GetOrAdd(roomId, _ => new Room { Name = roomId, UserIds = new ConcurrentHashSet<string>() });
        room.UserIds.Add(user.Id);

        var users = room.UserIds.Select(x => _users[x]).ToList();
        var connectionIds = users.Select(x => x.ConnectionId).ToList();
        var roomInfo = new RoomInfo()
        {
            Name = room.Name,
            Users = users,
        };

        await Clients.Clients(connectionIds).SendAsync("Connections", roomInfo, Context.ConnectionAborted);
    }

    public async Task LeaveRoom(string roomId)
    {
        var userId = Context.UserIdentifier;
        if (userId == null)
        {
            throw new InvalidOperationException("Cannot connect without user identifier");
        }

        var user = _users[userId];
        await leaveRoom(user, roomId);
    }

    private async Task leaveRoom(User user, string roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            _logger.LogError("{user} Room {ip} not found", user.Id, roomId);
            return;
        }

        if (!room.UserIds.TryRemove(user.Id))
        {
            _logger.LogError("{user} already disconnected to {ip}", user.Id, roomId);
        }

        var users = room.UserIds.Select(x => _users[x]).ToList();
        var connectionIds = users.Select(x => x.ConnectionId).ToList();
        var roomInfo = new RoomInfo()
        {
            Name = room.Name,
            Users = users,
        };

        await Clients.Clients(connectionIds).SendAsync("Connections", roomInfo);
    }

    private string GetConnectionId(string userId)
    {
        if (!_users.TryGetValue(userId, out var user))
        {
            throw new InvalidOperationException("User not found");
        }

        return user.ConnectionId;
    }
}

public class Room
{
    public string Name { get; set; }
    public ConcurrentHashSet<string> UserIds { get; set; }
}

public class RoomInfo
{

    public string Name { get; set; }
    public List<User> Users { get; set; }
}

public class User : IEqualityComparer<User>
{
    public string Id { get; set; }
    public string ConnectionId { get; set; }

    public ConcurrentHashSet<string> RoomIds { get; set; }

    public bool Equals(User? x, User? y)
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

    public int GetHashCode([DisallowNull] User obj)
    {
        return obj.Id.GetHashCode();
    }
}
