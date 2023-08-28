using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platybot.Data;
using Platybot.Services;
using SharpLink;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.IO;
using Platybot.Constants;
using Platybot.Enums;
using Platybot.Helpers;
using Platybot.Logger;

namespace Platybot
{
    class Program
    {
        private ServiceProvider _services;
        private DiscordSocketClient _client;
        private DataContext _dataContext;

        static void Main()
        {
            DotEnv.Load();
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        private async Task MainAsync()
        {
            _services = ConfigureServices();
            _client = _services.GetRequiredService<DiscordSocketClient>();
            _dataContext = _services.GetRequiredService<DataContext>();

            if (string.IsNullOrWhiteSpace(ConfigHelper.TOKEN))
            {
                PlatybotLogger.Log("Please provide a PLATYBOT_TOKEN. Closing down...", platybotPrefix: true);
                await Task.Delay(5 * 1000);
                Environment.Exit((int)ExitCode.NoToken);
            }

            _client.Connected += OnConnected;
            _client.Ready += ReadyAsync;
            _client.Log += LogAsync;

            _client.UserJoined += OnUserJoinedAsync;
            _client.UserBanned += OnUserBannedAsync;
            _client.UserUnbanned += OnUserUnbanned;
            _client.UserLeft += OnUserLeftAsync;

            _services.GetRequiredService<CommandService>().Log += LogAsync;

            await _client.LoginAsync(TokenType.Bot, ConfigHelper.TOKEN);
            await _client.StartAsync();

            _services.GetRequiredService<CommandHandler>().InitializeAsync();

            await Task.Delay(-1);
        }

        private Task OnConnected()
        {
            _ = Task.Run(async () =>
            {
                await _client.DownloadUsersAsync(_client.Guilds);
            });

            return Task.CompletedTask;
        }

        private Task ReadyAsync()
        {
            _ = Task.Run(async () =>
            {
                // Server List
                var guilds = _client.Guilds;
                var message = $"Bot is active in {guilds.Count} server{(guilds.Count > 1 ? "s" : "")}: ";
                message += string.Join(", ", guilds.Select(x => x.Name));
                PlatybotLogger.Log(message, platybotPrefix: true);

                // Lavalink
                var lavalinkManager = _services.GetRequiredService<PlatybotLavalinkManager>();
                await lavalinkManager.StartAsync();

                // OpenAi
                var botAIService = _services.GetRequiredService<AIService>();
                botAIService.Init();

                // TimerService
                var timerService = _services.GetRequiredService<TimerService>();
                timerService.Init();
            });

            return Task.CompletedTask;
        }

        private Task LogAsync(LogMessage log)
        {
            bool saveToFile = log.Severity == LogSeverity.Error;
            PlatybotLogger.Log(log.ToString(), false, saveToFile);

            return Task.CompletedTask;
        }

        #region UserEvents

        private async Task OnUserJoinedAsync(SocketGuildUser user)
        {
            await AlertAsync(user, UserEvent.Join);

            if (!_dataContext.GetGuildConfiguration(user.Guild.Id).WelcomeNewcomers) return;

            var welcomeMessage = _dataContext.GetGuildConfiguration(user.Guild.Id).WelcomeMessage;

            var stream = ImageService.GetGreetings(user.Username, welcomeMessage);
            var systemChannel = user.Guild.SystemChannel;

            if (systemChannel == null) return;

            await systemChannel.SendFileAsync(stream, "greetings.png", "");
            stream.Close();

            UpdateMemberCount(user.Guild);
        }

        private async Task OnUserBannedAsync(SocketUser user, SocketGuild guild)
        {
            await AlertAsync(user, UserEvent.Ban);

            UpdateMemberCount(guild);
        }

        private async Task OnUserUnbanned(SocketUser user, SocketGuild guild)
        {
            await AlertAsync(user, UserEvent.Unban);
        }

        private async Task OnUserLeftAsync(SocketGuild guild, SocketUser user)
        {
            await AlertAsync(user, UserEvent.Leave);

            UpdateMemberCount(guild);
        }

        private async Task AlertAsync(SocketUser socketUser, UserEvent userEvent)
        {
            if (socketUser is not SocketGuildUser socketGuildUser) return;

            await AlertAsync(socketGuildUser, userEvent);
        }

        private async Task AlertAsync(SocketGuildUser user, UserEvent userEvent)
        {
            var guild = user.Guild;

            var alertChannelId = _dataContext.GetGuildConfiguration(guild.Id).AlertChannelId;

            var alertChannel = guild.GetChannel(alertChannelId.Value) as IMessageChannel;

            string title = $"{user.Username}";
            string description = $"**User: <@{user.Id}>**";
            Color color;

            var banInfos = await guild.GetBanAsync(user);
            if (banInfos is not null && userEvent != UserEvent.Ban) return;

            switch (userEvent)
            {
                case UserEvent.Join:
                    title += $" JOINED";
                    color = Color.Green;
                    break;
                case UserEvent.Leave:
                    title += $" LEFT";
                    color = Color.Orange;
                    break;
                case UserEvent.Ban:
                    title += $" BANNED";
                    color = Color.Red;
                    description += $"\n**Reason**: {banInfos.Reason}";
                    break;
                case UserEvent.Unban:
                    title += $" UNBANNED";
                    color = Color.Green;
                    break;
                default:
                    return;
            }

            var embedBuilder = new EmbedBuilder
            {
                Title = title,
                Description = description,
                Color = color,
                ThumbnailUrl = user.GetAvatarUrl()
            };

            await alertChannel.SendMessageAsync(embed: embedBuilder.Build());
        }

        private void UpdateMemberCount(SocketGuild guild)
        {
            var memberCountChannelId = _dataContext.GetGuildConfiguration(guild.Id).MemberCountChannelId;

            if (memberCountChannelId is null)
                return;

            var memberCountChannel = guild.GetChannel((ulong)memberCountChannelId);

            memberCountChannel.ModifyAsync(x => x.Name = $"Total Members: {guild.MemberCount}");
        }

        #endregion

        private static ServiceProvider ConfigureServices()
        {
            var socketConfig = new DiscordSocketConfig()
            {
                AlwaysDownloadUsers = true,
                GatewayIntents = GatewayIntents.All & ~GatewayIntents.GuildPresences & ~GatewayIntents.GuildScheduledEvents & ~GatewayIntents.GuildInvites

            };
            var client = new DiscordSocketClient(socketConfig);

            var lavalinkManager = new PlatybotLavalinkManager(client, new LavalinkManagerConfig
            {
                RESTHost = "localhost",
                RESTPort = 2333,
                WebSocketHost = "localhost",
                WebSocketPort = 2333,
                Authorization = "youshallnotpass",
                TotalShards = 1
            });

            return new ServiceCollection()
                .AddSingleton(client)
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandler>()
                .AddSingleton<InteractionService>()
                .AddSingleton<HttpClient>()
                .AddSingleton<SimpleCommandService>()
                .AddSingleton<ImageService>()
                .AddSingleton<TimerService>()
                .AddSingleton<EasterEggService>()
                .AddSingleton<FF14Service>()
                .AddSingleton<AIService>()
                .AddSingleton(lavalinkManager)
                .AddDbContext<DataContext>(options => options.UseSqlite("Data Source=" + Path.Join(PathHelper.DataDirectory, PathConstants.PLATYBOT_DB_FILE)))
                .BuildServiceProvider();
        }
    }
}
