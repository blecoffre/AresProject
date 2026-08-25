namespace Core.Models.Console
{
    public enum ConsoleLogType
    {
        Standard,   // Texte classique de log
        Success,    // [OK] Actions réussies
        Narrative   // Indices de l'histoire (photo de chat, etc.)
    }

    public readonly struct LogEntry
    {
        public string Message { get; }
        public ConsoleLogType Type { get; }

        public LogEntry(string message, ConsoleLogType type)
        {
            Message = message;
            Type = type;
        }
    }
}