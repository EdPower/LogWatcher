using System.ComponentModel.DataAnnotations;

namespace LogWatcher.Models
{
    [Flags]
    public enum LogLevel
    {
        Trace = 0,
        Information = 1,
        Warning = 2,
        Error = 4
    }

    public record LogModel
    {
        public int Id { get; set; }
        public string? CustomerId { get; set; }
        public DateTime SentDt { get; set; }
        public string? Module { get; set; }
        public LogLevel Level { get; set; }
        public string? Message { get; set; }
    }
}