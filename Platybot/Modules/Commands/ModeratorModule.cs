using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Platybot.Attributes;
using Platybot.Constants;
using Platybot.Data;
using Platybot.Data.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Platybot.Modules.Commands
{
    [RequireModerator]
    [Discord.Commands.RequireContext(Discord.Commands.ContextType.Guild)]
    internal class ModeratorModule : CommandModule
    {
        private readonly InteractionService _interactionService;

        public ModeratorModule(IServiceProvider services)
        {
            DataContext = services.GetRequiredService<DataContext>();
            _interactionService = services.GetRequiredService<InteractionService>();
        }

        [Command("temp_embed")] // TODO: Finish
        public async Task EditEmbedAsync(string title, string description)
        {
            var embedBuilder = new EmbedBuilder
            {
                Title = title,
                Description = description,
                Color = new Color(Convert.ToUInt32("ff69b4", 16))
            };

            var embed = embedBuilder.Build();

            await ReplyAsync(embed: embed);
        }

        [Command("event")]
        public async Task EventAsync(string date)
        {
            string description = $"\"*Come rest how deities do.*\" Fetch yourself a drink, company, and perhaps a night's companion at Mikoshi's Rest!\r\n\r\n**Carrd**: https://mikoshisrest.carrd.co/#staff\r\n";
            var startTime = DateTime.Parse(date + " 21:00:00").AddHours(4);
            var endTime = DateTime.Parse(date + " 00:00:00").AddHours(4).AddDays(1);
            string assemblyDirectoryPath = Path.GetDirectoryName(AppContext.BaseDirectory);
            string mikoshisBackgroundPath = Path.Combine(assemblyDirectoryPath, PathConstants.RESOURCES_FOLDER, PathConstants.MIKOSHI_BACKGROUND);
            Image mikoshisBackgroundImage = new Image(mikoshisBackgroundPath);

            await Context.Guild.CreateEventAsync("Open Night", startTime, GuildScheduledEventType.External, GuildScheduledEventPrivacyLevel.Private, description, endTime, null, "[Mateus] Shirogane @ Ward 5, Plot 49", mikoshisBackgroundImage, null);
        }

        [Command("sensitive")]
        public async Task SensitiveAsync()
        {
            Configuration.IsSensitiveCommunity = !Configuration.IsSensitiveCommunity;
            DataContext.Update(Configuration);

            if (Configuration.IsSensitiveCommunity)
                await ReplyAsync("`Community set to \"Sensitive\"...`");
            else
                await ReplyAsync("`Community set to \"Not Sensitive\"...`");
        }

        [Command("enable_commands")]
        public async Task EnableCommandsAsync()
        {
            Configuration.AreCommandsEnabled = true;
            base.DataContext.Update(Configuration);

            await _interactionService.RegisterCommandsToGuildAsync(Configuration.GuildId, true);

            await ReplyAsync("`Commands Enabled!`");
        }

        [Command("disable_commands")]
        public async Task DisableCommandsAsync()
        {
            Configuration.AreCommandsEnabled = false;
            base.DataContext.Update(Configuration);

            await ReplyAsync("`Commands Disabled!`");
        }

        [Command("enable_tickets")]
        public async Task EnableTicketsAsync(ulong openCategoryId, ulong closeCategoryId, ulong moderatorChannelId, ulong notificationChannelId = 0)
        {
            Configuration.TicketOpenCategoryId = openCategoryId;
            Configuration.TicketClosedCategoryId = closeCategoryId;
            Configuration.TicketModeratorChannelId = moderatorChannelId;
            base.DataContext.Update(Configuration);

            var embed = new EmbedBuilder()
            {
                Title = "Create a Ticket",
                Description = "To submit a ticket, type /ticket in the chat bar, and then press Enter to bring up the submission box. Type a short description of your issue in the box and then submit.",
                Color = Color.Green
            };

            if (notificationChannelId != 0)
            {
                var notificationChannel = Context.Guild.Channels.Where(x => x.Id == notificationChannelId).FirstOrDefault() as ITextChannel;
                await notificationChannel.SendMessageAsync(embed: embed.Build());
            }

            await ReplyAsync("`Ticket System Enabled!`");
        }

        [Command("disable_tickets")]
        public async Task DisableTicketsAsync()
        {
            Configuration.TicketOpenCategoryId = null;
            Configuration.TicketClosedCategoryId = null;
            Configuration.TicketModeratorChannelId = null;

            base.DataContext.Update(Configuration);

            await ReplyAsync("`Ticket System Disabled!`");
        }

        [Command("prefix")]
        public async Task PrefixAsync(string prefix = null)
        {
            Configuration.CommandPrefix = prefix;

            await ReplyAsync("Done!");
        }

        [Command("moderator_check")]
        public async Task ModeratorCheckAsync()
        {
            await ReplyAsync(string.Format("<@{0}> is a moderator!", Context.User.Id));
        }

        [RequireGuildOwner]
        [Command("owner_check")]
        public async Task OwnerCheckAsync()
        {
            await ReplyAsync(string.Format("<@{0}> is the server owner!", Context.User.Id));
        }

        [Command("timeout")]
        public async Task TimeoutAsync(IGuildUser user, int timeInMinutes)
        {
            await user.SetTimeOutAsync(new TimeSpan(0, timeInMinutes, 0));

            var pluralChar = timeInMinutes == 1 ? string.Empty : "s";
            await ReplyAsync($"<@{user.Id}> has been resigned to the Shadow Realm for {timeInMinutes} minute{pluralChar}!");
        }

        [Command("forgive")]
        public async Task ForgiveAsync(IGuildUser user)
        {
            await user.RemoveTimeOutAsync();

            await ReplyAsync($"<@{user.Id}> has been returned from the Shadow Realm!");
        }

        [Command("role")]
        public async Task RoleAsync(ulong messageId, ulong roleId, string emoji)
        {
            var message = await Context.Channel.GetMessageAsync(messageId);
            if (message == null)
            {
                await ReplyAsync("Post is invalid!");
            }

            var roleAssignment = new RoleAssignment
            {
                GuildId = Context.Guild.Id,
                ChannelId = Context.Channel.Id,
                MessageId = messageId,
                RoleId = roleId,
                Emoji = emoji
            };

            if (Emote.TryParse(emoji, out var emote))
            {
                // Custom Emote
                await message.AddReactionAsync(emote);

            }
            else
            {
                // Stock Emoji
                await message.AddReactionAsync(new Emoji(emoji));
            }

            await DataContext.UpdateRoleAssignments(roleAssignment);
            await ReplyAsync("Done!");
        }

        [Command("enable_member_count")]
        public async Task EnableMemberCountAsync()
        {
            var memberCount = Context.Guild.MemberCount;
            var channel = await Context.Guild.CreateVoiceChannelAsync($"Total Members: {memberCount}");
            await channel.AddPermissionOverwriteAsync(Context.Guild.EveryoneRole, OverwritePermissions.DenyAll(channel));
            await channel.AddPermissionOverwriteAsync(Context.Guild.EveryoneRole, new OverwritePermissions(viewChannel: PermValue.Allow));

            Configuration.MemberCountChannelId = channel.Id;
            DataContext.Update(Configuration);

            await ReplyDoneAsync();
        }

        [Command("disable_member_count")]
        public async Task DisableMemberCountAsync()
        {
            var guildConfiguration = DataContext.GetGuildConfiguration(Context.Guild.Id);
            var memberCountChannelId = guildConfiguration.MemberCountChannelId;

            if (memberCountChannelId is null)
                return;

            await Context.Guild.GetChannel((ulong)memberCountChannelId).ModifyAsync(x => x.Name = "DELETE THIS");

            guildConfiguration.MemberCountChannelId = null;
            DataContext.Update(guildConfiguration);
        }

        [Command("purge")]
        public async Task PurgeAsync()
        {
            var firstMessage = Context.Message.ReferencedMessage;

            if (firstMessage == null)
            {
                return;
            }

            var messagesAsync = await Context.Channel.GetMessagesAsync(firstMessage, Direction.After).FlattenAsync();
            var messages = messagesAsync.ToList();
            messages.Add(firstMessage);

            await DoPurgeAsync(messages);
        }

        [Command("purge")]
        public async Task PurgeAsync(int amount)
        {
            amount = amount > 25 ? 25 : amount;
            var messagesAsync = await Context.Channel.GetMessagesAsync(amount + 1).FlattenAsync();
            var messages = messagesAsync.ToList();

            await DoPurgeAsync(messages);
        }

        [Command("purge")]
        public async Task PurgeAsync(ulong messageId, string stringUserIds = null)
        {
            var firstMessage = await Context.Channel.GetMessageAsync(messageId);
            var messagesAsync = await Context.Channel.GetMessagesAsync(firstMessage, Direction.After).FlattenAsync();
            var messages = messagesAsync.ToList();

            if (stringUserIds != null)
            {
                List<string> userIds = stringUserIds.Split(',').ToList();
                messages = messages.Where(x => userIds.Contains(x.Author.Id.ToString())).ToList();
            }

            messages.Add(firstMessage);

            await DoPurgeAsync(messages);
        }

        private async Task DoPurgeAsync(List<IMessage> messages)
        {
            messages.Reverse();
            var amount = messages.Count - 1;

            if (base.DataContext.GetGuildConfiguration(Context.Guild.Id).LogChannelId != null)
            {
                var purgeTime = Context.Message.Timestamp.ToLocalTime();
                var filename = $"purge_{Context.Channel.Name}_{purgeTime:yyyyMMdd_HHmmss}.txt";

                using Stream stream = new MemoryStream();
                var header = $"{amount} messages were deleted by {Context.User.Username}#{Context.User.Discriminator} ({Context.User.Id}) in channel #{Context.Channel.Name} at {purgeTime:yyyy-MM-dd HH:mm:ss.fff}.\n\n";
                stream.Write(GetTextBytes(header));

                foreach (var message in messages)
                {
                    var username = message.Author.Username;
                    var usernameId = message.Author.Id;
                    var timestamp = message.Timestamp.ToLocalTime();
                    var timestampString = $"{timestamp:yyyy-MM-dd HH:mm:ss.fff}";
                    var content = message.Content;

                    // Edited?
                    if (message.EditedTimestamp != null)
                    {
                        var editedTimeStamp = ((DateTimeOffset)message.EditedTimestamp).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
                        timestampString += $" (Edited : {editedTimeStamp:yyyy-MM-dd HH:mm:ss.fff})";
                    }

                    // Attachments?
                    foreach (var attachment in message.Attachments)
                    {
                        var newline = string.IsNullOrWhiteSpace(content) ? "" : "\n";
                        content += $"{newline}<FILE: {attachment.Filename}, {attachment.Size} bytes>";
                    }

                    var text =
                        $"\n{username} ({usernameId})\n" +
                        $"{timestampString}\n" +
                        $"{content}\n";

                    stream.Write(GetTextBytes(text));
                }

                stream.Seek(0, SeekOrigin.Begin);
                await ((IMessageChannel)Context.Guild.GetChannel(base.DataContext.GetGuildConfiguration(Context.Guild.Id).LogChannelId.Value)).SendFileAsync(stream, filename);
            }

            await ((ITextChannel)Context.Channel).DeleteMessagesAsync(messages);
            var confirmationMessage = await ReplyAsync($":recycle: Platybot deleted **{amount}** messages for you!");
            Thread.Sleep(5 * 1000);
            await Context.Channel.DeleteMessageAsync(confirmationMessage);
        }

        public async void UpdateAttendanceTracking(ulong postId, ulong listPostId)
        {
            ulong channelId = 934474430636253224;
            //ulong postId = 993200243741044818;
            var channel = Context.Guild.GetChannel(channelId) as IMessageChannel;
            var post = await channel.GetMessageAsync(postId);

            ulong listChannelId = 968291798105612349;
            //ulong listPostId = 993253280652198039;
            var listChannel = Context.Guild.GetChannel(listChannelId) as IMessageChannel;
            var listPost = await listChannel.GetMessageAsync(listPostId);

            var reactions = post.Reactions;

            var reactionList = string.Empty;

            var validEmojis = new Dictionary<string, string>()
            {
                {"vren_smug", "Courtesan Work"},
                {"autumn_kawaii", "Bartending / Hosting"},
                {"blob_knife_peek", "Security Work"},
                {"2b_dance", "Private Dancing"},
                {"🗞️", "Administrative Work"},
                {"⚕️", "On-Call Medical"},
            };

            foreach (var reaction in reactions)
            {
                if (validEmojis.ContainsKey(reaction.Key.Name))
                {
                    var selectedEmoji = validEmojis[reaction.Key.Name];

                    var selectedRole = string.Empty;
                    _ = validEmojis.TryGetValue(reaction.Key.Name, out selectedRole);
                    var numberOfSelected = reaction.Value.ReactionCount;

                    var users = await post.GetReactionUsersAsync(reaction.Key, 15).FlattenAsync();

                    reactionList += "**" + selectedRole + "**: " + (numberOfSelected - 1) + "\n";

                    foreach (var user in users)
                    {
                        if (user.Id != Id.BOT_ID)
                        {
                            var guildUser = user as IGuildUser;
                            reactionList += "- <@" + user.Id + ">\n";
                        }
                    }

                    reactionList += "\n";
                }
            }

            await listChannel.ModifyMessageAsync(listPostId, x => x.Content = reactionList);
        }
    }
}
