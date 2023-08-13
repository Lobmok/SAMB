using Discord;
using Discord.WebSocket;
using Discord.Net;
using Newtonsoft.Json;
using OKX.Api;
using OKX.Api.Models.MarketData;
using System.Collections.Concurrent;

namespace samb;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private DiscordSocketClient _client;
    private readonly OkxService _okx;
    private readonly ConcurrentDictionary<string, ulong> _subscribers = new();
    private SocketTextChannel? _channel;

    public Worker(ILogger<Worker> logger, DiscordSocketClient client, OkxService okx)
    {
        _logger = logger;
        _client = client;
        _okx = okx;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);

        _client = new DiscordSocketClient();
        string token = File.ReadAllText("token.txt").Trim();
        _client.Log += Log;
        _client.Ready += ClientReady;
        _client.SlashCommandExecuted += SlashCommandHandler;
        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        var ws = new OKXStreamClient();
        await _okx.Subscribe(ws);

        await StartCandlestickStream(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
        await _okx.Unsubscribe(ws);
    }

    private Task StartCandlestickStream(CancellationToken stoppingToken)
    {
        return Task.Run(
            async () =>
            {
                await foreach (var data in _okx.CandlestickChannel.ReadAllAsync(stoppingToken))
                {
                    _logger.LogInformation("Candlestick: {@Data}", data);
                    if (_channel is null)
                    {
                        _logger.LogInformation("Channel not found");
                        continue;
                    }
                    var threads = await _channel!.GetActiveThreadsAsync();
                    var thread = threads.FirstOrDefault(c => c.Name == data.Instrument);
                    if (thread is null)
                    {
                        _logger.LogInformation("Thread not found for {Arg}", data.Instrument);
                        continue;
                    }
                    _ = await thread.SendMessageAsync(GetPriceUpdate(data));
                    await Task.Delay(5000, stoppingToken);
                }
            },
            stoppingToken
        );
    }

    private static string GetPriceUpdate(OkxCandlestick data)
    {
        return $"```{data.Time}\nOpen:{data.Open}\nClose:{data.Close}\nHigh:{data.High}\nLow:{data.Low}\n```";
    }

    private Task Log(LogMessage msg)
    {
        _logger.LogInformation("{Arg}", msg.ToString());
        return Task.CompletedTask;
    }

    public async Task ClientReady()
    {
        var lastPrice = new SlashCommandBuilder()
            .WithName("subcribe")
            .WithDescription("Subcribes to a coin")
            .AddOption(
                "coin",
                ApplicationCommandOptionType.String,
                "Name of coin you want to subscribe to",
                true
            );

        try
        {
            _ = await _client.CreateGlobalApplicationCommandAsync(lastPrice.Build());
        }
        catch (HttpException exception)
        {
            string json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);

            _logger.LogInformation("{Arg}", json);
        }
    }

    public async Task SlashCommandHandler(SocketSlashCommand command)
    {
        if (command.Data.Name == "subcribe")
        {
            string? coin = command.Data.Options.First().Value.ToString();

            ulong channelId = (ulong)command.ChannelId!;
            var guild = _client.GetGuild(command.GuildId!.Value);
            var channel = guild.GetTextChannel(channelId);
            //var price = await GetPrice(coin);
            await command.RespondAsync($"{command.User.GlobalName} have subscribed to {coin}");

            ulong threadId = _subscribers!.Any(c => c.Key == coin) ? _subscribers[coin!] : default;
            SocketThreadChannel thread;
            if (threadId is default(ulong))
            {
                _logger.LogInformation("Creating new thread for {Arg}", coin);
                thread = await channel.CreateThreadAsync(coin, ThreadType.PrivateThread);
                threadId = thread.Id;
                _ = _subscribers.TryAdd(coin!, threadId);
            }
            else
            {
                _logger.LogInformation("Getting existing thread for {Arg}", coin);
                thread = channel.Threads.First(c => c.Id == threadId);
            }
            var user = guild.GetUser(command.User.Id);
            await thread.AddUserAsync(user);
            _channel = channel;
            // _ = await thread.SendMessageAsync("Can you see this?");
        }
    }
}
