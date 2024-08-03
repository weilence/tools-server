using Microsoft.EntityFrameworkCore;

namespace tools_server.Entities;

public class ToolsDbContext : DbContext
{
    public ToolsDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
}
