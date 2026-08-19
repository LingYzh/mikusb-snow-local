using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MikuSB.Proxy;
using MikuSB.SdkServer.Handlers;
using MikuSB.SdkServer.Utils;
using MikuSB.Util;
using System.Text.Json;

namespace MikuSB.SdkServer;

public static class SdkServer
{
    public static void Start(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder
                    .UseStartup<Startup>()
                    .ConfigureLogging((_, logging) => { logging.ClearProviders(); })
                    .ConfigureKestrel(serverOptions =>
                    {
                        // Pre-warm cert before first TLS handshake
                        _ = Utils.CertHelper.GetOrCreate(null);

                        var bindAddr = System.Net.IPAddress.Parse(ConfigManager.Config.HttpServer.BindAddress);
                        var httpPort = ConfigManager.Config.HttpServer.Port;
                        serverOptions.Listen(bindAddr, httpPort);

                        foreach (var port in GetHttpsListenPorts())
                        {
                            if (port == httpPort)
                                continue;
                            if (!CanBindHttps(port))
                                continue;
                            serverOptions.Listen(bindAddr, port, o =>
                            {
                                o.UseHttps(https =>
                                {
                                    https.ServerCertificateSelector = (_, sni) =>
                                        Utils.CertHelper.GetOrCreate(sni);
                                });
                            });
                        }
                    });
            });

        var host = builder.Build();
        host.RunAsync();
    }

    private static IEnumerable<int> GetHttpsListenPorts()
    {
        foreach (var port in new[] { 11443, 13443, 18443, 19443, 31443 })
            yield return port;

        if (CanBindHttps(443))
            yield return 443;
    }

    private static bool CanBindHttps(int port)
    {
        try
        {
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public class Startup
{
    private static bool LooksLikeServerListRequest(string path, string? query)
    {
        var value = $"{path}?{query}".ToLowerInvariant();
        return value.Contains("server")
            || value.Contains("version")
            || value.Contains("query_version")
            || value.Contains("serverlist")
            || value.Contains("/query");
    }

    public static void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

        app.UseRouting();
        app.UseCors("AllowAll");
        app.UseAuthorization();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.Map("{**path}", async context =>
            {
                var path = context.Request.Path.Value ?? "";
                if (LooksLikeServerListRequest(path, context.Request.QueryString.Value))
                {
                    var response = path.Contains("query", StringComparison.OrdinalIgnoreCase)
                        ? (object)RouteController.BuildServerQueryList()
                        : RouteController.BuildServerList("");
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(response));
                    return;
                }
                var fallbackResponse = new
                {
                    code = 1,
                    msg = "操作成功",
                    responseSuccess = true,
                    message = "ok",
                    service = ConfigManager.Config.GameServer.GameServerName,
                    path = path,
                    query = context.Request.QueryString.Value ?? ""
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(fallbackResponse));
            });
        });
    }

    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll",
                builder => { builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader(); });
        });
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = null;
            });
        services.AddSingleton<Logger>(_ => new Logger("Proxy"));
        services.AddMikuSbProxy(ConfigManager.Config.Proxy);
    }
}
