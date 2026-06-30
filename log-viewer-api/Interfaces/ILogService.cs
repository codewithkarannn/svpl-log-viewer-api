using log_viewer_api.Models;

namespace log_viewer_api.Interfaces;


public interface ILogFileService
{
    List<LogFileDto> GetLogFiles(string directoryPath);
    Task<List<LogEntryDto>> ReadFile(string filePath);
    List<LogEntryDto> Parse(string text, int startId = 0);
}