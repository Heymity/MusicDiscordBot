#define DEBUG
// The defaultdir macro is here in case you have different directories for the deevelopment and production build. In this case you just compile the code with the flag to change the directory.
#undef DEFAULTDIR

using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Commands;
using DiscordBot.Utilities.Managers.Storage;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DiscordBot
{
    public class Program
    {
#if DEFAULTDIR
        public const string DIRECTORY = "";
#else
		public const string DIRECTORY = "";
#endif
        private readonly string tokenDir = $"{DIRECTORY}/Token.txt";

        private DiscordSocketClient client;
        private CommandHandler commandHandler;
        private CommandService commandService;

        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            await Login();

            client.Ready += WhenReady;

            DataStorageManager.Current.LoadData();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private async Task Login()
        {
            client = new DiscordSocketClient(new DiscordSocketConfig { MessageCacheSize = 100 });
            commandService = new CommandService();
            commandHandler = new CommandHandler(client, commandService);

            await commandHandler.InstallCommandsAsync();

            await client.LoginAsync(TokenType.Bot, File.ReadAllText(tokenDir));
            await client.StartAsync();

#if DEBUG
            client.MessageUpdated += MessageUpdated;
            client.MessageReceived += MessageReceived;
#endif
        }

        private async Task WhenReady()
        {
            Console.WriteLine("Bot is connected!");
            
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