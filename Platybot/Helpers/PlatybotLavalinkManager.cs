using Discord.WebSocket;
using SharpLink;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Platybot.Helpers
{
    internal class PlatybotLavalinkManager : LavalinkManager
    {
        public DiscordSocketClient DiscordSocketClient { get; set; }

        private Dictionary<ulong, VoiceSession> VoiceSessions;

        public PlatybotLavalinkManager(DiscordSocketClient discordClient, LavalinkManagerConfig config = null) : base(discordClient, config)
        {
            SetupPlatybotManager();
        }

        public PlatybotLavalinkManager(DiscordShardedClient discordShardedClient, LavalinkManagerConfig config = null) : base(discordShardedClient, config)
        {
            SetupPlatybotManager();
        }

        private void SetupPlatybotManager()
        {
            VoiceSessions = new Dictionary<ulong, VoiceSession>();

            var inactivityCheck = new Thread(async delegate ()
            {
                while (true)
                {
                    Thread.Sleep(30 * 1000);
                    await DisconnectFromInactiveChannels();
                }
            });
            inactivityCheck.Start();
        }

        public async Task DisconnectFromInactiveChannels()
        {
            foreach (var voiceSession in VoiceSessions)
            {
                if (voiceSession.Value.LastPlayed != null)
                {
                    if (voiceSession.Value.LastPlayed.Value.AddMinutes(3) < DateTime.UtcNow)
                    {
                        var player = GetPlayer(voiceSession.Key);
                        await player.StopAsync();
                        await player.DisconnectAsync();

                        VoiceSessions.Remove(voiceSession.Key);
                    }
                }
            }
        }

        public int GetTrackCount(ulong guildId)
        {
            var session = GetOrCreateVoiceSession(guildId);
            return session.Tracks.Count;
        }

        public void AddTrack(ulong guildId, LavalinkTrack track, ulong requesterId)
        {
            var session = GetOrCreateVoiceSession(guildId);
            session.Tracks.Add(track);
            session.RequesterId = requesterId;
        }

        public LavalinkTrack PopNextTrack(ulong guildId)
        {
            var session = GetOrCreateVoiceSession(guildId);
            var track = session.Tracks.FirstOrDefault();

            if (track == null) return null;

            session.Tracks.RemoveAt(0);
            return track;
        }

        public bool LoopTrack(ulong guildId)
        {
            var session = GetOrCreateVoiceSession(guildId);
            session.IsLooping = !session.IsLooping;

            return session.IsLooping;
        }

        public bool IsTrackLooping(ulong guildId)
        {
            var session = GetOrCreateVoiceSession(guildId);
            return session.IsLooping;
        }

        public void FinishPlaying(ulong guildId)
        {
            var session = GetOrCreateVoiceSession(guildId);
            session.LastPlayed = DateTime.UtcNow;
        }

        public void ClearTracks(ulong guildId)
        {
            var session = GetOrCreateVoiceSession(guildId);
            session.Tracks = new List<LavalinkTrack>();
        }

        private VoiceSession GetOrCreateVoiceSession(ulong guildId)
        {
            if (!VoiceSessions.TryGetValue(guildId, out VoiceSession voiceSession))
            {
                voiceSession = new VoiceSession();
                VoiceSessions.Add(guildId, voiceSession);
            }

            return voiceSession;
        }
    }
}
