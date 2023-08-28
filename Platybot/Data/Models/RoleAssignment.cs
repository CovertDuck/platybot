using System.ComponentModel.DataAnnotations;

namespace Platybot.Data.Models
{
    internal class RoleAssignment
    {
        [Required]
        public ulong GuildId { get; set; }

        [Required]
        public ulong ChannelId { get; set; }

        [Required]
        public ulong MessageId { get; set; }

        public ulong RoleId { get; set; }

        [Required]
        public string Emoji { get; set; }

        public virtual GuildConfiguration GuildConfiguration { get; set; }
    }
}
