using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Platybot.Data.Models
{
    internal class RestrictedCommand
    {
        [Required]
        public ulong GuildId { get; set; }

        public ulong? ChannelId { get; set; }

        [Required]
        public string Command { get; set; }

        [Required]
        public bool Enabled { get; set; }

        public virtual GuildConfiguration GuildConfiguration { get; set; }
    }
}
