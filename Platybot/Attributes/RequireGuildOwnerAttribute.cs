using Discord.Commands;
using Platybot.Constants;
using System;
using System.Threading.Tasks;

namespace Platybot.Attributes
{
    internal class RequireGuildOwnerAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            bool isGuildOwner = context.Guild.OwnerId == context.User.Id;

            PreconditionResult result = isGuildOwner
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError(AttributeErrorMessages.NOT_SERVER_OWNER);

            return Task.FromResult(result);
        }
    }
}
