using System.Collections.Generic;

namespace DiscordBot.Utilities.Managers.Data
{
    [System.Serializable]
    public class GuildDataManager
    {
        public ulong Id { get; set; }

        private char commandPrefix = '!';
        public char CommandPrefix { get => commandPrefix == '\0' ? '!' : commandPrefix; set => commandPrefix = value; }

        public Dictionary<ulong, ChannelDataManager> ChannelsData { get; private set; }

        public GuildDataManager(ulong id, Dictionary<ulong, ChannelDataManager> channelsData, char commandPrefix = '!')
        {
            Id = id;
            ChannelsData = channelsData ?? new Dictionary<ulong, ChannelDataManager>();
            CommandPrefix = commandPrefix;
        }

        public GuildDataManager(ulong id) : this(id, new Dictionary<ulong, ChannelDataManager>()) { }
    }
}