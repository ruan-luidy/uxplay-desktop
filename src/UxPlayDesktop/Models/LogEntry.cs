using System;

namespace UxPlayDesktop.Models;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public class LogEntry
{
    public DateTime Timestamp { get; } = DateTime.Now;
    public LogLevel Level { get; set; } = LogLevel.Info;
    public string Message { get; set; } = string.Empty;

    public string FormattedTime => Timestamp.ToString("HH:mm:ss.fff");

    public Tone Tone => Level switch
    {
        LogLevel.Info => Tone.Accent,
        LogLevel.Warning => Tone.Warning,
        LogLevel.Error => Tone.Danger,
        _ => Tone.Neutral
    };
}
