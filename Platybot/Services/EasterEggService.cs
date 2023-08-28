using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Platybot.Attributes;
using Platybot.Data;
using Platybot.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Platybot.Services.TimerService;

namespace Platybot.Services
{
    internal class EasterEggService
    {
        private readonly DataContext _dataContext;
        private readonly TimerService _timerService;

        private readonly Random Random;
        private readonly Dictionary<string[], MethodInfo> EasterEgg;
        private IMessage Message;

        public EasterEggService(IServiceProvider services)
        {
            _dataContext = services.GetRequiredService<DataContext>();
            _timerService = services.GetRequiredService<TimerService>();

            Random = new Random();
            EasterEgg = new Dictionary<string[], MethodInfo>();
            Message = null;

            List<MethodInfo> methods = this.GetType().GetMethods().Where(x => x.CustomAttributes.Any(y => y.AttributeType == typeof(EasterEggAttribute))).ToList();
            foreach (var method in methods)
            {
                var triggers = method.GetCustomAttribute<EasterEggAttribute>().Triggers;
                EasterEgg.Add(triggers, method);
            }
        }

        public void InvokeEasterEgg(IMessage message)
        {
            var egg = EasterEgg.Where(x => MessageContains(message, x.Key)).Select(x => x.Value).FirstOrDefault();
            if (egg != null)
            {
                Message = message;
                egg.Invoke(this, null);
                message = null;
            }
        }

        #region Easter Eggs

        [EasterEgg("amogus")]
        public async void AmogusEgg()
        {
            string amogus = Random.Next(10) == 9 ? ":knife:ඞ" : "ඞ";
            amogus = Message.Author.Id == 272139684824743936 ? ":knife: ඞ" : amogus; // Vox the Impostor
            await ReplyAsync(amogus);
        }

        [EasterEgg("alot")]
        public async void AlotEgg()
        {
            if (Message.Channel is SocketGuildChannel guildChannelTimer)
            {
                if (_timerService.UpdateTimer(TimerType.Alot, guildChannelTimer.Guild.Id))
                {
                    await ReplyAsync("<<alot1>><<alot2>>");
                }
            }
        }

        [EasterEgg("yansim, yan sim, yandere sim, yandere simulator, yandev, yan dev, yandere dev, alex mahan, evaxephon")]
        public async void CumEgg()
        {
            string cumChaliceUrl = "https://i.imgur.com/IAgLAEV.gif";
            await ReplyAsync(cumChaliceUrl);
        }

        [EasterEgg("👉👉")]
        public async void AyooEgg()
        {
            await ReplyAsync("👈👈");
        }

        #endregion

        #region Utilities 

        private static bool MessageContains(IMessage message, params string[] texts)
        {
            foreach (var text in texts)
            {
                if (Regex.IsMatch(message.Content, $"\\b{text}\\b", RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task ReplyAsync(string message)
        {
            await Message.Channel.SendMessageAsync(_dataContext.InsertRawEmotes(message));
        }

        #endregion
    }
}
