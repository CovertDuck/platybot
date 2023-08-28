using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Platybot.Enums;
using Platybot.Logger;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Platybot.Services
{
    internal class TimerService
    {
        private readonly DiscordSocketClient Client;

        private Dictionary<(TimerType, ulong, ulong?), DateTime> Timers { get; set; }
        private readonly TimeSpan DefaultTimerLength;
        private readonly Dictionary<TimerType, TimeSpan> TimerLengths;
        private readonly Dictionary<Message, DateTime> MessagesToDelete;

        public TimerService(IServiceProvider services)
        {
            Timers = new Dictionary<(TimerType, ulong, ulong?), DateTime>();

            TimerLengths = new Dictionary<TimerType, TimeSpan>()
            {
                { TimerType.Alot, new TimeSpan(0, 5, 0) }
            };

            DefaultTimerLength = new TimeSpan(0, 10, 0);

            MessagesToDelete = new Dictionary<Message, DateTime>();
            Client = services.GetRequiredService<DiscordSocketClient>();
        }

        public void Init()
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await DeletePosts();
                    await Task.Delay(10 * 1000);
                }
            });
        }

        #region MessagesToDelete

        public void AddMessageToDelete(IUserMessage message, int secondsUntilDelete)
        {
            var channel = message.Channel as SocketGuildChannel;
            AddMessageToDelete(channel.Guild.Id, channel.Id, message.Id, secondsUntilDelete);
        }

        public void AddMessageToDelete(ulong guildId, ulong channelId, ulong messageId, int secondsUntilDelete)
        {
            var post = new Message()
            {
                GuildId = guildId,
                ChannelId = channelId,
                MessageId = messageId
            };

            var timeToDelete = DateTime.Now + new TimeSpan(0, 0, secondsUntilDelete);

            MessagesToDelete.Add(post, timeToDelete);
        }

        private async Task DeletePosts()
        {
            if (MessagesToDelete.Count == 0)
                return;

            foreach (var postToDelete in MessagesToDelete)
            {
                var timeToDelete = postToDelete.Value;

                if (DateTime.Compare(DateTime.Now, timeToDelete) < 0)
                    return;

                var post = postToDelete.Key;

                try
                {
                    var message = await ((ISocketMessageChannel)Client.GetGuild(post.GuildId).GetChannel(post.ChannelId)).GetMessageAsync(post.MessageId);
                    await message.DeleteAsync();
                }
                catch (Exception ex)
                {
                    PlatybotLogger.Log(ex.ToString());
                }

                MessagesToDelete.Remove(postToDelete.Key);
            }
        }

        #endregion

        #region Timers

        public bool UpdateTimer(TimerType timerType, ulong serverId, ulong? userId = null, bool checkOnly = false)
        {
            var timerLength = TimerLengths.ContainsKey(timerType) ? TimerLengths[timerType] : DefaultTimerLength;

            if (Timers.ContainsKey((timerType, serverId, userId)))
            {
                if (Timers[(timerType, serverId, userId)].Add(timerLength) < DateTime.Now)
                {
                    if (!checkOnly)
                        Timers.Remove((timerType, serverId, userId));

                    return true;
                }
                else
                {
                    return false;
                }
            }

            if (!checkOnly)
                Timers.Add((timerType, serverId, userId), DateTime.Now);

            return true;
        }

        #endregion
    }
}
