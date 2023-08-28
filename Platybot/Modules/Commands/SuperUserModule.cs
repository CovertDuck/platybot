using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Platybot.Attributes;
using Platybot.Services;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Platybot.Modules.Commands
{
    [RequireSuperUser]
    [Discord.Commands.RequireContext(Discord.Commands.ContextType.Guild)]
    internal class SuperUserModule : CommandModule
    {
        readonly InteractionService _interactions;
        readonly TimerService _timerService;

        public SuperUserModule(IServiceProvider services)
        {
            _interactions = services.GetRequiredService<InteractionService>();
            _timerService = services.GetRequiredService<TimerService>();
        }

        [Command("check_db")]
        public async Task CheckDbAsync()
        {
            var embed = new EmbedBuilder
            {
                Title = "Guild Configuration",
                Description = "For special eyes only...",
                Color = Color.Blue,
            };

            PropertyInfo[] properties = Configuration.GetType().GetProperties();
            foreach (PropertyInfo property in properties)
            {
                embed.AddField(property.Name, property.GetValue(Configuration, null) ?? "*NULL*");
            }

            await ReplyAsync(embed: embed.Build());
        }

        [Command("refresh_context")]
        public async Task RefreshContextAsync()
        {
            DataContext.RefreshSettings();
            await ReplyDoneAsync();
        }

        [Command("list_servers")]
        public async Task ListServersAsync()
        {
            var embedBuilder = new EmbedBuilder
            {
                Title = "Platybot"
            };

            var embedContent = string.Empty;
            var guilds = DiscordSocketClient.Guilds;
            foreach (var guild in guilds)
            {
                embedContent += $"• {guild.Name} {guild.Id})";
                if (!guilds.Equals(guild))
                {
                    embedContent += "\n";
                }
            }

            embedBuilder.AddField("Servers", embedContent);
            await ReplyAsync(embed: embedBuilder.Build());
        }

        [Command("echo")]
        public async Task EchoAsync([Remainder] string text)
        {
            await ReplyAsync(text);
            await Context.Message.DeleteAsync();
        }

        [Command("register_commands")]
        public async Task RegisterCommandsAsync()
        {
            await _interactions.RegisterCommandsToGuildAsync(Context.Guild.Id);
            await ReplyDoneAsync();
        }

        [Command("super_forgive")]
        public async Task SuperUntimeoutAsync(ulong serverId)
        {
            var guild = DiscordSocketClient.Guilds.Where(x => x.Id == serverId).First();
            var user = guild.GetUser(Context.User.Id);
            await user.RemoveTimeOutAsync();
        }

        [Command("add_moderator")]
        public async Task AddModeratorAsync(SocketRole role)
        {
            var roleExists = Context.Guild.Roles.Where(x => x.Id == role.Id).Any();

            if (!roleExists)
            {
                await ReplyAsync("This role doesn't exist... Try again!");
                return;
            }

            await DataContext.AddModeratorRole(Context.Guild.Id, role.Id);
            await ReplyDoneAsync();
        }

        [Command("remove_moderator")]
        public async Task RemoveModeratorAsync(SocketRole role)
        {
            var roleExists = Context.Guild.Roles.Where(x => x.Id == role.Id).Any();

            if (!roleExists)
            {
                await ReplyAsync("This role doesn't exist... Try again!");
                return;
            }

            await DataContext.RemoveModeratorRole(Context.Guild.Id, role.Id);
        }

        [Command("delete")]
        public async Task DeleteAsync(string url)
        {
            var splitUrl = url.Split('/');
            if (splitUrl.Length != 7) throw new FormatException("Invalid URL.");

            ulong serverId;
            ulong channelId;
            ulong messageId;

            try
            {
                serverId = ulong.Parse(splitUrl[4]);
                channelId = ulong.Parse(splitUrl[5]);
                messageId = ulong.Parse(splitUrl[6]);
            }
            catch (Exception e)
            {
                throw new FormatException("Invalid URL.", e);
            }

            var channel = DiscordSocketClient.GetGuild(serverId).GetChannel(channelId) as SocketTextChannel;
            await channel.DeleteMessageAsync(messageId);
        }

        [Command("message")]
        public async Task MessageAsync(ulong userId, [Remainder] string message)
        {
            var user = DiscordSocketClient.GetUser(userId);
            await user.SendMessageAsync(message);
        }

        [Command("message")]
        public async Task MessageAsync(string usernameWithDiscriminator, [Remainder] string message)
        {
            var username = usernameWithDiscriminator.Split('#')[0];
            var discriminator = usernameWithDiscriminator.Split('#')[1];

            var user = DiscordSocketClient.GetUser(username, discriminator);
            await user.SendMessageAsync(message);
        }

        [Command("reaction")]
        public async Task ReactionAsync(ulong messageId, string emoji)
        {
            var message = await Context.Channel.GetMessageAsync(messageId);

            if (Emote.TryParse(emoji, out var emote))
            {
                await message.AddReactionAsync(emote);
            }
            else
            {
                await message.AddReactionAsync(new Emoji(emoji));
            }
        }

        [Command("embed")]
        public async Task EmbedAsync(string title, string message, string color, string imageUrl = null, string linkUrl = null, string thumnailUrl = null)
        {
            var embedBuilder = new EmbedBuilder
            {
                Title = title,
                Description = message,
                Color = new Color(Convert.ToUInt32(color, 16)),
                ImageUrl = imageUrl,
                Url = linkUrl,
                ThumbnailUrl = thumnailUrl,
            };

            await ReplyAsync(embed: embedBuilder.Build());
        }

        [Command("playing")]
        public async Task PlayingAsync()
        {
            await PlayingAsync(string.Empty);
        }

        [Command("playing")]
        public async Task PlayingAsync([Remainder] string gameName)
        {
            var bot = Context.Client;

            await bot.SetGameAsync(gameName);
        }

        [Command("rename", RunMode = Discord.Commands.RunMode.Async)]
        public async Task RenameAsync(IGuildUser user, [Remainder] string nickname)
        {
            await user.ModifyAsync(x => { x.Nickname = nickname.Trim(); });
            await Context.Message.DeleteAsync();
        }
    }
}
