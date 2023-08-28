using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Platybot.Constants;
using Platybot.Data;
using Platybot.Data.Models;
using Platybot.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Platybot.Modules
{
    internal class CommandModule : ModuleBase<SocketCommandContext>
    {
        internal static readonly Random random = new();
        internal static readonly object _intensiveMethodLock = new();

        public DiscordSocketClient DiscordSocketClient { get; set; }
        public HttpClient HttpClient { get; set; }
        public ImageService ImageService { get; set; }
        public SimpleCommandService EmoteService { get; set; }
        public CommandService CommandService { get; set; }
        public DataContext DataContext { get; set; }
        public GuildConfiguration Configuration
        {
            get
            {
                return DataContext.GetGuildConfiguration(Context.Guild.Id);
            }
        }

        #region Reply
        
        protected override Task<IUserMessage> ReplyAsync(string message = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null)
        {
            var injectedMessage = DataContext.InsertRawEmotes(message);
            return base.ReplyAsync(injectedMessage, isTTS, embed, options, allowedMentions, messageReference, components, stickers, embeds);
        }

        internal async Task ReplyDoneAsync()
        {
            var message = await ReplyAsync(ModuleConstants.REPLY_DONE_MESSAGE);

            _ = Task.Run(async () =>
            {
                await Task.Delay(1000 * 3);
                await message.DeleteAsync();

                return Task.CompletedTask;
            });
        }

        internal void ReplyDoneAsync(IUserMessage message)
        {
            message.ModifyAsync(x => x.Content = ModuleConstants.REPLY_DONE_MESSAGE, null);
        }

        internal Task<IUserMessage> ReplyLoadingAsync(string loadingMessage = "Loading")
        {
            return ReplyAsync($"<<loading>> {loadingMessage}...");
        }

        #endregion

        #region Helpers

        internal static Image GetImage(string imageName)
        {
            string assemblyDirectoryPath = Path.GetDirectoryName(AppContext.BaseDirectory);
            string imagePath = Path.Combine(assemblyDirectoryPath, PathConstants.RESOURCES_FOLDER, PathConstants.IMAGES_FOLDER, imageName);
            Image image = new(imagePath);
            return image;
        }

        internal static string GetRandomLineFromList(string fileName)
        {
            string assemblyDirectory = Path.GetDirectoryName(AppContext.BaseDirectory);
            var lines = File.ReadLines(Path.Join(assemblyDirectory, PathConstants.RESOURCES_FOLDER, PathConstants.LISTS_FOLDER, fileName)).ToList().Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var randomLine = lines[random.Next(lines.Count)];

            return randomLine;
        }

        internal async Task PostImage(byte[] image, string filename)
        {
            var stream = new MemoryStream(image);
            await Context.Channel.SendFileAsync(stream, filename);
        }

        internal async Task<IEnumerable<IMessage>> GetLastMessageGroup(IUser user = null)
        {
            var messages = await Context.Channel.GetMessagesAsync(100).FlattenAsync();
            var messageGroup = new List<IMessage>();
            var inGroup = false;

            // Skip the command
            user ??= messages.Skip(1).First().Author;

            foreach (var message in messages)
            {
                if (message.Author.Id == user.Id && message.Id != Context.Message.Id)
                {
                    messageGroup.Add(message);
                    inGroup = true;
                }
                else if (inGroup == true)
                {
                    break;
                }
            }

            messageGroup.Reverse();
            return messageGroup;
        }

        internal static int GetDailyStableHashCode(string str)
        {
            var today = DateTime.Today;
            int hashCode =
                GetStableHashCode(str) +
                GetStableHashCode(today.ToString("yyyyMMdd"));

            return hashCode;
        }

        internal static int GetStableHashCode(string str)
        {
            unchecked
            {
                int hash1 = 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }

        internal static byte[] GetTextBytes(string text)
        {
            return new UTF8Encoding(true).GetBytes(text);
        }

        #endregion
    }
}
