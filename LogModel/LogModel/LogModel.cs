using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogWatcher.Models
{
    [Flags]
    public enum LogLevel
    {
        Trace = 0,
        Information = 1,
        Warning = 2,
        Error = 4,
        Critical = 8,
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