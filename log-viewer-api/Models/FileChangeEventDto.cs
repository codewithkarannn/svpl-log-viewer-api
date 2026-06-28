namespace log_viewer_api.Models;

public class FileChangeEventDto
{
    public string EventType { get; set; } = string.Empty;

    public LogFileDto? File { get; set; }

    public string? OldName { get; set; }
}