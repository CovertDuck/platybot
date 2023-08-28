using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Platybot.Constants;
using Platybot.Data;
using Platybot.Enums;
using Platybot.Logger;
using Platybot.Services;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Platybot.Helpers
{
    internal class CommandHandler
    {
        readonly DiscordSocketClient _client;
        readonly CommandService _commands;
        readonly InteractionService _interactions;
        readonly SimpleCommandService _simpleCommands;
        readonly EasterEggService _easterEggs;
        readonly DataContext _dataContext;
        readonly IServiceProvider _services;

        public CommandHandler(IServiceProvider services)
        {
            _client = services.GetRequiredService<DiscordSocketClient>();
            _commands = services.GetRequiredService<CommandService>();
            _interactions = services.GetRequiredService<InteractionService>();
            _simpleCommands = services.GetRequiredService<SimpleCommandService>();
            _dataContext = services.GetRequiredService<DataContext>();
            _easterEggs = services.GetRequiredService<EasterEggService>();

            _services = services;

            _client.MessageReceived += MessageReceivedAsync;
            _client.ReactionAdded += ReactionAddedAsync;
            //_commands.CommandExecuted += CommandExecutedAsync;
            _client.InteractionCreated += InteractionCreatedAsync;
            _client.ButtonExecuted += ButtonExecutedAsync;
        }

        public void InitializeAsync()
        {
            _client.Ready += ReadyAsync;
            _interactions.SlashCommandExecuted += SlashCommandExecutedAsync;
        }

        private async Task ReadyAsync()
        {
            await RegisterCommandsAsync();
            // TODO: FIX DOUBLE REGISTRATION
            //await RegisterInteractionsAsync();
            UpdateAllMemberCounts();

            // Register command modules
            //await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(), services: _services);

            //_discord.Ready -= ReadyAsync;
        }

        public async Task MessageReceivedAsync(SocketMessage rawMessage)
        {
            // Check for channels with sticky posts (TODO: FINISH)
            if (rawMessage.Channel is SocketGuildChannel && (rawMessage.Channel.Id == 934197932553560175 || rawMessage.Channel.Id == 934482114081083442))
            {
                var channelMessagesAsync = rawMessage.Channel.GetMessagesAsync(10);
                var channelMessages = (await channelMessagesAsync.ToListAsync()).First();

                if (channelMessages.First().Author.Id == Id.BOT_ID)
                    return;

                foreach (var channelMessage in channelMessages)
                {
                    if (channelMessage.Author.Id == Id.BOT_ID)
                    {
                        if (channelMessage.Embeds.Count != 0 && channelMessage.Embeds.First().Description.StartsWith(MessageConstants.PLEASE))
                        {
                            await channelMessage.DeleteAsync();
                            break;
                        }
                    }
                }

                var embed = new EmbedBuilder()
                {
                    Description = MessageConstants.REQUEST_TO_LIMIT_SCREENSHOTS,
                    Color = Color.Orange
                }
                .Build();

                await rawMessage.Channel.SendMessageAsync(embed: embed);
            }

            // Direct messages
            if (rawMessage.Channel is IPrivateChannel && _client.CurrentUser.Id != rawMessage.Author.Id)
            {
                var dmChannel = _client.GetGuild(ConfigHelper.HOME_GUILD_ID).GetChannel(ConfigHelper.HOME_GUILD_CHANNEL_ID) as IMessageChannel;

                var username = rawMessage.Author.Username;
                var usernameDiscriminator = rawMessage.Author.Discriminator;
                var usernameId = rawMessage.Author.Id;
                var text = rawMessage.Content;

                var response = $":envelope_with_arrow:  `{username}#{usernameDiscriminator} ({usernameId})`\n{text}";

                await dmChannel.SendMessageAsync(response);
            }

            // Ignore system messages, or messages from other bots
            if (rawMessage is not SocketUserMessage message) return;
            if (message.Source != MessageSource.User) return;

            // Check for Easter Eggs
            _easterEggs.InvokeEasterEgg(message);

            // Command prefix
            var commandPrefix = ConfigHelper.DEFAULT_PREFIX;
            if (message.Channel is SocketGuildChannel guildChannel)
            {
                var customCommandPrefix = _dataContext.GetGuildConfiguration(guildChannel.Guild.Id)?.CommandPrefix;
                if (customCommandPrefix != null)
                {
                    commandPrefix = customCommandPrefix;
                }
            }

            // This value holds the offset where the prefix ends
            var argPos = commandPrefix.Length;
            if (!message.Content.StartsWith(commandPrefix)) return;

            // Check if an emote has been summoned
            if (_simpleCommands.HasSimpleCommand(message.Content[argPos..]))
            {
                string commandName = message.Content[argPos..];
                int commandNameEnd = commandName.IndexOf(' ');
                commandName = commandNameEnd == -1 ? commandName : commandName[..commandNameEnd];

                string selfId = message.Author.Id.ToString();
                string targetId = null;
                if (message.ReferencedMessage != null)
                {
                    targetId = message.ReferencedMessage.Author.Id.ToString();
                }
                else if (message.MentionedUsers.Count != 0)
                {
                    targetId = message.MentionedUsers.First().Id.ToString();
                }

                string rawSimpleCommand = _simpleCommands.GetSimpleCommand(commandName, selfId, targetId);

                await message.Channel.SendMessageAsync(_dataContext.InsertRawEmotes(rawSimpleCommand));
            }
            else
            {
                var context = new SocketCommandContext(_client, message);
                await _commands.ExecuteAsync(context, argPos, _services);
            }
        }

        public async Task ReactionAddedAsync(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> originChannel, SocketReaction reaction)
        {

            if (reaction.User.Value.IsBot) return;
            if (!_dataContext.IsMessageRoleAssignment(reaction.MessageId)) return;

            var message = await cachedMessage.GetOrDownloadAsync();
            var channel = originChannel.Value as SocketGuildChannel;
            var guild = channel.Guild;
            var user = guild.GetUser(reaction.UserId);

            if (guild.Id == 933995567485440110 && channel.Id == 934465684950372393)
            {
                if (reaction.Emote is Emoji)
                {
                    var botCheckReaction = reaction.Emote.ToString();
                    if (botCheckReaction == MessageConstants.SKULL_TEXT_EMOJI || botCheckReaction == MessageConstants.SKULL_EMOJI)
                    {
                        await user.BanAsync(reason: MessageConstants.POISON_ROLE);
                    }
                }
            }

            var emojiText = string.Empty;

            var test = string.Empty;
            if (reaction.Emote is Emoji)
            {
                emojiText = reaction.Emote.ToString();
            }
            else
            {
                var emote = reaction.Emote as Emote;
                emojiText = emote.ToString();
            }

            var roleAssignments = _dataContext.GetRoleAssignments(guild.Id, channel.Id, message.Id);

            if (!roleAssignments.Where(x => x.Emoji == emojiText).Any())
            {
                // No assignement linked to emote
                await message.RemoveReactionAsync(reaction.Emote, user);
                return;
            }

            var roleAssignment = roleAssignments.Where(x => x.Emoji == emojiText).FirstOrDefault();
            var role = guild.GetRole(roleAssignment.RoleId);

            if (!user.Roles.Contains(role))
            {
                await user.AddRoleAsync(role);
            }
            else
            {
                await user.RemoveRoleAsync(role);
            }

            await message.RemoveReactionAsync(reaction.Emote, user);
        }

        private async Task InteractionCreatedAsync(SocketInteraction arg)
        {
            try
            {
                var context = new SocketInteractionContext(_client, arg);
                var result = await _interactions.ExecuteCommandAsync(context, _services);
            }
            catch (Exception ex)
            {
                PlatybotLogger.Log(ex.ToString());
            }
        }

        // TODO: Finish
        private async Task ButtonExecutedAsync(SocketMessageComponent component)
        {
            var buttonName = component.Data.CustomId.Split('-')[0];
            var buttonId = component.Data.CustomId.Split('-')[1];

            EmbedBuilder embedBuilder = null;

            switch (buttonName)
            {
                case "fun_button":
                    embedBuilder = new EmbedBuilder
                    {
                        Title = $"{component.User.Username} clicked the **Fun Button**! ({buttonId})",
                        Color = Color.Purple
                    };
                    break;
                case "r34delete":
                    embedBuilder = new EmbedBuilder
                    {
                        Title = $"Post deleted",
                        Color = Color.Green
                    };

                    var messageToDelete = buttonId.Split(':').Select(x => ulong.Parse(x)).ToList();
                    var message = await ((ISocketMessageChannel)_client.GetGuild(messageToDelete[0]).GetChannel(messageToDelete[1])).GetMessageAsync(messageToDelete[2]);
                    var messageContent = message.Content;
                    await message.DeleteAsync();
                    PlatybotLogger.Log($"Post {buttonId} was deleted by {component.User.Username}! ({messageContent})", true, true, false, false, LogType.Rule34);
                    break;
                case "r34report":
                    embedBuilder = new EmbedBuilder
                    {
                        Title = $"Post deleted. Thank you {component.User.Username}! ♥",
                        Color = Color.Red
                    };

                    var messageToDeleteReport = buttonId.Split(':').Select(x => ulong.Parse(x)).ToList();
                    var messageReport = await ((ISocketMessageChannel)_client.GetGuild(messageToDeleteReport[0]).GetChannel(messageToDeleteReport[1])).GetMessageAsync(messageToDeleteReport[2]);
                    var messageContentReport = messageReport.Content;
                    await messageReport.DeleteAsync();
                    PlatybotLogger.Log($"Post {buttonId} was deleted and REPORTED by {component.User.Username}! ({messageContentReport})", true, true, false, false, LogType.Rule34);
                    break;
            }

            if (embedBuilder is not null)
            {
                await component.RespondAsync(embed: embedBuilder.Build(), ephemeral: true);
            }
        }

        private Task SlashCommandExecutedAsync(SlashCommandInfo arg1, IInteractionContext arg2, Discord.Interactions.IResult arg3)
        {
            return Task.CompletedTask;
        }

        private void UpdateAllMemberCounts()
        {
            var guildConfigurations = _dataContext.GuildConfigurations.Where(x => x.MemberCountChannelId != null).ToList();

            foreach (var guildConfiguration in guildConfigurations)
            {
                var guild = _client.GetGuild(guildConfiguration.GuildId);
                var memberCountChannelId = guild.GetChannel((ulong)guildConfiguration.MemberCountChannelId);

                if (memberCountChannelId is null)
                    return;

                memberCountChannelId.ModifyAsync(x => x.Name = $"Total Members: {guild.MemberCount}");
            }
        }

        private async Task RegisterCommandsAsync()
        {
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        private async Task RegisterInteractionsAsync()
        {
            _client.MessageReceived += MessageReceivedAsync;

            var guildIds = _dataContext.GuildConfigurations.Where(x => x.AreCommandsEnabled == true).Select(x => x.GuildId).ToList();


            foreach (var guildId in guildIds)
            {
                await _interactions.RegisterCommandsToGuildAsync(guildId, true);
            }
        }
    }
}
