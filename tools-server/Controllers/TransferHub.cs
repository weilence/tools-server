using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
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

        var room = _rooms.GetOrAdd(roomId, _ => new Room { Name = roomId });
        await joinRoom(user, room);
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

    public async Task JoinRoom(string roomId, string password)
    {
        var userId = Context.UserIdentifier;
        if (userId == null)
        {
            throw new InvalidOperationException("Cannot connect without user identifier");
        }

        var user = _users.GetValueOrDefault(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        if (password == "")
        {
            throw new InvalidOperationException("Password is required");
        }

        var room = _rooms.GetValueOrDefault(roomId);
        if (room == null)
        {
            room = _rooms.GetOrAdd(roomId, _ => new Room { Name = roomId, Password = password });
        }
        else if (password != room.Password)
        {
            throw new InvalidOperationException("Password is incorrect");
        }

        await joinRoom(user, room);
    }

    private async Task joinRoom(User user, Room room)
    {
        user.RoomIds.Add(room.Name);
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

        if (user.RoomIds.Count == 0)
        {
            _users.Remove(userId, out _);
        }
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

        if (room.UserIds.Count == 0)
        {
            _rooms.TryRemove(roomId, out _);
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

    public void AddPassword(string roomId, string password)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            throw new InvalidOperationException("Room not found");
        }

        if (password == "")
        {
            throw new InvalidOperationException("Password is required");
        }

        room.Password = password;
    }
}

public class Room
{
    public required string Name { get; set; }
    [JsonIgnore]
    public string Password { get; set; } = "";
    public ConcurrentHashSet<string> UserIds { get; set; } = [];
}

public class RoomInfo
{

    public required string Name { get; set; }
    public List<User> Users { get; set; } = [];
}

public class User : IEqualityComparer<User>
{
    public required string Id { get; set; }
    public required string ConnectionId { get; set; }

    public ConcurrentHashSet<string> RoomIds { get; set; } = [];

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
