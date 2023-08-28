using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Platybot.Data;
using Platybot.Enums;
using Platybot.Logger;
using Platybot.Resources;
using Platybot.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using static Platybot.Services.ImageService;

namespace Platybot.Modules
{
    internal class InteractionModule : InteractionModuleBase<SocketInteractionContext>
    {
        private static readonly Random random = new();

        public DataContext DataContext;
        public ImageService ImageService;

        [SlashCommand("wednesday", "Conjure a Wednesday frog!", runMode: Discord.Interactions.RunMode.Async)]
        public async Task WednesdayAsync()
        {
            await RespondAsync($"You call out for a Wednesday frog!", ephemeral: true);

            if (DateTime.Now.DayOfWeek == DayOfWeek.Wednesday)
            {
                var (data, extension) = await ImageService.GetImage("frog", ImageProvider.Unsplash);
                await ReplyAsync($"{Context.User.Mention} conjures a Wednesday frog!\n`It is Wednesday, my Dudes!`");
                await PostImage(data, "wednesday" + extension);
            }
            else
            {
                await ReplyAsync($"{Context.User.Mention} conjures a Wednesday frog!\n``It is not Wednesday, my Dudes...`");
            }
        }

        [SlashCommand("hold", "Hold the mascot!")]
        public async Task HoldAsync()
        {
            var duckbillImageUrl = ImageLinks.Hold_Duckbill;

            await ReplyAsync($"{Context.User.Mention} holds the Duckbill like hamburger.");
            await ReplyAsync(duckbillImageUrl);

            await RespondAsync($"The Duckbill enjoys being held!", ephemeral: true);
        }

        #region Ticket System

        [SlashCommand("ticket", "Create a ticket.")]
        public async Task CreateTicketAsync()
        {
            try
            {
                await Context.Interaction.RespondWithModalAsync<TicketModal>("ticket_creation");
            }
            catch (Exception ex)
            {
                PlatybotLogger.Log(ex.ToString());
            }
        }

        [ModalInteraction("ticket_creation")]
        public async Task TicketModalResponse(TicketModal modal)
        {
            // Create the ticket channel
            var ticketCount = DataContext.GetGuildConfiguration(Context.Guild.Id).TicketCount;
            ticketCount++;
            var ticketChannelName = $"ticket_{ticketCount:000}";

            var guildConfiguration = DataContext.GetGuildConfiguration(Context.Guild.Id);
            var moderatorChannelId = guildConfiguration.TicketModeratorChannelId;
            var openCategoryId = guildConfiguration.TicketOpenCategoryId;

            // Fetch IDs
            var ticketChannel = await Context.Guild.CreateTextChannelAsync(ticketChannelName, x => x.CategoryId = openCategoryId);
            var modChannel = Context.Guild.GetChannel((ulong)moderatorChannelId) as ITextChannel;

            // Post ticket description to ticket channel
            var ticketEmbedBuilder = new EmbedBuilder
            {
                Title = $"Ticket Information",
                Color = Color.LightOrange
            }.WithCurrentTimestamp();

            ticketEmbedBuilder
                .AddField("Issuer", Context.User.Mention);

            ticketEmbedBuilder
                .AddField("Description", modal.TicketDescription);

            await ticketChannel.SendMessageAsync(embed: ticketEmbedBuilder.Build());

            // Add issuer to ticket channel
            var permissions = new OverwritePermissions(
                viewChannel: PermValue.Allow,
                sendMessages: PermValue.Allow,
                embedLinks: PermValue.Allow,
                attachFiles: PermValue.Allow,
                addReactions: PermValue.Allow,
                useExternalEmojis: PermValue.Allow,
                useExternalStickers: PermValue.Allow,
                readMessageHistory: PermValue.Allow);

            await ticketChannel.AddPermissionOverwriteAsync(Context.User, permissions);

            // Ping the moderators in the private ticket channel
            var embedBuilder = new EmbedBuilder
            {
                Title = $"New Ticket",
                Description = $"<@&934196611054194710>\nA new ticket has been created!",
                Color = Color.Green
            }.WithCurrentTimestamp();

            embedBuilder
                .AddField("Channel", ticketChannel.Mention);

            embedBuilder
                .AddField("Issuer", Context.User.Mention);

            embedBuilder
                .AddField("Description", modal.TicketDescription);

            await modChannel.SendMessageAsync(embed: embedBuilder.Build());

            // Update ticket count
            var props = DataContext.GetGuildConfiguration(Context.Guild.Id);
            props.TicketCount = ticketCount;
            DataContext.Update(props);

            // Respond to the modal.
            try
            {
                await RespondAsync($"Channel {ticketChannel.Mention} created. Please head to this channel.", ephemeral: true);
            }
            catch (Exception ex)
            {
                PlatybotLogger.Log(ex.ToString());
            }
        }

        [SlashCommand("close", "Close this ticket.")]
        public async Task CloseTicketAsync()
        {
            // Check if channel is a ticket
            if (!Context.Channel.Name.StartsWith("ticket_"))
            {
                await RespondAsync($"This channel is not a Ticket Channel. You cannot close it!", ephemeral: true);
                return;
            }

            var guildConfiguration = DataContext.GetGuildConfiguration(Context.Guild.Id);
            var closedCategoryId = guildConfiguration.TicketClosedCategoryId;
            var moderatorChannelId = guildConfiguration.TicketModeratorChannelId;

            // Remove issuer from channel
            var ticketChannel = Context.Channel as IGuildChannel;
            var users = ticketChannel.GetUsersAsync();
            var nonModUsers = new List<IGuildUser>();

            var moderationRoleIds = guildConfiguration.ModerationRoles?.Select(x => x.ModeratorRoleId).ToList() ?? new List<ulong>();

            foreach (var user in await users.FlattenAsync())
            {
                if (!(user.RoleIds.Where(x => moderationRoleIds.Contains(x)).Any()))
                {
                    nonModUsers.Add(user);
                }
            }

            foreach (var nonModUser in nonModUsers)
            {
                await ticketChannel.RemovePermissionOverwriteAsync(nonModUser);
            }

            // Move channel to ticket graveyard
            await ticketChannel.ModifyAsync(x =>
            {
                x.CategoryId = closedCategoryId;
            });

            // Advertise that ticket is closed
            var embedBuilder = new EmbedBuilder
            {
                Title = $"Ticket Closed",
                Description = $"{string.Join(" ", moderationRoleIds.Select(x => "<@&" + x + ">"))}\nTicket <#{ticketChannel.Id}> has been closed by {Context.User.Mention}!",
                Color = Color.Purple
            }.WithCurrentTimestamp();

            var modChannel = Context.Guild.GetChannel((ulong)moderatorChannelId) as ITextChannel;
            await modChannel.SendMessageAsync(embed: embedBuilder.Build());

            await RespondAsync($"Ticket has been archived!", ephemeral: true);
        }

        #endregion

        #region Helpers

        private async Task PostImage(byte[] image, string filename)
        {
            var stream = new MemoryStream(image);
            await Context.Channel.SendFileAsync(stream, filename);
        }

        #endregion
    }
}
