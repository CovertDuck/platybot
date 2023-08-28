using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Platybot.Attributes
{

    [AttributeUsage(AttributeTargets.Method)]
    internal class EasterEggAttribute : Attribute
    {
        public string[] Triggers;

        public EasterEggAttribute(string triggers)
        {
            Triggers = triggers.Split(",").Select(x => x.Trim()).ToArray();
        }
    }
}
