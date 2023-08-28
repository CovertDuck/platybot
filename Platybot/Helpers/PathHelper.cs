using Platybot.Constants;
using System.IO;
using System.Reflection;

namespace Platybot.Helpers
{
    internal static class PathHelper
    {
        private static string BasePath { get => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }

        public static string WorkingDirectory { get => BasePath; }
        public static string DataDirectory { get => Path.Join(BasePath, PathConstants.DATA); }
        public static string LogDirectory { get => Path.Join(BasePath, PathConstants.LOGS); }
    }
}
