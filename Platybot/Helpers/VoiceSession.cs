using SharpLink;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Platybot.Helpers
{
    internal class VoiceSession
    {
        public VoiceSession()
        {
            Tracks = new List<LavalinkTrack>();
            IsLooping = false;
            RequesterId = null;
            LastPlayed = null;
        }

        public List<LavalinkTrack> Tracks { get; set; }
        public bool IsLooping { get; set; }
        public ulong? RequesterId { get; set; }
        public ulong GuildId { get; set; }
        public DateTime? LastPlayed { get; set; }
    }
}
