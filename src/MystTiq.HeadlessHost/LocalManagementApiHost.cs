using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using MystTiq.Core.Models;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed class LocalManagementApiHost : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);

    private LocalManagementApiHost(WebApplication app) => this.app = app;

    public static LocalManagementApiHost Create(
        HeadlessConfiguration configuration,
        IServerLifecycleService lifecycle,
        ILinuxServiceManager serviceManager,
        HeadlessSecretFileService? secretFiles = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(serviceManager);
        secretFiles ??= new HeadlessSecretFileService();

        if (!IPAddress.TryParse(configuration.Api.BindAddress, out var bindAddress))
            throw new InvalidOperationException("Management API bind address must be a literal IP address.");

        var loopback = IPAddress.IsLoopback(bindAddress);
        if (!loopback && (!configuration.Api.Authentication.Enabled || !configuration.Api.Tls.Enabled))
            throw new InvalidOperationException("Non-loopback management API requires both authentication and TLS.");

        string? bearerToken = null;
        if (configuration.Api.Authentication.Enabled)
            bearerToken = secretFiles.ReadRequiredSecret(configuration.Api.Authentication.TokenFile);

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(bindAddress, configuration.Api.Port, listen =>
            {
                if (configuration.Api.Tls.Enabled)
                {
                    var password = secretFiles.ReadRequiredSecret(configuration.Api.Tls.CertificatePasswordFile);
                    var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                        configuration.Api.Tls.CertificatePath,
                        password);
                    listen.UseHttps(certificate);
                }
            });
        });

        var app = builder.Build();
        var host = new LocalManagementApiHost(app);

        app.Use(async (context, next) =>
        {
            if (!configuration.Api.Authentication.Enabled || string.Equals(context.Request.Path.Value, "/healthz", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            var header = context.Request.Headers["Authorization"].ToString();
            const string prefix = "Bearer ";
            if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "missing-bearer-token" });
                return;
            }

            var supplied = header[prefix.Length..].Trim();
            if (bearerToken is null || !HeadlessSecretFileService.FixedTimeEquals(bearerToken, supplied))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "invalid-bearer-token" });
                return;
            }

            await next();
        });

        app.MapGet("/healthz", () => Results.Ok(new
        {
            status = "ok",
            component = "mysttiq-headless",
            api = loopback ? "local" : "remote-secured",
            authentication = configuration.Api.Authentication.Enabled,
            tls = configuration.Api.Tls.Enabled
        }));

        app.MapGet("/api/v1/status", async (CancellationToken token) => Results.Ok(await lifecycle.GetStatusAsync(token)));
        app.MapGet("/api/v1/service", async (CancellationToken token) => Results.Ok(await serviceManager.GetStatusAsync(token)));
        app.MapGet("/api/v1/config", () => Results.Ok(new
        {
            configuration.SchemaVersion,
            Api = new
            {
                configuration.Api.Enabled,
                configuration.Api.BindAddress,
                configuration.Api.Port,
                Scope = loopback ? "loopback" : "remote-secured",
                AuthenticationEnabled = configuration.Api.Authentication.Enabled,
                TlsEnabled = configuration.Api.Tls.Enabled
            },
            configuration.Lifecycle,
            configuration.Server
        }));

        app.MapPost("/api/v1/server/start", async (CancellationToken token) => await host.RunLifecycleActionAsync(() =>
            lifecycle.StartAsync(configuration.Server.LaunchArguments, TimeSpan.FromSeconds(configuration.Lifecycle.StartupTimeoutSeconds), token)));
        app.MapPost("/api/v1/server/stop", async (CancellationToken token) => await host.RunLifecycleActionAsync(() =>
            lifecycle.StopAsync(TimeSpan.FromSeconds(configuration.Lifecycle.StopTimeoutSeconds), token)));
        app.MapPost("/api/v1/server/restart", async (CancellationToken token) => await host.RunLifecycleActionAsync(() =>
            lifecycle.RestartAsync(configuration.Server.LaunchArguments, TimeSpan.FromSeconds(configuration.Lifecycle.StartupTimeoutSeconds), TimeSpan.FromSeconds(configuration.Lifecycle.StopTimeoutSeconds), token)));

        return host;
    }

    public Task StartAsync(CancellationToken cancellationToken) => app.StartAsync(cancellationToken);
    public Task WaitForShutdownAsync(CancellationToken cancellationToken) => ((IHost)app).WaitForShutdownAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => app.StopAsync(cancellationToken);
    public string[] Addresses => app.Urls.ToArray();

    public async ValueTask DisposeAsync()
    {
        lifecycleGate.Dispose();
        await app.DisposeAsync();
    }

    private async Task<IResult> RunLifecycleActionAsync(Func<Task<ServerLifecycleOperationResult>> operation)
    {
        if (!await lifecycleGate.WaitAsync(0)) return Results.Conflict(new { error = "lifecycle-operation-in-progress" });
        try
        {
            var result = await operation();
            return result.Success ? Results.Ok(result) : Results.Json(result, statusCode: MapStatusCode(result.ExitCode));
        }
        finally { lifecycleGate.Release(); }
    }

    private static int MapStatusCode(HeadlessExitCode exitCode) => exitCode switch
    {
        HeadlessExitCode.AlreadyRunning => StatusCodes.Status409Conflict,
        HeadlessExitCode.NotRunning => StatusCodes.Status409Conflict,
        HeadlessExitCode.ServerExecutableMissing => StatusCodes.Status424FailedDependency,
        HeadlessExitCode.StartupTimeout => StatusCodes.Status504GatewayTimeout,
        HeadlessExitCode.StopTimeout => StatusCodes.Status504GatewayTimeout,
        _ => StatusCodes.Status500InternalServerError
    };
}
