using Discord.WebSocket;
using SharpLink;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Platybot.Helpers
{
    internal static class ObfuscationHelper
    {
        public static string Rot13(string input)
        {
            char[] array = input.ToCharArray();
            for (int i = 0; i < array.Length; i++)
            {
                int num = array[i];
                if (num >= 65 && num <= 90)
                {
                    num += ((num > 77) ? -13 : 13);
                }
                else if (num >= 97 && num <= 122)
                {
                    num += ((num > 109) ? -13 : 13);
                }
                array[i] = (char)num;
            }
            return new string(array);
        }
    }
}
