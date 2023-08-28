using Platybot.Constants;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Platybot.Helpers
{
    internal static class DotEnv
    {
        public static void Load()
        {
            string filePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            filePath = Path.Join(filePath, PathConstants.ENV_FILE_EXTENSION);

            if (!File.Exists(filePath))
                return;

            foreach (var line in File.ReadAllLines(filePath))
            {
                var parts = line.Split('=', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();

                if (parts.Length != 2)
                    continue;

                Environment.SetEnvironmentVariable(parts[0], parts[1]);
            }
        }
    }
}
