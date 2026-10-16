namespace Core.Models.Console
{
    public enum ConsoleLogType
    {
        Standard,   // Texte classique de log
        Success,    // [OK] Actions réussies
        Narrative,  // Indices de l'histoire (photo de chat, etc.)
        Critical    // [CRITICAL BYPASS] Overclock critique (2026-10-16)
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