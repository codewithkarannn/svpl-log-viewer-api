using log_viewer_api.Hubs;
using log_viewer_api.Models;
using Microsoft.AspNetCore.SignalR;

namespace log_viewer_api.Services;

public class LogWatcherService : BackgroundService
{
    private readonly IHubContext<LogHub> _hub;
    private readonly IConfiguration _configuration;

    public LogWatcherService(
        IHubContext<LogHub> hub,
        IConfiguration configuration)
    {
        _hub = hub;
        _configuration = configuration;
    }
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var path = _configuration["LogSettings:RootDirectory"];

        var watcher = new FileSystemWatcher(path!)
        {
            Filter = "*.*",
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };

        watcher.Created += async (_, e) =>
        {
            var info = new FileInfo(e.FullPath);

            var message = new FileChangeEventDto
            {
                EventType = "Created",
                File =  new LogFileDto
                {
                  FullPath   =  info.FullName,
                  LastModified = info.LastWriteTime,
                  Name = info.Name,
                  Size = info.Length
                }
               
            };

            await _hub.Clients.All.SendAsync("FileChanged", message);
        };

        watcher.Deleted += async (_, e) =>
        {
            Console.WriteLine($"Deleted: {e.FullPath}");

            var message = new FileChangeEventDto
            {
                EventType = "Deleted",
                File =  new LogFileDto
                {
                    Name = Path.GetFileName(e.FullPath)
                }

            };

            await _hub.Clients.All.SendAsync("FileChanged", message);
        };

        watcher.Renamed += async (_, e) =>
        {
            var info = new FileInfo(e.FullPath);
            var message = new FileChangeEventDto
            {
                EventType = "Renamed",
                File =  new LogFileDto
                {
                    FullPath   =  info.FullName,
                    LastModified = info.LastWriteTime,
                    Name = info.Name,
                    Size = info.Length
                },
                OldName =  e.OldName
                
            };
            await _hub.Clients.All.SendAsync(
                "FileChanged",
                message,
                stoppingToken);
        };

        return Task.CompletedTask;
    }
    
}