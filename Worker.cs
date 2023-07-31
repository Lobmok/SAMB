using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Discord.Commands;
using System.Reflection;
using Discord.Net;
using Newtonsoft.Json;

namespace samb;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private DiscordSocketClient _client;
    private CommandService _commands;
    private readonly IServiceProvider _services;

    public Worker(ILogger<Worker> logger, DiscordSocketClient client, CommandService commands, IServiceProvider services)
    {
        _logger = logger;
        _client = client;
        _commands = commands;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
        
        _client = new DiscordSocketClient();
        _client.Log += Log;

        var token = File.ReadAllText("token.txt");

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        _client.Ready += Client_Ready;
        _client.SlashCommandExecuted += SlashCommandHandler;;


        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }   
    }

    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    public async Task Client_Ready()
    {
        var lastPrice = new SlashCommandBuilder()
        .WithName("last-price")
        .WithDescription("Gets the last price of a coin")
        .AddOption("coin",  ApplicationCommandOptionType.String, "Name of coin you want to check" , true);

        try
        {
            await _client.CreateGlobalApplicationCommandAsync(lastPrice.Build());
        }
        catch(HttpException exception)
        {
            var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);

            Console.WriteLine(json);
        }
    }

    public async Task SlashCommandHandler(SocketSlashCommand command){
        if(command.Data.Name == "last-price"){
            var coin = command.Data.Options.First().Value.ToString();
            //var price = await GetPrice(coin);
            await command.RespondAsync($"The last price of {coin} is ");
        }
    }
}
