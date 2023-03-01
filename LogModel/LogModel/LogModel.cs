using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogWatcher.Models
{
    [Flags]
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 4,
        Error = 8,
        Critical = 16
    }   
    
    //[Table("Log")]
    public class LogModel
    {
        [Key]
        public int Id { get; set; }
        public string? CustomerId { get; set; }
        public DateTime SentDt { get; set; }
        public string? Module { get; set; }
        public LogLevel Level { get; set; }
        public string? Message { get; set; }
    }
}