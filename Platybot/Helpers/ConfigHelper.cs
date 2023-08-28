using Platybot.Constants;
using System;
using System.IO;
using System.Reflection;

namespace Platybot.Helpers
{
    internal static class ConfigHelper
    {
        public static string TOKEN
        {
            get
            {
                return GetEnvironmentVariable(EnvironmentVariables.TOKEN);
            }
        }

        public static string DEFAULT_PREFIX
        {
            get
            {
                return GetEnvironmentVariable(EnvironmentVariables.DEFAULT_PREFIX);
            }
        }

        public static ulong SUPERUSER_ID
        {
            get
            {
                return ulong.Parse(EnvironmentVariables.SUPERUSER_ID);
            }
        }

        public static ulong HOME_GUILD_ID
        {
            get
            {
                return ulong.Parse(EnvironmentVariables.HOME_GUILD_ID);
            }
        }

        public static ulong HOME_GUILD_CHANNEL_ID
        {
            get
            {
                return ulong.Parse(EnvironmentVariables.HOME_GUILD_CHANNEL_ID);
            }
        }

        private static string GetEnvironmentVariable(string name)
        {
            name = EnvironmentVariables.PLATYBOT_PREFIX + name;
            var value = Environment.GetEnvironmentVariable(name);

            return value;
        }
    }

}
