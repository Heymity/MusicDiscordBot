using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Victoria;
using Victoria.Enums;
using Victoria.Responses.Search;

namespace DiscordBot.Modules;

[Name("Audio Module")]
public class AudioModule : ModuleBase<SocketCommandContext> 
{
    private readonly LavaNode<XLavaPlayer> _lavaNode;
    private readonly AudioService _audioService;
    private static readonly IEnumerable<int> Range = Enumerable.Range(1900, 2000);

    public AudioModule(LavaNode<XLavaPlayer> lavaNode, AudioService audioService) {
        _lavaNode = lavaNode;
        _audioService = audioService;
    }

    [Command("Join")]
    public async Task JoinAsync([Remainder] string channelName = "") {
        if (_lavaNode.HasPlayer(Context.Guild)) {
            await ReplyAsync("I'm already connected to a voice channel!");
            return;
        }

        IVoiceChannel channel;

        if (string.IsNullOrWhiteSpace(channelName))
        {
            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null)
            {
                await ReplyAsync("You must be connected to a voice channel!");
                return;
            }

            channel = voiceState.VoiceChannel;
        }
        else
        {
            channel = Context.Guild.VoiceChannels.FirstOrDefault(v => v.Name == channelName);
            if (channel is null)
            {
                await ReplyAsync("Não achei esse canal ai não");
                return;
            }
        }

        try {
            await _lavaNode.JoinAsync(channel, Context.Channel as ITextChannel);
            await ReplyAsync($"Joined {channel.Name}!");
        }
        catch (Exception exception) {
            await ReplyAsync(exception.Message);
        }
    }

    [Command("Leave")]
    public async Task LeaveAsync() {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
            await ReplyAsync("I'm not connected to any voice channels!");
            return;
        }

        var voiceChannel = (Context.User as IVoiceState)?.VoiceChannel ?? player.VoiceChannel;
        if (voiceChannel == null) {
            await ReplyAsync("Not sure which voice channel to disconnect from.");
            return;
        }

        try {
            await _lavaNode.LeaveAsync(voiceChannel);
            await ReplyAsync($"I've left {voiceChannel.Name}!");
        }
        catch (Exception exception) {
            await ReplyAsync(exception.Message);
        }
    }

    [Command("Play")]
    [Alias("p")]
    [Summary("Toca uma música né, n sei oq vc esperava")]
    public async Task PlayAsync([Remainder] string searchQuery) {
        if (string.IsNullOrWhiteSpace(searchQuery)) {
            await ReplyAsync("O besta, vc tem q falar oq quer ouvir ne");
            return;
        }
        
        if (!_lavaNode.HasPlayer(Context.Guild)) await JoinAsync();

        var searchType = SearchType.YouTube;
        if (searchQuery.StartsWith("http")) searchType = SearchType.Direct; 
        
        var searchResponse = await _lavaNode.SearchAsync(searchType, searchQuery); 
        
        if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches) {
            await ReplyAsync($"I wasn't able to find anything for `{searchQuery}`.");
            return;
        }

        /*foreach (var track in searchResponse.Tracks)
        {
            Console.WriteLine(track.Title);
        }*/
        
        var player = _lavaNode.GetPlayer(Context.Guild);
        if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name)) {
            player.Queue.Enqueue(searchResponse.Tracks);
            
            if (searchType is SearchType.Direct)
                await ReplyAsync($"Enqueued {searchResponse.Tracks.Count} songs.");
            else
            {
                var track = searchResponse.Tracks.First();
                var artwork = await track.FetchArtworkAsync();

                var embed = new EmbedBuilder()
                    .WithAuthor(track.Author, Context.Client.CurrentUser.GetAvatarUrl(), track.Url)
                    .WithTitle(
                        $"Playlist {searchResponse.Playlist.Name} adicionada! ({searchResponse.Tracks.Count} musicas)")
                    .WithImageUrl(artwork)
                    .WithColor(GetColorFromSting(track.Title));

                await ReplyAsync(embed: embed.Build());
            }
        }
        else {
            var track = searchResponse.Tracks.FirstOrDefault();
            player.Queue.Enqueue(track);

            if (searchType is SearchType.Direct)
                await ReplyAsync($"Enqueued {track?.Title}");
            else
            {
                var artwork = await track.FetchArtworkAsync();

                var embed = new EmbedBuilder()
                    .WithAuthor(track?.Author, Context.Client.CurrentUser.GetAvatarUrl(), track?.Url)
                    .WithTitle($"{track?.Title} adicionada!")
                    .WithImageUrl(artwork)
                    .WithColor(GetColorFromSting(track?.Title));

                await ReplyAsync(embed: embed.Build());
            }
        }

        if (player.PlayerState is PlayerState.Playing or PlayerState.Paused) {
            return;
        }

        player.Queue.TryDequeue(out var lavaTrack);
        await player.PlayAsync(x => {
            x.Track = lavaTrack;
            x.ShouldPause = false;
        });
    }

    [Command("Pause")]
    public async Task PauseAsync() {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
            await ReplyAsync("I'm not connected to a voice channel.");
            return;
        }

        if (player.PlayerState != PlayerState.Playing) {
            await ReplyAsync("I cannot pause when I'm not playing anything!");
            return;
        }

        try {
            await player.PauseAsync();
            await ReplyAsync($"Paused: {player.Track.Title}");
        }
        catch (Exception exception) {
            await ReplyAsync(exception.Message);
        }
    }

    [Command("Resume")]
    public async Task ResumeAsync() {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
            await ReplyAsync("I'm not connected to a voice channel.");
            return;
        }

        if (player.PlayerState != PlayerState.Paused) {
            await ReplyAsync("I cannot resume when I'm not playing anything!");
            return;
        }

        try {
            await player.ResumeAsync();
            await ReplyAsync($"Resumed: {player.Track.Title}");
        }
        catch (Exception exception) {
            await ReplyAsync(exception.Message);
        }
    }

    [Command("Stop")]
    public async Task StopAsync() {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
            await ReplyAsync("I'm not connected to a voice channel.");
            return;
        }

        if (player.PlayerState == PlayerState.Stopped) {
            await ReplyAsync("Woaaah there, I can't stop the stopped forced.");
            return;
        }

        try {
            await player.StopAsync();
            await ReplyAsync("No longer playing anything.");
        }
        catch (Exception exception) {
            await ReplyAsync(exception.Message);
        }
    }

    [Command("Skip")]
    public async Task SkipAsync() {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
            await ReplyAsync("I'm not connected to a voice channel.");
            return;
        }

        if (player.PlayerState != PlayerState.Playing) {
            await ReplyAsync("Woaaah there, I can't skip when nothing is playing.");
            return;
        }

        if (player.Queue.Count == 0)
        {
            await StopAsync();
            return;
        }
        
        try {
            var (oldTrack, currenTrack) = await player.SkipAsync();
            await ReplyAsync($"Skipped: {oldTrack.Title}\nNow Playing: {player.Track.Title}");
        }
        catch (Exception exception) {
            await ReplyAsync(exception.Message);
        }
    }

    [Command("Seek")]
    public async Task SeekAsync(TimeSpan timeSpan) {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
            await ReplyAsync("I'm not connected to a voice channel.");
            return;
        }

        if (player.PlayerState != PlayerState.Playing) {
            await ReplyAsync("Woaaah there, I can't seek when nothing is playing.");
            return;
        }

        try {
            await player.SeekAsync(timeSpan);
            await ReplyAsync($"I've seeked `{player.Track.Title}` to {timeSpan}.");
        }
        catch (Exception exception) {
            await ReplyAsync(exception.Message);
        }
    }

    [Command("Volume")]
    public async Task VolumeAsync(ushort volume) {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
            await ReplyAsync("I'm not connected to a voice channel.");
            return;
        }

        try {
            await player.UpdateVolumeAsync(volume);
            await ReplyAsync($"I've changed the player volume to {volume}.");
        }
        catch (Exception exception) {
            await ReplyAsync(exception.Message);
        }
    }

    [Command("NowPlaying"), Alias("Np")]
    public async Task NowPlayingAsync() {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
            await ReplyAsync("I'm not connected to a voice channel.");
            return;
        }

        if (player.PlayerState != PlayerState.Playing) {
            await ReplyAsync("Woaaah there, I'm not playing any tracks.");
            return;
        }

        var track = player.Track;
        var artwork = await track.FetchArtworkAsync();

        var embed = new EmbedBuilder()
            .WithAuthor(track.Author, Context.Client.CurrentUser.GetAvatarUrl(), track.Url)
            .WithTitle($"Now Playing: {track.Title}")
            .WithImageUrl(artwork)
            .WithFooter($"{track.Position}/{track.Duration}");

        await ReplyAsync(embed: embed.Build());
    }

    [Command("Genius", RunMode = RunMode.Async)]
    public async Task ShowGeniusLyrics() {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
            await ReplyAsync("I'm not connected to a voice channel.");
            return;
        }

        if (player.PlayerState != PlayerState.Playing) {
            await ReplyAsync("Woaaah there, I'm not playing any tracks.");
            return;
        }

        var lyrics = await player.Track.FetchLyricsFromGeniusAsync();
        if (string.IsNullOrWhiteSpace(lyrics)) {
            await ReplyAsync($"No lyrics found for {player.Track.Title}");
            return;
        }

        await SendLyricsAsync(lyrics);
    }

    [Command("OVH", RunMode = RunMode.Async)]
    public async Task ShowOvhLyrics() {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
            await ReplyAsync("I'm not connected to a voice channel.");
            return;
        }

        if (player.PlayerState != PlayerState.Playing) {
            await ReplyAsync("Woaaah there, I'm not playing any tracks.");
            return;
        }

        var lyrics = await player.Track.FetchLyricsFromOvhAsync();
        if (string.IsNullOrWhiteSpace(lyrics)) {
            await ReplyAsync($"No lyrics found for {player.Track.Title}");
            return;
        }

        await SendLyricsAsync(lyrics);
    }

    [Command("Queue")]
    [Alias("q")]
    public Task QueueAsync() {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
            return ReplyAsync("I'm not connected to a voice channel.");
        }

        if (player.PlayerState != PlayerState.Playing)
            return ReplyAsync("I'm not playing any tracks.");
        
        var embed = new EmbedBuilder()
            .WithTitle("Fila")
            .WithColor(Color.Green)
            .WithDescription($"*Tocando Agora:* {player.Track.Title}{Environment.NewLine}" + string.Join(Environment.NewLine, player.Queue.Select((x, i) => $"{i}. {x.Title}")))
            .Build();

        return ReplyAsync(embed: embed);
    }

    private async Task SendLyricsAsync(string lyrics) {
        var splitLyrics = lyrics.Split(Environment.NewLine);
        var stringBuilder = new StringBuilder();
        foreach (var line in splitLyrics) {
            if (line.Contains('[')) {
                stringBuilder.Append(Environment.NewLine);
            }

            if (Range.Contains(stringBuilder.Length)) {
                await ReplyAsync($"```{stringBuilder}```");
                stringBuilder.Clear();
            }
            else {
                stringBuilder.AppendLine(line);
            }
        }

        await ReplyAsync($"```{stringBuilder}```");
    }
    
    private Color GetColorFromSting(string str)
    {
        int dividerIndex = (int)Math.Floor(str.Length / 3d);

        int r = Math.Abs(str.Substring(0, dividerIndex).GetHashCode() % 255);
        int g = Math.Abs(str.Substring(dividerIndex, 2 * dividerIndex).GetHashCode() % 255);
        int b = Math.Abs(str.Remove(0, 2 * dividerIndex).GetHashCode() % 255);

        return new Color(r, g, b);
    }
}