namespace log_viewer_api.Models;

public class LogFileDto
{
    public string Name { get; set; } = string.Empty;

    public string FullPath { get; set; } = string.Empty;

    public long Size { get; set; }

    public DateTime LastModified { get; set; }
}


public class LogEntryDto {
    public int Id { get; set; }
   
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } =  string.Empty;
    public string Source { get; set; }= string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Raw { get; set; }

}
