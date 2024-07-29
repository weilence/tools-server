namespace tools_server;


public class LogLevelConfigurationSource : IConfigurationSource
{
    private readonly IConfigurationProvider _provider = new LogLevelConfigurationProvider();

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return _provider;
    }

    public void Trace(string key)
    {
        Set(key, LogLevel.Trace);
    }

    public void Debug(string key)
    {
        Set(key, LogLevel.Debug);
    }

    public void Information(string key)
    {
        Set(key, LogLevel.Information);
    }

    public void Warning(string key)
    {
        Set(key, LogLevel.Warning);
    }

    public void Error(string key)
    {
        Set(key, LogLevel.Error);
    }

    public void Critical(string key)
    {
        Set(key, LogLevel.Critical);
    }

    public void None(string key)
    {
        Set(key, LogLevel.None);
    }

    private void Set(string key, LogLevel level)
    {
        _provider.Set("Logging:LogLevel:" + key, level.ToString());
    }
}

public class LogLevelConfigurationProvider : ConfigurationProvider
{
    public override void Set(string key, string? value)
    {
        base.Set(key, value);

        OnReload();
    }
}

public static class AspNetCoreExtensions
{
    public static IConfigurationBuilder AddLogLevelConfiguration(this IConfigurationBuilder builder, IConfigurationSource source)
    {
        builder.Add(source);
        return builder;
    }

    public static IApplicationBuilder UseLogLevel(this IApplicationBuilder app)
    {
        var s = app.ApplicationServices.GetRequiredService<LogLevelConfigurationSource>();
        app.Map("/LogLevel", app =>
        {
            app.Run(async context =>
            {
                var key = context.Request.Query["key"].ToString()?.ToLower();
                var level = context.Request.Query["level"].ToString()?.ToLower();
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(level))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Bad Request");
                    return;
                }

                switch (level)
                {
                    case "trace":
                        s.Trace(key);
                        break;
                    case "debug":
                        s.Debug(key);
                        break;
                    case "information":
                        s.Information(key);
                        break;
                    case "warning":
                        s.Warning(key);
                        break;
                    case "error":
                        s.Error(key);
                        break;
                    case "critical":
                        s.Critical(key);
                        break;
                    case "none":
                        s.None(key);
                        break;
                    default:
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsync("Bad Request");
                        return;
                }

                context.Response.StatusCode = 200;
                await context.Response.WriteAsync("OK");
            });
        });
        return app;
    }
}
