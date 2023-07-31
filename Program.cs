using samb;
using Discord.WebSocket;
using Discord.Commands;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddSingleton<DiscordSocketClient>();
        services.AddSingleton<CommandService>();
    })
    .Build();

await host.RunAsync();
