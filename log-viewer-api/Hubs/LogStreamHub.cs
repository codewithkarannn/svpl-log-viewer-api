using Microsoft.AspNetCore.SignalR;

public class LogStreamHub : Hub
{
    private readonly LogWatcherService _watcher;

    public LogStreamHub(LogWatcherService watcher) => _watcher = watcher;

    public async Task WatchFile(string filePath)
    {
        Console.WriteLine($"WatchFile called: {filePath}");
        var group = LogWatcherService.GroupName(filePath);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        _watcher.Watch(filePath);
    }

    public async Task StopWatch(string filePath)
    {
        var group = LogWatcherService.GroupName(filePath);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        // note: don't Unwatch() here — other clients may still be watching.
        // Use a ref-count or rely on FileSystemWatcher idling harmlessly.
    }

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        // SignalR auto-removes from groups on disconnect, nothing extra needed
        await base.OnDisconnectedAsync(ex);
    }
}