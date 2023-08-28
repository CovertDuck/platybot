using Discord;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using Platybot.Helpers;
using Platybot.Constants;
using Platybot.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Platybot.Attributes
{
    internal class RequireModeratorAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            bool isGuildOwner = context.Guild.OwnerId == context.User.Id;
            if (isGuildOwner)
            {
                return Task.FromResult(PreconditionResult.FromSuccess());
            }

            var botContext = services.GetRequiredService<DataContext>();

            bool isSuperUser = ConfigHelper.SUPERUSER_ID == context.User.Id;
            if (isSuperUser)
            {
                return Task.FromResult(PreconditionResult.FromSuccess());
            }

            PreconditionResult result;
            if (context.User is not IGuildUser user)
            {
                result = PreconditionResult.FromError(AttributeErrorMessages.COMMAND_CANNOT_EXECUTE_OUTSIDE_SERVER);
            }
            else
            {
                bool isModerator = user.RoleIds.Intersect(botContext.SelectModeratorRoleIds(user.GuildId)).Any();

                result = isModerator
                    ? PreconditionResult.FromSuccess()
                    : PreconditionResult.FromError(AttributeErrorMessages.NOT_A_MODERATOR);
            }

            return Task.FromResult(result);
        }
    }
}
