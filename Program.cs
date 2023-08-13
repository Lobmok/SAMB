#pragma warning disable IDE0058
using samb;
using Discord.WebSocket;
using Discord.Commands;
using Serilog;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(c => c.ClearProviders())
    .UseSerilog(
        (h, c) =>
        {
            _ = c.ReadFrom.Configuration(h.Configuration);
        }
    )
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddSingleton<DiscordSocketClient>();
        services.AddSingleton<CommandService>();
        services.AddSingleton<OkxService>();
    })
    .Build();

await host.RunAsync();
