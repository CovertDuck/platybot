using System;
using System.Collections.Generic;
using System.Linq;

namespace Platybot.Helpers
{
    internal class DiceRoller
    {
        public string Text { get; set; }
        public string Description { get; private set; }
        public int Result { get; private set; }
        public string Calculation { get; private set; }

        public DiceRoller() { }

        public bool Roll()
        {
            // Need some text to work on
            if (string.IsNullOrWhiteSpace(Text)) return false;

            // Splitting rolls from description
            string[] splitText = Text.Split('(', ')');

            if (splitText.Length == 3)
            {
                Description = splitText[1].ToUpper();
            }
            else
            {
                Description = null;
            }

            // Parse the dices
            string dicesText = string.Concat(splitText[0].Where(c => !char.IsWhiteSpace(c)));

            var dices = new List<Dice>();
            var currentDice = string.Empty;

            bool isFirstChar = true;
            foreach (char c in dicesText)
            {
                if ((c == '+' || c == '-') && !isFirstChar)
                {
                    var dice = new Dice(currentDice);
                    if (!dice.IsValid) return false;

                    dices.Add(dice);
                    currentDice = c.ToString();
                }
                else
                {
                    currentDice += c.ToString();
                }

                isFirstChar = false;
            }

            dices.Add(new Dice(currentDice));

            // 100 dices max
            if (dices.Count > 100) return false;

            // Roll the dices
            Result = 0;
            Calculation = string.Empty;
            var random = new Random();
            foreach (var dice in dices)
            {
                dice.Roll(random);
                Result += dice.Result;
                Calculation += dice.Calculation + (!dice.Equals(dices.Last()) ? ", " : "");
            }
            Calculation = "(" + Calculation + ")";

            return true;
        }
    }
}
