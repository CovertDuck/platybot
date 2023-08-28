using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Platybot.Data.Models
{
    internal class GuildConfiguration
    {
        [Key]
        public ulong GuildId { get; set; }

        public string CommandPrefix { get; set; }

        public ulong? LogChannelId { get; set; }

        public ulong? AlertChannelId { get; set; }

        [Required]
        public bool WelcomeNewcomers { get; set; }

        public string WelcomeMessage { get; set; }

        [Required]
        public int TicketCount { get; set; }

        public ulong? TicketModeratorChannelId { get; set; }

        public ulong? TicketOpenCategoryId { get; set; }

        public ulong? TicketClosedCategoryId { get; set; }

        public bool IsSensitiveCommunity { get; set; }

        public bool AreCommandsEnabled { get; set; }

        public ulong? MemberCountChannelId { get; set; }

        public List<ModeratorRole> ModerationRoles { get; set; }

        public List<RoleAssignment> RoleAssignments { get; set; }

        public List<RestrictedCommand> RestrictedCommands { get; set; }
    }
}
