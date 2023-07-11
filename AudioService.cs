using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;

namespace DiscordBot;

public sealed class AudioService {
    private readonly LavaNode<XLavaPlayer> _lavaNode;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _disconnectTokens;

    public AudioService(LavaNode<XLavaPlayer> lavaNode, ILoggerFactory loggerFactory) {
        _lavaNode = lavaNode;
        _logger = loggerFactory.CreateLogger<LavaNode>();
        _disconnectTokens = new ConcurrentDictionary<ulong, CancellationTokenSource>();

        _lavaNode.OnLog += arg => {
            _logger.Log((LogLevel)(5 - (int)arg.Severity), arg.Exception, arg.Message);
            return Task.CompletedTask;
        };

        _lavaNode.OnPlayerUpdated += OnPlayerUpdated;
        _lavaNode.OnStatsReceived += OnStatsReceived;
        _lavaNode.OnTrackEnded += OnTrackEnded;
        _lavaNode.OnTrackStarted += OnTrackStarted;
        _lavaNode.OnTrackException += OnTrackException;
        _lavaNode.OnTrackStuck += OnTrackStuck;
        _lavaNode.OnWebSocketClosed += OnWebSocketClosed;
    }

    private async Task OnPlayerUpdated(PlayerUpdateEventArgs arg) {
        _logger.LogInformation("Track update received for {TrackTitle}: {ArgPosition}/{TrackDuration}", arg.Track.Title, arg.Position, arg.Player.Track.Duration);
        if (arg.Player.Track.Duration.Subtract(arg.Position.Value) <= TimeSpan.FromSeconds(6))
        {
            _logger.LogInformation("Last Update");
            
            var trackId = arg.Player.Track.Id;
            var trackPos = arg.Track.Position;
            await Task.Delay(7500);

            if (arg.Player.Track.Id != trackId || arg.Player.Track.Position != trackPos ||
                arg.Player.Queue.Count == 0 ||
                arg.Player.PlayerState is not PlayerState.Playing) return;
            
            _logger.LogInformation("Track didn\'t change, playstate is {PlayerPlayerState}, next in queue is {Title}", arg.Player.PlayerState, arg.Player.Queue.Peek().Title);
            await arg.Player.SkipAsync();
        }
    }

    private Task OnStatsReceived(StatsEventArgs arg) {
        _logger.LogInformation("Lavalink has been up for {ArgUptime}", arg.Uptime);
        return Task.CompletedTask;
    }

    private async Task OnTrackStarted(TrackStartEventArgs arg) {
        await arg.Player.TextChannel.SendMessageAsync($"Now playing: {arg.Track.Title}");
        if (!_disconnectTokens.TryGetValue(arg.Player.VoiceChannel.Id, out var value)) {
            return;
        }

        if (value.IsCancellationRequested) {
            return;
        }

        value.Cancel(true);
        await arg.Player.TextChannel.SendMessageAsync("Auto disconnect has been cancelled!");
    }

    private async Task OnTrackEnded(TrackEndedEventArgs args) {
        Console.WriteLine($"TrackEnded for reason: {args.Reason}");
        if (args.Reason != TrackEndReason.Finished) {
            return;
        }

        var player = args.Player;
        if (!player.Queue.TryDequeue(out var lavaTrack)) {
            await player.TextChannel.SendMessageAsync("Acabou as musicas :( vou quitar da call em 5min se n me querem mais");
            _ = InitiateDisconnectAsync(args.Player, TimeSpan.FromMinutes(5));
            return;
        }

        if (lavaTrack is null) {
            await player.TextChannel.SendMessageAsync("Next item in queue is not a track.");
            return;
        }

        await args.Player.PlayAsync(lavaTrack);
        await args.Player.TextChannel.SendMessageAsync(
            $"{args.Reason}: {args.Track.Title}\nNow playing: {lavaTrack.Title}");
    }

    private async Task InitiateDisconnectAsync(LavaPlayer player, TimeSpan timeSpan) {
        if (!_disconnectTokens.TryGetValue(player.VoiceChannel.Id, out var value)) {
            value = new CancellationTokenSource();
            _disconnectTokens.TryAdd(player.VoiceChannel.Id, value);
        }
        else if (value.IsCancellationRequested) {
            _disconnectTokens.TryUpdate(player.VoiceChannel.Id, new CancellationTokenSource(), value);
            value = _disconnectTokens[player.VoiceChannel.Id];
        }

        await player.TextChannel.SendMessageAsync($"Auto disconnect initiated! Disconnecting in {timeSpan}...");
        var isCancelled = SpinWait.SpinUntil(() => value.IsCancellationRequested, timeSpan);
        if (isCancelled) {
            return;
        }

        await _lavaNode.LeaveAsync(player.VoiceChannel);
        await player.TextChannel.SendMessageAsync("Invite me again sometime, sugar.");
    }

    private async Task OnTrackException(TrackExceptionEventArgs arg) {
        _logger.LogError("Track {TrackTitle} threw an exception. Please check Lavalink console/logs", arg.Track.Title);
        arg.Player.Queue.Enqueue(arg.Track);
        await arg.Player.TextChannel.SendMessageAsync(
            $"{arg.Track.Title} has been re-added to queue after throwing an exception.");
    }

    private async Task OnTrackStuck(TrackStuckEventArgs arg) {
        _logger.LogError("Track {TrackTitle} got stuck for {ArgThreshold}ms. Please check Lavalink console/logs", arg.Track.Title, arg.Threshold);
        arg.Player.Queue.Enqueue(arg.Track);
        await arg.Player.TextChannel.SendMessageAsync(
            $"{arg.Track.Title} has been re-added to queue after getting stuck.");
    }

    private Task OnWebSocketClosed(WebSocketClosedEventArgs arg) {
        _logger.LogCritical("Discord WebSocket connection closed with following reason: {ArgReason}", arg.Reason);
        return Task.CompletedTask;
    }
}