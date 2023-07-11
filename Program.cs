#define DEBUG
// The defaultdir macro is here in case you have different directories for the deevelopment and production build. In this case you just compile the code with the flag to change the directory.
#undef DEFAULTDIR

using System;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscordBot.Commands;
using DiscordBot.Utilities.Managers.Storage;
using Victoria;

namespace DiscordBot
{
    public class Program
    {
#if DEFAULTDIR
        public const string DIRECTORY = "";
#else
		public const string DIRECTORY = @"D:\Coding\C#\DiscordBot\MusicDiscordBot";
#endif
        private readonly string tokenDir = $"{DIRECTORY}/Token.txt";

        private DiscordSocketClient client;
        private CommandHandler commandHandler;
        private CommandService commandService;
        private LavaNode<XLavaPlayer> _lavaNode;

        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            await Login();

        }

        private async Task Login()
        {
            var config = new DiscordSocketConfig
            {
                MessageCacheSize = 100,
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            };

            client = new DiscordSocketClient(config);

            client.Log += LogMessage;

            await using var provider = new ServiceCollection()
                .AddSingleton(client)
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandler>()
                .AddSingleton<AudioService>()
                .Configure<CommandServiceConfig>(x =>
                {
                    x.CaseSensitiveCommands = false;
                    x.LogLevel = LogSeverity.Debug;
                })
                .AddSingleton<LavaConfig>()
                .AddSingleton<LavaNode<XLavaPlayer>>()
                .AddLogging(builder => builder.AddConsole())
                .BuildServiceProvider();

            _lavaNode = provider.GetRequiredService<LavaNode<XLavaPlayer>>();
            commandHandler = provider.GetRequiredService<CommandHandler>();
            commandService = provider.GetRequiredService<CommandService>();
            
            await commandHandler.InstallCommandsAsync();

            client.UserVoiceStateUpdated += async (user, before, after) =>
            {
                var currentUser = client.CurrentUser.Username;
                if (after.VoiceChannel is null && before.VoiceChannel.Users.Any(x => x.Username == currentUser))
                {
                    var hasOtherUsers = before.VoiceChannel.Users.Any(x => x.Username != currentUser);
                    if (!hasOtherUsers)
                    {
                        Console.WriteLine($"Leaving {before.VoiceChannel} as the last user, {user.Username}, has left.");
                        await before.VoiceChannel.DisconnectAsync();
                    }
                }
            };
            
            await client.LoginAsync(TokenType.Bot, File.ReadAllText(tokenDir));
            await client.StartAsync();

#if DEBUG
            client.MessageUpdated += MessageUpdated;
            client.MessageReceived += MessageReceived;
#endif
            AppDomain.CurrentDomain.ProcessExit += async (sender, eventArgs) =>
            {
                await _lavaNode.DisconnectAsync();
                await client.LogoutAsync();
            };
            
            client.Ready += WhenReady;

            DataStorageManager.Current.LoadData();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private Task LogMessage(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task WhenReady()
        {
            Console.WriteLine("Bot is connected!");
            
            if (!_lavaNode.IsConnected)
            {
                Console.WriteLine("Waiting for LavaLink connection");
                await _lavaNode.ConnectAsync();
            }

            Console.WriteLine("Ready");
            
            await client.SetGameAsync("help me please");
        }

#if DEBUG

        private async Task MessageReceived(SocketMessage msg)
        {
            await Task.Run(() =>
            {
                Console.WriteLine($"({msg.Channel}, {msg.Author}) -> {msg.Content}");
                return Task.CompletedTask;
            });
        }

        private async Task MessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
        {
            await Task.Run(async () =>
            {
                var message = await before.GetOrDownloadAsync();
                Console.WriteLine($"({message.Channel}, {message.Author}): {message} -> {after}");
            });
        }

#endif
    }
}