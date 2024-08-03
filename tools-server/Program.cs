using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using tools_server;
using tools_server.Controllers;
using tools_server.Entities;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var source = new LogLevelConfigurationSource();
builder.Configuration.AddLogLevelConfiguration(source);
builder.Services.AddSingleton(source);
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = SimpleAuthenticationHandler.SchemaName;
    options.AddScheme<SimpleAuthenticationHandler>(SimpleAuthenticationHandler.SchemaName, null);
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = null;
});
builder.Services.AddAuthorization();
builder.Services.AddDbContext<ToolsDbContext>(options => options.UseSqlite("Data Source=data.db"));
builder.Services.AddAutoServices(ServiceLifetime.Scoped);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ToolsDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();
app.UseLogLevel();
app.MapHub<TransferHub>("/hub/transfer");
app.MapControllers();

app.Run();
