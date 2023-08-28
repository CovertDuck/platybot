using Platybot.Constants;
using Platybot.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Platybot.Services
{
    internal class SimpleCommandService
    {
        readonly Dictionary<string, ActionCommand> _actions;
        readonly Dictionary<string, string> _links;
        readonly Dictionary<string, string> _copypastas;

        public SimpleCommandService()
        {
            _actions = GetActions();
            _links = GetLinks();
            _copypastas = GetCopypastas();
        }

        private static Dictionary<string, ActionCommand> GetActions()
        {
            var emotes = new Dictionary<string, ActionCommand>();

            string assemblyDirectory = Path.GetDirectoryName(AppContext.BaseDirectory);
            var lines = File.ReadLines(Path.Join(assemblyDirectory, PathConstants.MODULES_FOLDER, PathConstants.SIMPLE_FOLDER, PathConstants.ACTIONS_FILE));
            foreach (var line in lines)
            {
                string[] parameters = line.Split(";");
                if (parameters.Length == 3)
                {
                    emotes.Add(parameters[0], new ActionCommand
                    {
                        MessageSelf = parameters[1],
                        MessageTarget = parameters[2]
                    });
                }
            }

            return emotes;
        }

        private static Dictionary<string, string> GetLinks()
        {
            var emotes = new Dictionary<string, string>();

            string assemblyDirectory = Path.GetDirectoryName(AppContext.BaseDirectory);
            var lines = File.ReadLines(Path.Join(assemblyDirectory, PathConstants.MODULES_FOLDER, PathConstants.SIMPLE_FOLDER, PathConstants.LINKS_FILE));
            foreach (var line in lines)
            {
                string[] parameters = line.Split(";");
                if (parameters.Length == 2)
                {
                    foreach (var trigger in parameters[0].Split(",").Select(x => x.Trim()).ToArray())
                    {
                        emotes.Add(trigger, parameters[1]);
                    }
                }
            }

            return emotes;
        }

        private static Dictionary<string, string> GetCopypastas()
        {
            var emotes = new Dictionary<string, string>();

            string assemblyDirectory = Path.GetDirectoryName(AppContext.BaseDirectory);
            var lines = ReadFile(Path.Join(assemblyDirectory, PathConstants.MODULES_FOLDER, PathConstants.SIMPLE_FOLDER, PathConstants.COPY_PASTAS_FILE));
            foreach (var line in lines)
            {
                string[] parameters = line.Split(";");
                emotes.Add(parameters[0], string.Concat(parameters[1..]).Replace("\\n", Environment.NewLine));
            }

            return emotes;
        }

        public List<string> GetSimpleCommand()
        {
            var commands = new List<string>();
            foreach (KeyValuePair<string, ActionCommand> textEmote in _actions)
            {
                commands.Add(textEmote.Key);
            }

            commands.Sort();
            return commands;
        }

        public bool HasSimpleCommand(string name)
        {
            int commandNameEnd = name.IndexOf(' ');
            name = commandNameEnd == -1 ? name : name[..commandNameEnd];

            return _actions.ContainsKey(name) || _links.ContainsKey(name) || _copypastas.ContainsKey(name);
        }

        private static string[] ReadFile(string filename)
        {
            return File.ReadAllLines(filename).Where(x => !x.Trim().StartsWith("#") && !string.IsNullOrWhiteSpace(x)).ToArray();
        }

        public string GetSimpleCommand(string name, string selfId, string targetId)
        {
            if (_actions.ContainsKey(name))
            {
                return BuildSimpleCommand(SimpleCommandType.Action, name, selfId, targetId);
            }

            if (_links.ContainsKey(name))
            {
                return BuildSimpleCommand(SimpleCommandType.Link, name);
            }

            if (_copypastas.ContainsKey(name))
            {
                return BuildSimpleCommand(SimpleCommandType.Copypasta, name);
            }

            return null;
        }

        private string BuildSimpleCommand(SimpleCommandType type, string name, string selfId = null, string targetId = null)
        {
            string command = String.Empty;

            switch (type)
            {
                case SimpleCommandType.Action:
                    if (string.IsNullOrEmpty(targetId) || selfId == targetId)
                        command = string.Format(_actions[name].MessageSelf, "<@" + selfId + ">");
                    else
                        command = string.Format(_actions[name].MessageTarget, "<@" + selfId + ">", "<@" + targetId + ">");
                    break;
                case SimpleCommandType.Link:
                    command = _links[name];
                    break;
                case SimpleCommandType.Copypasta:
                    command = _copypastas[name];
                    break;
            }

            return command;
        }
    }
}
