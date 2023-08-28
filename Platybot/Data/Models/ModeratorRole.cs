using System.ComponentModel.DataAnnotations;

namespace Platybot.Data.Models
{
    internal class ModeratorRole
    {
        [Required]
        public ulong GuildId { get; set; }

        [Required]
        public ulong ModeratorRoleId { get; set; }

        public virtual GuildConfiguration GuildConfiguration { get; set; }
    }
}
