using Discord;
using Discord.WebSocket;
using Discord.Net;
using Newtonsoft.Json;

namespace samb;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private DiscordSocketClient _client;

    public Worker(ILogger<Worker> logger, DiscordSocketClient client)
    {
        _logger = logger;
        _client = client;
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
        _client.SlashCommandExecuted += SlashCommandHandler;


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
        .WithName("subcribe")
        .WithDescription("Subcribes to a coin")
        .AddOption("coin",  ApplicationCommandOptionType.String, "Name of coin you want to subscribe to" , true);

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
        if(command.Data.Name == "subcribe"){
            var coin = command.Data.Options.First().Value.ToString();

            ulong channelId = (ulong)command.ChannelId!;
            var guild = _client.GetGuild(command.GuildId!.Value);
            var channel = guild.GetTextChannel(channelId);
            //var price = await GetPrice(coin);
            await command.RespondAsync($"You have subscribed to {coin}");

            var thread = await channel.CreateThreadAsync("test", ThreadType.PrivateThread);
            var user = guild.GetUser(command.User.Id);
            await thread.AddUserAsync(user);

            await thread.SendMessageAsync("Can you see this?");
        }
    }
}
