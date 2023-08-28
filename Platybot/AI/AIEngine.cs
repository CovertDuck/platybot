namespace Platybot.AI
{
    internal abstract class AIEngine
    {
        public float Temperature { get; set; } = 0.7f;
        public float TopP { get; set; } = 1f;
    }
}
