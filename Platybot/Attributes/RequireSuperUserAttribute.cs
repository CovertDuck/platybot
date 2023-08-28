using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using Platybot.Helpers;
using Platybot.Constants;
using Platybot.Data;
using System;
using System.Threading.Tasks;

namespace Platybot.Attributes
{
    internal class RequireSuperUserAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var botContext = services.GetRequiredService<DataContext>();

            bool isSuperUser = ConfigHelper.SUPERUSER_ID == context.User.Id;

            PreconditionResult result = isSuperUser
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError(AttributeErrorMessages.NOT_A_SUPERUSER);

            return Task.FromResult(result);
        }
    }
}
