using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using Platybot.Constants;
using Platybot.Data;
using System;
using System.Threading.Tasks;

namespace Platybot.Attributes
{
    internal class RequireRestrictedCommandAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var dataContext = services.GetRequiredService<DataContext>();
            bool allowed = dataContext.IsCommandAllowed(context.Guild.Id, context.Channel.Id, command.Name);

            PreconditionResult result = allowed
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError(AttributeErrorMessages.SECRET_COMMAND_NOT_ALLOWED);

            return Task.FromResult(result);
        }
    }
}
