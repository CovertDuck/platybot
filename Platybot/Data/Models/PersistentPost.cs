using System.ComponentModel.DataAnnotations;

namespace Platybot.Data.Models
{
    internal class PersistentPost
    {
        [Required]
        public ulong GuildId { get; set; }

        [Required]
        public ulong ChannelId { get; set; }

        [Required]
        public ulong MessageId { get; set; }

        [Required]
        public string MessageContent { get; set; }

        public virtual GuildConfiguration GuildConfiguration { get; set; }
    }
}
