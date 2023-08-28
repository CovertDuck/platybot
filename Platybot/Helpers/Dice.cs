using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Platybot.Helpers
{
    internal class Dice
    {
        private int Multiplier { get; set; }
        private int? Faces { get; set; }
        public bool IsValid { get; private set; }
        public int Result { get; set; }
        public string Calculation { get; set; }

        public Dice(string text)
        {
            IsValid = true;

            string[] splitText = text.ToLower().Split('d');
            if (splitText.Length == 1)
            {
                Multiplier = int.Parse(splitText[0]);
                Faces = null;
            }
            else if (splitText.Length == 2)
            {
                if (splitText[0] == string.Empty || splitText[0] == "+" || splitText[0] == "-")
                {
                    splitText[0] = splitText[0] + "1";
                }

                Multiplier = int.Parse(splitText[0]);
                Faces = int.Parse(splitText[1]);
            }
            else
            {
                IsValid = false;
            }

            // 1 000 000 faces max
            if (Faces > 1000000) IsValid = false;
        }

        public void Roll(Random random)
        {
            if (!IsValid) return;

            if (!Faces.HasValue) // Is a simple multiplier
            {
                Result = Multiplier;
                Calculation = Multiplier.ToString();
            }
            else // Is an actual dice
            {
                Result = 0;
                Calculation = string.Empty;
                for (int i = 0; i < Math.Abs(Multiplier); i++)
                {
                    int diceResult = random.Next(1, (int)Faces + 1) * (Multiplier < 0 ? -1 : 1);
                    Result += diceResult;
                    Calculation += diceResult + (i < Math.Abs(Multiplier) - 1 ? " + " : "");
                }

                if (Multiplier > 1)
                {
                    Calculation += " = " + Result;
                }
            }
        }
    }
}
