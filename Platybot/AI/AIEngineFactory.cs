namespace Platybot.AI
{
    internal class AIEngineFactory
    {
        public static AIEngine GetEngine(AIEngineType engineType)
        {
            switch (engineType)
            {
                case AIEngineType.text_davinci_003:
                    return new TextDavinci003();
                case AIEngineType.GPT_3:
                    return new GPT3();
                case AIEngineType.GPT_35:
                    return new GPT35();
                case AIEngineType.GPT_4:
                    return new GPT4();
                default:
                    return new TextDavinci003();
            }
        }
    }
}
