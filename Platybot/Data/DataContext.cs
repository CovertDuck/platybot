using Discord;
using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using Platybot.Constants;
using Platybot.Data.Models;
using Platybot.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Platybot.Data
{
    internal class DataContext : DbContext
    {
        #region Tables

        private Dictionary<string, Emote> EmoteDictionary;
        public List<string> DisabledCommands { get; private set; }

        public DbSet<RoleAssignment> RoleAssignments { get; set; }
        public DbSet<GuildConfiguration> GuildConfigurations { get; set; }
        public DbSet<ModeratorRole> ModeratorRoles { get; set; }
        public DbSet<RestrictedCommand> RestrictedCommands { get; set; }

        #endregion

        public DataContext() { }

        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
            //RefreshSettings();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var dataDirectory = PathHelper.DataDirectory;

                if (!Directory.Exists(dataDirectory))
                    Directory.CreateDirectory(dataDirectory);

                optionsBuilder.UseSqlite(Path.Join(dataDirectory, PathConstants.PLATYBOT_DB_FILE));
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // GuildConfiguration
            modelBuilder.Entity<GuildConfiguration>()
                .Property(x => x.WelcomeNewcomers)
                .HasDefaultValue(false);

            modelBuilder.Entity<GuildConfiguration>()
                .Property(x => x.TicketCount)
                .HasDefaultValue(0);

            modelBuilder.Entity<GuildConfiguration>()
                .Property(x => x.IsSensitiveCommunity)
                .HasDefaultValue(false);

            modelBuilder.Entity<GuildConfiguration>()
                .Property(x => x.AreCommandsEnabled)
                .HasDefaultValue(false);

            modelBuilder.Entity<GuildConfiguration>()
                .HasMany(x => x.ModerationRoles)
                .WithOne(x => x.GuildConfiguration).HasForeignKey(x => x.GuildId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GuildConfiguration>()
                .HasMany(x => x.RoleAssignments)
                .WithOne(x => x.GuildConfiguration).HasForeignKey(x => x.GuildId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GuildConfiguration>()
                .HasMany(x => x.RestrictedCommands)
                .WithOne(x => x.GuildConfiguration).HasForeignKey(x => x.GuildId)
                .OnDelete(DeleteBehavior.Cascade);

            //modelBuilder.Entity<GuildConfiguration>()
            //    .HasMany(x => x.PersistentPosts)
            //    .WithOne(x => x.GuildConfiguration).HasForeignKey(x => x.GuildId)
            //    .OnDelete(DeleteBehavior.Cascade);

            // RoleAsignment
            modelBuilder.Entity<RoleAssignment>()
                .HasKey(x => new { x.GuildId, x.ChannelId, x.MessageId, x.Emoji });

            // ModeratorRole
            modelBuilder.Entity<ModeratorRole>()
                .HasKey(x => new { x.GuildId, x.ModeratorRoleId });

            // RestrictedCommand
            modelBuilder.Entity<RestrictedCommand>()
                .HasKey(x => new { x.GuildId, x.ChannelId, x.Command });

            modelBuilder.Entity<RestrictedCommand>()
                .Property(x => x.Enabled)
                .HasDefaultValue(false);

            // PersistentPost
            modelBuilder.Entity<PersistentPost>()
                .HasKey(x => new { x.GuildId, x.ChannelId, x.MessageId });

            base.OnModelCreating(modelBuilder);
        }


        #region Emojis

        private static Dictionary<string, Emote> GenerateEmoteDictionary()
        {
            string assemblyDirectory = Path.GetDirectoryName(AppContext.BaseDirectory);
            var lines = File.ReadLines(Path.Join(assemblyDirectory, PathConstants.RESOURCES_FOLDER, PathConstants.LISTS_FOLDER, PathConstants.EMOTE_DICTIONARY_FILE_NAME));

            var dictionary = new Dictionary<string, Emote>();
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("#"))
                {
                    var splitLine = line.Split(';').ToList();
                    var friendlyName = splitLine[0];
                    var rawEmote = splitLine[1];

                    if (Emote.TryParse(rawEmote, out var emote))
                    {
                        dictionary.Add(friendlyName, emote);
                    }
                }
            }

            return dictionary;
        }

        public string InsertRawEmotes(string inputString)
        {
            if (inputString == null)
                return null;

            var pattern = new Regex(@"\<<.*?\>>", RegexOptions.Compiled);
            string output = pattern.Replace(inputString, new MatchEvaluator(InsertRawEmotesEvaluator));

            return output;
        }

        private string InsertRawEmotesEvaluator(Match text)
        {
            var friendlyName = text.Value.Replace("<<", "").Replace(">>", "");
            var rawEmote = text.Value;

            if (EmoteDictionary.ContainsKey(friendlyName))
            {
                rawEmote = EmoteDictionary[friendlyName].ToString();
            }

            return rawEmote;
        }

        #endregion

        #region GuildConfiguration

        public GuildConfiguration GetGuildConfiguration(ulong guildId)
        {
            var guildConfiguration = GuildConfigurations.Where(x => x.GuildId == guildId).FirstOrDefault();

            if (guildConfiguration == null)
            {
                guildConfiguration = BuildDefaultGuildConfiguration(guildId);
                GuildConfigurations.Update(guildConfiguration).State = EntityState.Added;
                SaveChanges();
            }

            return guildConfiguration;
        }

        private static GuildConfiguration BuildDefaultGuildConfiguration(ulong guildId)
        {
            var guildConfiguration = new GuildConfiguration()
            {
                GuildId = guildId
            };

            return guildConfiguration;
        }

        public List<ulong> SelectModeratorRoleIds(ulong guildId)
        {
            var guildConfiguration = GetGuildConfiguration(guildId);
            return guildConfiguration.ModerationRoles.Select(x => x.ModeratorRoleId).ToList();
        }

        public async void Update(GuildConfiguration guildConfigurations)
        {
            GuildConfigurations.Update(guildConfigurations);
            await SaveChangesAsync();
        }

        #endregion

        #region RoleAssignments

        public List<RoleAssignment> GetRoleAssignments(ulong guildId, ulong channelId, ulong MessageId)
        {
            return RoleAssignments.Where(x => x.GuildId == guildId && x.ChannelId == channelId && x.MessageId == MessageId).ToList();
        }

        public bool IsMessageRoleAssignment(ulong messageId)
        {
            return RoleAssignments.Where(x => x.MessageId == messageId).Any();
        }

        public async Task UpdateRoleAssignments(RoleAssignment roleAssignment)
        {
            RoleAssignments.Update(roleAssignment);
            await SaveChangesAsync();
        }

        #endregion

        #region ModeratorRole

        public async Task AddModeratorRole(ulong guildId, ulong moderatorRoleId)
        {
            // Making sure we have a GuildConfiguration row in the database
            var guildConfiguration = GetGuildConfiguration(guildId);

            var moderatorRole = ModeratorRoles.Where(x => x.GuildId == guildId && x.ModeratorRoleId == moderatorRoleId).FirstOrDefault();
            if (moderatorRole != null)
                return;

            moderatorRole = new ModeratorRole() { GuildId = guildId, ModeratorRoleId = moderatorRoleId };

            ModeratorRoles.Update(moderatorRole).State = EntityState.Added;
            await SaveChangesAsync();
        }

        public async Task RemoveModeratorRole(ulong guildId, ulong moderatorRoleId)
        {
            // Making sure we have a GuildConfiguration row in the database
            var guildConfiguration = GetGuildConfiguration(guildId);

            var moderatorRole = ModeratorRoles.Where(x => x.GuildId == guildId && x.ModeratorRoleId == moderatorRoleId).FirstOrDefault();

            ModeratorRoles.Update(moderatorRole).State = EntityState.Deleted;
            await SaveChangesAsync();
        }

        #endregion

        #region RestrictedCommands

        public bool IsCommandAllowed(ulong guildId, ulong? channelId, string command)
        {
            var restrictedCommandGuild = RestrictedCommands.Where(x => x.GuildId == guildId && x.Command == command.ToLower()).FirstOrDefault();
            if (restrictedCommandGuild != null)
                return restrictedCommandGuild.Enabled;

            var restrictedCommandChannel = RestrictedCommands.Where(x => x.GuildId == guildId && x.ChannelId == channelId && x.Command == command.ToLower()).FirstOrDefault();
            if (restrictedCommandChannel is null)
                return false;

            return restrictedCommandChannel.Enabled;
        }

        #endregion

        private string CamelToCaps(string camelCase)
        {
            string caps =
                System.Text.RegularExpressions.Regex.Replace(camelCase, "([A-Z])", " $1", System.Text.RegularExpressions.RegexOptions.Compiled)
                .Trim()
                .ToUpperInvariant()
                .Replace(' ', '_');

            return caps;
        }
    }
}
