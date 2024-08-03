using Microsoft.EntityFrameworkCore;
using SourceGenerator.Common;
using tools_server.Entities;

namespace tools_server.Services;

[Service]
public partial class UserService
{
    private readonly ToolsDbContext _db;

    public async Task CreateAsync(string id, string connectionId, string ip)
    {
        var user = _db.Users.FirstOrDefault(m => m.Id == id);
        if (user == null)
        {
            user = new User()
            {
                Id = id,
                ConnectionId = connectionId,
                Ip = ip,
            };
            _db.Users.Add(user);
        }
        else
        {
            user.ConnectionId = connectionId;
        }

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var user = _db.Users.FirstOrDefault(m => m.Id == id);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
    }

    public string GetConnectionId(string id)
    {
        return _db.Users.FirstOrDefault(m => m.Id == id)?.ConnectionId ?? "";
    }

    public async Task<List<User>> GetLanUsersAsync(string ip)
    {
        return await _db.Users.Where(m => m.Ip == ip).ToListAsync();
    }
}
