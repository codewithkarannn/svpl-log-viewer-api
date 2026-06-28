using System.Text;
using System.Text.RegularExpressions;
using log_viewer_api.Interfaces;
using log_viewer_api.Models;

namespace log_viewer_api.Services;

public class LogFileService : ILogFileService
{
    private static readonly HashSet<string> ValidLevels =
    [
        "INFO",
        "DEBUG",
        "WARN",
        "WARNING",
        "ERROR",
        "TRACE",
        "FATAL"
    ];
    private static readonly Regex LogPattern = new(
        @"^(\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}\.\d+)\s*\|\s*(INFO|DEBUG|WARN|WARNING|ERROR|TRACE|FATAL)\s*\|\s*([^|]+?)\s*\|\s*(.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    public  List<LogFileDto> GetLogFiles(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException();

        return Directory
            .GetFiles(directoryPath, "*.txt", SearchOption.AllDirectories)
            .Select(file =>
            {
                var info = new FileInfo(file);

                return new LogFileDto
                {
                    Name = info.Name,
                    FullPath = info.FullName,
                    Size = info.Length,
                    LastModified = info.LastWriteTime
                };
            })
            .OrderByDescending(x => x.LastModified)
            .ToList();
    }

    public async Task<List<LogEntryDto>> ReadFile(string filePath)
    {
        
        using var reader = new StreamReader(filePath);

        var builder = new StringBuilder();

        while (!reader.EndOfStream)
        {
            builder.AppendLine(await reader.ReadLineAsync());
        }

        return Parse(builder.ToString());
    }


    public List<LogEntryDto> Parse(string text, int startId = 0)
    {
        var entries = new List<LogEntryDto>();

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var id = startId;

        // Cross-platform timezone ID
        string timeZoneId = OperatingSystem.IsWindows()
            ? "India Standard Time"
            : "Asia/Kolkata";

        var indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');

            var match = LogPattern.Match(line);

            if (match.Success)
            {
                var level = match.Groups[2].Value
                    .Trim()
                    .ToUpperInvariant();

                // Parse UTC timestamp
                var utcTimestamp = DateTime.SpecifyKind(
                    DateTime.Parse(match.Groups[1].Value.Trim()),
                    DateTimeKind.Utc);

                // Convert to IST
                var localTimestamp = TimeZoneInfo.ConvertTimeFromUtc(
                    utcTimestamp,
                    indiaTimeZone);

                entries.Add(new LogEntryDto
                {
                    Id = id++,
                    Timestamp = localTimestamp,
                    Level = ValidLevels.Contains(level)
                        ? level
                        : "INFO",
                    Source = match.Groups[3].Value.Trim(),
                    Message = match.Groups[4].Value.Trim(),
                    Raw = line
                });
            }
            else if (entries.Count > 0)
            {
                entries[^1].Message += Environment.NewLine + line;
                entries[^1].Raw += Environment.NewLine + line;
            }
        }

        return entries
            .OrderByDescending(x => x.Timestamp)
            .ToList();
    }

}