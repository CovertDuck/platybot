using Platybot.Constants;
using Platybot.Enums;
using Platybot.Helpers;
using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Platybot.Logger
{
    internal static class PlatybotLogger
    {
        public static void Log(string message, bool timestamp = true, bool writeToFile = false, bool writeToOutput = true, bool platybotPrefix = false, LogType logType = LogType.Normal)
        {
            var logDirectory = PathHelper.LogDirectory;

            if (!Directory.Exists(logDirectory))
                Directory.CreateDirectory(logDirectory);

            string filename = logType switch
            {
                LogType.Normal => Path.Join(logDirectory, PathConstants.PLATYBOT_LOG_FILE),
                LogType.Rule34 => Path.Join(logDirectory, PathConstants.RULE34_LOG_FILE),
                _ => Path.Join(logDirectory, PathConstants.PLATYBOT_LOG_FILE)
            };

            var logMessage = string.Empty;

            if (timestamp)
            {
                logMessage += $"[{DateTime.Now:HH:mm:ss.fff}]";
            }

            if (writeToOutput)
            {
                logMessage += "[" + Assembly.GetExecutingAssembly().GetName().Name + "] ";
            }

            logMessage += message + "\n";

            if (writeToFile)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.Append(logMessage);
                File.AppendAllText(filename, stringBuilder.ToString());
            }
            else if (platybotPrefix)
            {
                Console.WriteLine(logMessage.Replace("\n", ""));
            }
            else if (writeToOutput)
            {
                Console.WriteLine(message);
            }
        }
    }
}
