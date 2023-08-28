using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Platybot.Attributes;
using Platybot.Data;
using Platybot.Enums;
using Platybot.Helpers;
using Platybot.Logger;
using Platybot.Resources;
using Platybot.Services;
using SharpLink;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Platybot.Services.ImageService;

namespace Platybot.Modules.Commands
{
    [RequireContext(ContextType.Guild)]
    internal class PublicModule : CommandModule
    {
        private readonly int ROULETTE_TIMEOUT_IN_MINUTES = 5;

        private readonly PlatybotLavalinkManager LavalinkManager;
        private readonly FF14Service FF14Service;

        public PublicModule(IServiceProvider services)
        {
            LavalinkManager = services.GetRequiredService<PlatybotLavalinkManager>();
            FF14Service = services.GetRequiredService<FF14Service>();
        }

        #region FFXIV

        [Command("item")]
        public async Task ItemAsync([Remainder] string search)
        {
            var names = await FF14Service.GetItemNamesAsync(search);

            await ReplyAsync(string.Join("\n", names));
        }

        [Command("lore")]
        public async Task LoreAsync([Remainder] string search)
        {
            var lores = await FF14Service.GetLoreAsync(search);
            var lore = lores.First();

            var embedBuilder = new EmbedBuilder
            {
                Title = $"**{lore.Source}** [1/{lores.Count}]",
                Description = lore.Text,
                Color = Color.DarkPurple
            };

            var build = new ComponentBuilder().WithButton("Next", "next");

            await ReplyAsync(embed: embedBuilder.Build(), components: build.Build());
        }

        #endregion

        #region Images

        // [Command("ai", RunMode = RunMode.Async)]
        // [RequireRestrictedCommand]
        // public async Task AiAsync([Remainder] string prompt)
        // {
        //     var loadingMessage = await ReplyLoadingAsync("Generating AI art");

        //     var image = await ImageService.GetAiImage(prompt);
        //     await loadingMessage.DeleteAsync();

        //     if (image is null)
        //     {
        //         await ReplyAsync("The generation failed...");
        //         return;
        //     }

        //     await Context.Channel.SendFileAsync(image, $"{prompt.Replace(" ", "_").ToLowerInvariant()}.png");
        // }

        [Command("wednesday")]
        [Summary("Frogs!")]
        public async Task WednesdayAsync()
        {
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

        [Command("image", RunMode = RunMode.Async)]
        [Summary("KEYWORD1;KEYWORD2;...;Find a random picture.")]
        public async Task RandomPictureAsync([Remainder] string keywords)
        {
            keywords = string.Join("+", keywords.ToLower().Split(' ').Where(x => !string.IsNullOrWhiteSpace(x)).ToList());
            var (data, extension) = await ImageService.GetImage(keywords, ImageProvider.Unsplash);
            await PostImage(data, keywords + extension);
        }

        [Command("gone", RunMode = RunMode.Async)]
        [Summary("TOP TEXT;Post a crab rave GIF.")]
        public async Task CrabRaveAsync([Remainder] string text = null)
        {
            text ??= Context.User.Username;

            using (Context.Channel.EnterTypingState())
            {
                Stream stream;
                lock (_intensiveMethodLock)
                {
                    stream = GetCrabRave(text, "IS GONE!");
                }

                stream.Seek(0, SeekOrigin.Begin);
                await Context.Channel.SendFileAsync(stream, "crabrave.gif");
                stream.Close();
            }
        }

        [Command("crab", RunMode = RunMode.Async)]
        [Summary("TOP TEXT;BOTTOM TEXT;Post a crab rave GIF.")]
        public async Task CrabRaveAsync(string topText, string bottomText)
        {
            using (Context.Channel.EnterTypingState())
            {
                Stream stream;
                lock (_intensiveMethodLock)
                {
                    stream = GetCrabRave(topText, bottomText);
                }

                stream.Seek(0, SeekOrigin.Begin);
                await Context.Channel.SendFileAsync(stream, "crabrave.gif");
                stream.Close();
            }
        }

        [Command("inspirobot", RunMode = RunMode.Async)]
        [Summary("Post an AI-generated inspirational quote.")]
        public async Task InspirobotAsync()
        {
            var (data, extension) = await ImageService.GetImage(null, ImageProvider.Inspirobot);
            await PostImage(data, "inspirobot" + extension);
        }

        #endregion

        #region Music

        private async Task<LavalinkPlayer> GetPlayer()
        {
            var voiceChannel = (Context.Message.Author as IGuildUser)?.VoiceChannel;

            if (voiceChannel == null)
            {
                return null;
            }

            LavalinkPlayer player = LavalinkManager.GetPlayer(Context.Guild.Id) ?? await LavalinkManager.JoinAsync(voiceChannel);

            return player;
        }

        [RequireContext(ContextType.Guild)]
        [Command("play")]
        public async Task PlayAsync([Remainder] string identifier)
        {
            using (Context.Channel.EnterTypingState())
            {
                var loadingMessage = await ReplyLoadingAsync("Searching");

                var player = await GetPlayer();

                if (player == null)
                {
                    await loadingMessage.ModifyAsync(x => x.Content = $"You need to be in a Voice Channel before invoking this command!");
                    return;
                }

                LoadTracksResponse response = await LavalinkManager.GetTracksAsync(identifier);
                LavalinkTrack track = response.Tracks.First();

                if (!player.Playing)
                {
                    await player.PlayAsync(track);
                    await PostCurrentTrack(track, loadingMessage);

                    LavalinkManager.TrackEnd += async (player, track, eventCode) =>
                    {
                        // TODO: Looping freaking out
                        LavalinkTrack newTrack;
                        var isTrackLooping = LavalinkManager.IsTrackLooping(Context.Guild.Id);

                        if (isTrackLooping)
                        {
                            newTrack = track;
                        }
                        else
                        {
                            newTrack = LavalinkManager.PopNextTrack(Context.Guild.Id);
                        }

                        if (newTrack != null)
                        {
                            await player.PlayAsync(newTrack);
                            if (!isTrackLooping)
                            {
                                await PostCurrentTrack(track);
                            }
                        }
                        else
                        {
                            LavalinkManager.FinishPlaying(Context.Guild.Id);
                        }
                    };
                }
                else
                {
                    LavalinkManager.AddTrack(Context.Guild.Id, track, Context.User.Id);
                }

                int trackCount = LavalinkManager.GetTrackCount(Context.Guild.Id);
                if (trackCount > 0)
                {
                    await loadingMessage.ModifyAsync(x => x.Content = $"There is **{trackCount}** tracks in the queue.");
                }
            }
        }

        private async Task PostCurrentTrack(LavalinkTrack track, IUserMessage message = null)
        {
            var embedBuilder = new EmbedBuilder
            {
                Title = "Now Playing!",
                Description =
                    $"[{track.Title}]({track.Url})\n" +
                    $"`[0:00 / {track.Length:mm':'ss}]`\n" +
                    $"\n" +
                    $"Requested by: <@{Context.User.Id}>\n",
                Color = Color.DarkPurple,
                ThumbnailUrl = $"https://img.youtube.com/vi/{track.Identifier}/mqdefault.jpg",
            };

            if (message != null)
            {
                await message.ModifyAsync(x => x.Embed = embedBuilder.Build());
                await message.ModifyAsync(x => x.Content = string.Empty);
            }
            else
            {
                await ReplyAsync(embed: embedBuilder.Build());
            }
        }

        [Command("stop")]
        public async Task StopAsync()
        {
            var player = await GetPlayer();

            await player.StopAsync();
            await player.DisconnectAsync();
        }

        [Command("loop")]
        public async Task LoopAsync()
        {
            var isLooping = LavalinkManager.LoopTrack(Context.Guild.Id);

            if (isLooping)
                await ReplyAsync("Current track will now loop... (Command currently semi-broken)");
            else
                await ReplyAsync("Current track will no longer be looping.");
        }

        [Command("volume")]
        public async Task VolumeAsync(uint volumeLevel)
        {
            var player = await GetPlayer();
            volumeLevel = volumeLevel > 100 ? 100 : volumeLevel;

            await player.SetVolumeAsync(volumeLevel);

            await ReplyAsync($"Volume set at **{volumeLevel}%**.");
        }

        [Command("skip")]
        public async Task SkipAsync()
        {
            var player = await GetPlayer();
            await player.StopAsync();

            var nextTrack = LavalinkManager.PopNextTrack(Context.Guild.Id);
            await ReplyAsync("Skipping...");

            if (nextTrack != null)
            {
                await player.PlayAsync(nextTrack);
            }
        }

        [Command("clear")]
        public async Task ClearAsync()
        {
            LavalinkManager.ClearTracks(Context.Guild.Id);
            await ReplyAsync("The queue has been cleared.");
        }

        #endregion

        #region Misc.

        [Summary("Ping the bot!")]
        [Command("ping")]
        public async Task PingAsync()
        {
            await ReplyAsync("Pong!");
        }

        [Summary("Be the hacker you always wanted to be.")]
        [Alias("rot")]
        [Command("rot13")]
        public async Task Rot13Async([Remainder] string text)
        {
            await ReplyAsync(ObfuscationHelper.Rot13(text));
        }

        [Summary("Post a random catte fact.")]
        [Alias("cattefact")]
        [Command("catfact")]
        public async Task CatFactAsync()
        {
            string response = await HttpClient.GetStringAsync("https://catfact.ninja/fact");
            string fact = JObject.Parse(response)["fact"].ToObject<string>();

            string cattifiedFact = fact
                .Replace("cat", "catte")
                .Replace("Cat", "Catte");

            string message = "Here's your catte fact! <<blob_cat>>\n```" + cattifiedFact + "```";
            await ReplyAsync(message);
        }

        [Command("mock")]
        [Summary("USER (optional);MoCK aN uSeR.")]
        public async Task MockAsync(IGuildUser user = null)
        {
            var messageGroup = await GetLastMessageGroup(user);
            var mockingMessageGroup = new List<string>();
            var random = new Random();

            foreach (var message in messageGroup)
            {
                char[] originalMessage = message.Content.ToCharArray();
                char[] mockingMessage = new char[originalMessage.Length];
                _ = mockingMessage.GetEnumerator();
                var lowerCaseCount = 0;
                var upperCaseCount = 0;

                for (int i = 0; i < message.Content.Length; i++)
                {
                    if (lowerCaseCount == 2)
                    {
                        mockingMessage[i] = char.ToUpper(originalMessage[i]);
                        upperCaseCount = 1;

                        lowerCaseCount = 0;
                    }
                    else if (upperCaseCount == 2)
                    {
                        mockingMessage[i] = char.ToLower(originalMessage[i]);
                        lowerCaseCount = 1;

                        upperCaseCount = 0;
                    }
                    else
                    {
                        if (random.Next(0, 2) == 0)
                        {
                            mockingMessage[i] = char.ToLower(originalMessage[i]);
                            lowerCaseCount++;
                        }
                        else
                        {
                            mockingMessage[i] = char.ToUpper(originalMessage[i]);
                            upperCaseCount++;
                        }
                    }
                }

                mockingMessageGroup.Add(new string(mockingMessage));
            }

            await ReplyAsync(string.Join("\n", mockingMessageGroup));
        }

        [Command("coin")]
        [Summary("Flip a coin.")]
        public async Task CoinAsync()
        {
            if (random.Next(0, 2) == 0)
            {
                await ReplyAsync(ImageLinks.Coin_Head);
            }
            else
            {
                await ReplyAsync(ImageLinks.Coin_Tail);
            }
        }

        [Command("uwu")]
        [Summary("USER (optional);Uwu-ify a post. uwu")]
        public async Task UwuAsync(IUser user = null)
        {
            var messageGroup = await GetLastMessageGroup(user);
            var uwuifiedMessageGroup = new List<string>();

            foreach (var message in messageGroup)
            {
                string uwuifiedMessage = message.Content
                    .Replace("r", "w")
                    .Replace("R", "W")
                    .Replace("l", "w")
                    .Replace("L", "W")
                    .Replace("na", "nya")
                    .Replace("nA", "nYA")
                    .Replace("ne", "nye")
                    .Replace("nE", "nYE")
                    .Replace("ni", "nyi")
                    .Replace("nI", "nYI")
                    .Replace("no", "nyo")
                    .Replace("nO", "nYO")
                    .Replace("nu", "nyu")
                    .Replace("nu", "nYU")
                    .Replace("Na", "Nya")
                    .Replace("Ne", "Nye")
                    .Replace("Ni", "Nyi")
                    .Replace("No", "Nyo")
                    .Replace("Nu", "Nyu")
                    .Replace("NA", "NYA")
                    .Replace("NE", "NYE")
                    .Replace("NI", "NYI")
                    .Replace("NO", "NYO")
                    .Replace("NU", "NYU")
                    .Replace("ove", "uv")
                    .Replace("ovE", "uv")
                    .Replace("oVe", "uV")
                    .Replace("oVE", "uV")
                    .Replace("oVe", "uV")
                    .Replace("OvE", "Uv")
                    .Replace("Ove", "Uv")
                    .Replace("OVe", "UV")
                    .Replace("OVE", "UV");

                uwuifiedMessageGroup.Add(uwuifiedMessage);
            }

            await ReplyAsync(string.Join("\n", uwuifiedMessageGroup) + " uwu");
        }

        [Command("clap")]
        [Summary("USER (optional);Clap-ify a post.")]
        public async Task ClapAsync(IUser user = null)
        {
            var messageGroup = await GetLastMessageGroup(user);
            var clappedMessageGroup = new List<string>();

            foreach (var message in messageGroup)
            {
                string clappedMessage = message.Content.Trim().Replace(" ", " :clap: ");
                clappedMessageGroup.Add(clappedMessage);
            }

            await ReplyAsync(string.Join("\n", clappedMessageGroup));
        }

        [Command("dadjoke")]
        [Summary("Post a random dad joke.")]
        public async Task DadJokeAsync()
        {
            var message = new HttpRequestMessage(HttpMethod.Get, "https://icanhazdadjoke.com/");
            message.Headers
                .Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));

            string joke;
            using (var response = await HttpClient.SendAsync(message))
            {
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                joke = JObject.Parse(content)["joke"].ToObject<string>();
            }

            await ReplyAsync(joke);
        }

        [Command("yomomma")]
        [Alias("momma")]
        [Summary("Post a random yo momma joke.")]
        public async Task YoMommaJokeAsync()
        {
            var message = new HttpRequestMessage(HttpMethod.Get, "https://api.yomomma.info/");
            message.Headers
                .Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));

            string joke;
            using (var response = await HttpClient.SendAsync(message))
            {
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                joke = JObject.Parse(content)["joke"].ToObject<string>();
            }

            if (joke.Last() != '.' && joke.Last() != '!' && joke.Last() != '?')
            {
                joke += ".";
            }

            await ReplyAsync(joke);
        }

        [Command("yin")]
        [Alias("yang")]
        [Summary("USER (optional);Determine the daily yin level of the user.")]
        public async Task YinAsync(IGuildUser user = null)
        {
            user ??= Context.User as IGuildUser;
            int yinLevel = Context.User.Id == 272139684824743936 ? 100 : CalculatDailyYin(user);

            var message = string.Format(
                "<@{0}> has a daily *yin* level of **{1}%**! (**{2}%** *yang!*)\nThey are being a total  {3} **{4}** {3}  today!",
                user.Id,
                yinLevel.ToString(),
                (100 - yinLevel).ToString(),
                yinLevel >= 50 ? "<<white_star>>" : "<<black_star>>",
                yinLevel >= 50 ? "YIN" : "YANG"
            );

            await ReplyAsync(message);
        }

        [Command("love")]
        [Summary("USER1 USER2;Determe the love compatibility between two people.")]
        public async Task LoveAsync(IGuildUser user1, IGuildUser user2)
        {
            if (user1 == user2)
            {
                await ReplyAsync("You can't love yourself, silly! :broken_heart:");
                return;
            }

            int compatibility;

            if (user1.Id == DiscordSocketClient.CurrentUser.Id || user2.Id == DiscordSocketClient.CurrentUser.Id)
            {
                compatibility = 100;
            }
            else
            {
                var seed = (user1.Id + user2.Id).ToString();
                compatibility = Math.Abs(GetStableHashCode(seed) % 101);
            }

            await ReplyAsync(string.Format(
                "<@{0}> and <@{1}> have a love compatibility of **{2}%**! :heart:",
                user1.Id,
                user2.Id,
                compatibility
            ));
        }

        [Command("roll")]
        [Alias("dice", "random")]
        [Summary("ex: 1d20 - 1d8 + 10;Roll some dices!")]
        public async Task RollAsync([Remainder] string text)
        {
            var diceRoller = new DiceRoller()
            {
                Text = text
            };

            bool success = diceRoller.Roll();

            if (success)
            {
                string reply = string.Format(
                    "<@{0}> rolls!\n" +
                    "\n" +
                    (diceRoller.Description != null ? diceRoller.Description + ":   " : "") + "**{1}**\n" +
                    "`{2}`",
                    Context.User.Id, diceRoller.Result, diceRoller.Calculation
                );

                await ReplyAsync(reply);
            }
            else
            {
                await ReplyAsync("Syntax error!");
            }
        }

        [Command("8ball", RunMode = RunMode.Async)]
        [Alias("ask")]
        [Summary("Ask platybot a question!")]
        public async Task EightBallballAsync([Remainder] string question)
        {
            if (question.Trim() == string.Empty) return;

            string answer = GetRandomLineFromList("8ball.txt");
            await ReplyAsync(answer);
        }

        [Command("eternal_september")]
        [Alias("eternalseptember", "eternal", "september")]
        [Summary("Look up the Eternal September date.")]
        public async Task EternalSeptemberAsync()
        {
            var now = DateTime.Now;
            var september = new DateTime(1993, 9, 1);
            TimeSpan difference = now - september;
            var dayOfSeptember = difference.Days + 1;

            string daySuffix = (dayOfSeptember % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th",
            };

            var eternalSeptember =
                string.Format("The date is:\n**{0}, September {1}{2}, 1993**",
                now.DayOfWeek.ToString(),
                dayOfSeptember,
                daySuffix);

            await ReplyAsync(eternalSeptember);
        }

        [Command("rotten_tomatoes")]
        [Alias("rottentomatoes", "rotten", "tomatoes", "rt")]
        [Summary("MOVIE_NAME;Fetch the Rotten Tomatoes rating for specified movie.")]
        public async Task RottenTomatoesAsync([Remainder] string title)
        {
            var searchMessage = new HttpRequestMessage(HttpMethod.Get, $"https://www.rottentomatoes.com/api/private/v2.0/search?q={title}&limit=1");
            searchMessage.Headers
                .Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));

            JToken movie;
            using (var response = await HttpClient.SendAsync(searchMessage))
            {
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                movie = JObject.Parse(content)["movies"].FirstOrDefault();
            }

            if (movie == null)
            {
                await ReplyAsync("Can't find a movie by that name!");
                return;
            }

            string name = movie["name"].ToObject<string>();
            string year = movie["year"].ToObject<string>();
            string imageUrl = movie["image"].ToObject<string>();

            string score;
            if (movie["meterScore"] != null)
            {
                score = movie["meterScore"].ToObject<string>() + "%";
            }
            else
            {
                score = "No Score";
            }

            string meterClass = movie["meterClass"].ToObject<string>();

            string freshnessIcon;
            Color? freshnessColor;

            switch (meterClass)
            {
                case "certified_fresh":
                    freshnessIcon = DataContext.InsertRawEmotes("<<rt_certified_fresh>>");
                    freshnessColor = new Color(0xF9D320);
                    break;
                case "fresh":
                    freshnessIcon = DataContext.InsertRawEmotes("<<rt_fresh>>");
                    freshnessColor = new Color(0xFA320A);
                    break;
                default: // N/A
                    freshnessIcon = ":question:";
                    freshnessColor = new Color(0x0AC855);
                    break;
            }

            var embedBuilder = new EmbedBuilder
            {
                Title = $"{name} ({year})",
                Description = $"{freshnessIcon} {score}",
                ThumbnailUrl = imageUrl,
                Color = freshnessColor,
                Url = "https://www.rottentomatoes.com" + movie["url"]
            };

            await ReplyAsync(null, false, embedBuilder.Build());
        }

        [Summary("Urianger translator!.")]
        [Command("urianger")]
        public async Task UriangerAsync(IGuildUser user = null)
        {
            var messageGroup = await GetLastMessageGroup(user);
            var uriangerMessageGroup = new List<string>();

            foreach (var message in messageGroup)
            {
                var uriangerMessage = message.Content
                    .Replace("again", "once more", StringComparison.OrdinalIgnoreCase)
                    .Replace("are", "art", StringComparison.OrdinalIgnoreCase)
                    .Replace("back", "returned", StringComparison.OrdinalIgnoreCase)
                    .Replace("bad", "dire", StringComparison.OrdinalIgnoreCase)
                    .Replace("between", "'twixt", StringComparison.OrdinalIgnoreCase)
                    .Replace("come", "cameth", StringComparison.OrdinalIgnoreCase)
                    .Replace("crazy", "chaos", StringComparison.OrdinalIgnoreCase)
                    .Replace("cool", "excellent", StringComparison.OrdinalIgnoreCase)
                    .Replace("dearest", "dearest", StringComparison.OrdinalIgnoreCase)
                    .Replace("defeat", "vanquish", StringComparison.OrdinalIgnoreCase)
                    .Replace("did", "didst", StringComparison.OrdinalIgnoreCase)
                    .Replace("do", "doth", StringComparison.OrdinalIgnoreCase)
                    .Replace("does", "doth", StringComparison.OrdinalIgnoreCase)
                    .Replace("fast", "fleeting", StringComparison.OrdinalIgnoreCase)
                    .Replace("go", "goest", StringComparison.OrdinalIgnoreCase)
                    .Replace("have", "hath", StringComparison.OrdinalIgnoreCase)
                    .Replace("hello", "hail", StringComparison.OrdinalIgnoreCase)
                    .Replace("help", "serve", StringComparison.OrdinalIgnoreCase)
                    .Replace("here", "hither", StringComparison.OrdinalIgnoreCase)
                    .Replace("hey", "hail", StringComparison.OrdinalIgnoreCase)
                    .Replace("hi", "hail", StringComparison.OrdinalIgnoreCase)
                    .Replace("it is", "'tis", StringComparison.OrdinalIgnoreCase)
                    .Replace("its", "'tis", StringComparison.OrdinalIgnoreCase)
                    .Replace("killed", "slain", StringComparison.OrdinalIgnoreCase)
                    .Replace("many", "numerous", StringComparison.OrdinalIgnoreCase)
                    .Replace("need", "requireth", StringComparison.OrdinalIgnoreCase)
                    .Replace("newbie", "neophyte", StringComparison.OrdinalIgnoreCase)
                    .Replace("no", "nay", StringComparison.OrdinalIgnoreCase)
                    .Replace("nah", "nay", StringComparison.OrdinalIgnoreCase)
                    .Replace("only", "merely", StringComparison.OrdinalIgnoreCase)
                    .Replace("perhaps", "mayhap", StringComparison.OrdinalIgnoreCase)
                    .Replace("please", "pray", StringComparison.OrdinalIgnoreCase)
                    .Replace("plz", "pray", StringComparison.OrdinalIgnoreCase)
                    .Replace("pls", "pray", StringComparison.OrdinalIgnoreCase)
                    .Replace("probaby", "perhaps", StringComparison.OrdinalIgnoreCase)
                    .Replace("require", "requireth", StringComparison.OrdinalIgnoreCase)
                    .Replace("rises", "riseth", StringComparison.OrdinalIgnoreCase)
                    .Replace("sometimes", "On occasion", StringComparison.OrdinalIgnoreCase)
                    .Replace("there", "yon", StringComparison.OrdinalIgnoreCase)
                    .Replace("to", "unto", StringComparison.OrdinalIgnoreCase)
                    .Replace("tomorrow", "morrow", StringComparison.OrdinalIgnoreCase)
                    .Replace("want", "desire", StringComparison.OrdinalIgnoreCase)
                    .Replace("weird", "strange", StringComparison.OrdinalIgnoreCase)
                    .Replace("will", "shall", StringComparison.OrdinalIgnoreCase)
                    .Replace("win", "prevail", StringComparison.OrdinalIgnoreCase)
                    .Replace("written", "writ", StringComparison.OrdinalIgnoreCase)
                    .Replace("yes", "aye", StringComparison.OrdinalIgnoreCase)
                    .Replace("yep", "aye", StringComparison.OrdinalIgnoreCase)
                    .Replace("yea", "aye", StringComparison.OrdinalIgnoreCase)
                    .Replace("yesterday", "yester", StringComparison.OrdinalIgnoreCase)
                    .Replace("you", "thou", StringComparison.OrdinalIgnoreCase)
                    .Replace("u", "thou", StringComparison.OrdinalIgnoreCase)
                    .Replace("lmfao", "... hah!", StringComparison.OrdinalIgnoreCase)
                    .Replace("lmao", "... hah!", StringComparison.OrdinalIgnoreCase)
                    .Replace("lol", "... hah!", StringComparison.OrdinalIgnoreCase)
                    .Replace("rofl", "... hah!", StringComparison.OrdinalIgnoreCase)
                    .Replace("haha", "... hah!", StringComparison.OrdinalIgnoreCase)
                    .Replace("irl", "on the source", StringComparison.OrdinalIgnoreCase)
                    .Replace("whats", "what 'tis", StringComparison.OrdinalIgnoreCase)
                    .Replace("im", "I am", StringComparison.OrdinalIgnoreCase)
                    .Replace("?", "What will you ask?", StringComparison.OrdinalIgnoreCase)
                    .Replace("o/", "Greetings.", StringComparison.OrdinalIgnoreCase)
                    .Replace("afk", "A moment to collect my thoughts, I prithee...", StringComparison.OrdinalIgnoreCase)
                    .Replace("brb", "I shall be but a few moments.", StringComparison.OrdinalIgnoreCase)
                    .Replace("lfg", "Might I impose on thee to allow entrance to thine party?", StringComparison.OrdinalIgnoreCase)
                    .Replace("lf", "I pray thou wilt join our group", StringComparison.OrdinalIgnoreCase)
                    .Replace("inv", "I pray thou wilt submit an invitation", StringComparison.OrdinalIgnoreCase)
                    .Replace("bye", "farewell", StringComparison.OrdinalIgnoreCase)
                    .Replace("god", "the Twelve", StringComparison.OrdinalIgnoreCase)
                    .Replace("<3", "I have more than words for you, my friend.", StringComparison.OrdinalIgnoreCase)
                    .Replace(":d", "Words cannot well express my joy", StringComparison.OrdinalIgnoreCase)
                    .Replace("implying", "stating", StringComparison.OrdinalIgnoreCase)
                    .Replace("tyfp", "You have my thanks", StringComparison.OrdinalIgnoreCase)
                    .Replace("before", "ere", StringComparison.OrdinalIgnoreCase)
                    .Replace("sorry", "Pray accept mine apologies", StringComparison.OrdinalIgnoreCase)
                    .Replace("oops", "Pray accept mine apologies", StringComparison.OrdinalIgnoreCase)
                    .Replace("whoops", "Pray accept mine apologies", StringComparison.OrdinalIgnoreCase)
                    .Replace("sup", "How fares the realm?", StringComparison.OrdinalIgnoreCase)
                    .Replace("possess", "possesseth", StringComparison.OrdinalIgnoreCase)
                    .Replace("idk", "I know not", StringComparison.OrdinalIgnoreCase);

                uriangerMessageGroup.Add(uriangerMessage);
            }

            await ReplyAsync(string.Join("\n", uriangerMessageGroup));
        }

        [Command("roulette", RunMode = RunMode.Async)]
        [Summary("Try out your luck with the Timeout Gun!")]
        public async Task RouletteAsync()
        {
            var winImageUrl = ImageLinks.Roulette_Win;
            var lostImageUrl = ImageLinks.Roulette_Loss;
            bool won;

            var user = Context.User as IGuildUser;

            string message = $"<@{user.Id}> tries their luck with the PlatyGun...\n";
            var lostTheGame = random.Next(3) == 2;

            if (lostTheGame)
            {
                try
                {
                    await user.SetTimeOutAsync(new TimeSpan(0, ROULETTE_TIMEOUT_IN_MINUTES, 0));
                    message += $"***BANG!***\n\nThey are timed out for **{ROULETTE_TIMEOUT_IN_MINUTES} minutes**!";
                    won = false;
                }
                catch
                {
                    message += $"***BANG!***\n\nBut they are too fast and avoid the shot! **Wow!**";
                    won = false;
                }
            }
            else
            {
                message += "The PlatyGun spares them... For now.";
                won = true;
            }

            var embedBuilder = new EmbedBuilder
            {
                Title = $"**The PlatyRoulette!**",
                Description = message,
                Color = won ? Color.Green : Color.Red,
                ThumbnailUrl = won ? winImageUrl : lostImageUrl,
            };

            await ReplyAsync(embed: embedBuilder.Build());
        }

        #endregion

        #region Hidden

        [Command("silence_crab", RunMode = RunMode.Async)]
        [Alias("silence")]
        public async Task SilenceCrabAsync([Remainder] string text)
        {
            try
            {
                var userId = ulong.Parse(text.Substring(3, 18));
                var user = Context.Guild.GetUser(userId);
                var newText = user.Nickname ?? user.Username;
                text = newText;
            }
            catch (Exception ex)
            {
                PlatybotLogger.Log(ex.ToString(), writeToFile: true);
            }

            using (Context.Channel.EnterTypingState())
            {
                Stream stream;
                lock (_intensiveMethodLock)
                {
                    stream = GetSilenceCrab(text);
                }

                stream.Seek(0, SeekOrigin.Begin);
                await Context.Channel.SendFileAsync(stream, "silence_crab.png");
                stream.Close();
            }
        }

        [Command("small", RunMode = RunMode.Async)]
        public async Task SmallAsync(IUser user = null)
        {
            var messageGroup = await GetLastMessageGroup(user);
            var smallMessageGroup = new List<string>();

            foreach (var message in messageGroup)
            {
                string smallMessage = Smallify(message.Content);
                smallMessageGroup.Add(smallMessage);
            }

            await ReplyAsync(string.Join("\n", smallMessageGroup));
        }

        [Command("flip", RunMode = RunMode.Async)]
        public async Task BigAsync(IUser user = null)
        {
            var messageGroup = await GetLastMessageGroup(user);
            var smallMessageGroup = new List<string>();

            foreach (var message in messageGroup)
            {
                string smallMessage = Flipify(message.Content);
                smallMessageGroup.Add(smallMessage);
            }

            smallMessageGroup.Reverse();

            await ReplyAsync(string.Join("\n", smallMessageGroup));
        }

        [Command("bubble_wrap", RunMode = RunMode.Async)]
        [Alias("bubblewrap", "bubble", "wrap", "pop", "pops")]
        public async Task BubbleWrapAsync()
        {
            var pops = string.Concat(Enumerable.Repeat("||pop!||", 10));
            pops = string.Concat(Enumerable.Repeat(pops + "\n", 8));

            await ReplyAsync(pops);
        }

        [Command("pitchfork", RunMode = RunMode.Async)]
        [Alias("pitchfork_store", "pitchforkstore")]
        public async Task PitchforkAsync()
        {
            var pitchforkEmoji = "<<pitchfork>>";
            await ReplyAsync("*Platybot hands out some pitchforks...*");
            await ReplyAsync(string.Concat(Enumerable.Repeat(pitchforkEmoji, 5)));
        }

        #endregion

        #region Help

        [Command("emotes")]
        [Summary("List all emotes.")]
        public async Task EmotesAsync()
        {
            List<string> commands = EmoteService.GetSimpleCommand();

            var embedBuilder = new EmbedBuilder
            {
                Title = "Platybot's Command List"
            };


            string commandList = "";
            foreach (string command in commands)
            {
                commandList += "• " + command + "\n";
            }

            embedBuilder.AddField("Emotes", commandList);
            embedBuilder.WithThumbnailUrl("https://i.imgur.com/7RCAYmY.png");
            await ReplyAsync(embed: embedBuilder.Build());
        }

        [Command("help")]
        public async Task HelpAsync()
        {
            List<CommandInfo> commands = CommandService.Commands.ToList();
            var embedBuilder = new EmbedBuilder
            {
                Title = "Platybot's Command List"
            };

            foreach (CommandInfo command in commands)
            {
                if (!string.IsNullOrEmpty(command.Summary))
                {
                    var commandName = ConfigHelper.DEFAULT_PREFIX + command.Name;
                    var commandSummary = "";
                    var summaryParts = command.Summary.Split(";");

                    foreach (var summaryPart in summaryParts)
                    {
                        if (!summaryPart.Equals(summaryParts.Last()))
                        {
                            commandName += "   [" + summaryPart + "]";
                        }
                        else
                        {
                            commandSummary = summaryPart;
                        }
                    }

                    embedBuilder.AddField(commandName, commandSummary);
                }
            }

            embedBuilder.AddField("=== NEW COMMANDS ===", string.Format("If you have a command idea you'd like for me to learn, please contact my mom: <@{0}>", ConfigHelper.SUPERUSER_ID));

            embedBuilder.WithThumbnailUrl("https://i.imgur.com/7RCAYmY.png");
            await ReplyAsync(embed: embedBuilder.Build());
        }

        #endregion

        #region Private
        private static int CalculatDailyYin(IGuildUser user)
        {
            int seed = GetDailyStableHashCode(user.Id.ToString());

            int yinLevel = Math.Abs(seed) % 101;
            return yinLevel;
        }

        private static string Smallify(string text)
        {
            string alphabet = "abcdefghijklmnopqrstuvwxyz";
            string superScript = "ᵃᵇᶜᵈᵉᶠᵍʰᶦʲᵏˡᵐⁿᵒᵖᑫʳˢᵗᵘᵛʷˣʸᶻ";
            text = text.ToLower();

            for (int i = 0; i < alphabet.Length; i++)
            {
                text = text.Replace(alphabet[i], superScript[i]);
            }

            return text;
        }

        private static string Flipify(string text)
        {
            string alphabet = @"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string flippedAlphabet = @"ɐqɔpǝⅎƃɥᴉɾʞʅɯuodbɹsʇnʌʍxʎz∀ꓭϽᗡƎᖵ⅁HIᒋꓘ⅂ꟽNOԀꝹꓤSꓕՈɅϺX⅄Z";

            for (int i = 0; i < alphabet.Length; i++)
            {
                text = text.Replace(alphabet[i], flippedAlphabet[i]);
            }

            text = new string(text.Reverse().ToArray());
            return text;
        }
        #endregion
    }
}
