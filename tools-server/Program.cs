using Microsoft.AspNetCore.HttpOverrides;
using tools_server;
using tools_server.Controllers;

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();

app.UseAuthorization();
app.UseLogLevel();
app.MapHub<TransferHub>("/api/transfer");

app.Run();
