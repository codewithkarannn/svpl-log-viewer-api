using System.Collections.Concurrent;
using System.Threading.Channels;
using log_viewer_api.Interfaces;
using log_viewer_api.Models;
using log_viewer_api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;

public class LogWatcherService : IDisposable
{
    private readonly IHubContext<LogStreamHub> _hub;
    private readonly ILogFileService _parser;
    private readonly ILogger<LogWatcherService> _log;

    private readonly ConcurrentDictionary<string, WatchedFile> _files = new();

    private class WatchedFile
    {
        public required FileSystemWatcher Watcher { get; init; }
        public required Channel<bool> SignalChannel { get; init; }
        public long Offset;
        public int SeqId;
        public CancellationTokenSource Cts { get; } = new();
    }

    public LogWatcherService(
        IHubContext<LogStreamHub> hub,
        ILogFileService parser,
        ILogger<LogWatcherService> log)
    {
        _hub = hub;
        _parser = parser;
        _log = log;
    }

    public void Watch(string filePath)
    {
        Console.WriteLine($"Starting watcher for {filePath}");
        if (_files.ContainsKey(filePath)) return;   // already watching

        var dir  = Path.GetDirectoryName(filePath)!;
        var name = Path.GetFileName(filePath);

        // bounded channel — if consumer falls behind, drop oldest signal
        // (we re-read from offset anyway, so dropped *signals* are fine —
        //  we never drop *log lines*, just redundant "something changed" pings)
        var channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        var watcher = new FileSystemWatcher(dir, name)
        {
            NotifyFilter = NotifyFilters.LastWrite 
                           | NotifyFilters.Size 
                           | NotifyFilters.LastAccess
                           | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        Console.WriteLine($"Watching dir: '{dir}'");
        Console.WriteLine($"Watching file: '{name}'");
        Console.WriteLine($"Dir exists: {Directory.Exists(dir)}");
        Console.WriteLine($"File exists: {File.Exists(filePath)}");
        Console.WriteLine($"Watcher enabled: {watcher.EnableRaisingEvents}");

        var entry = new WatchedFile
        {
            Watcher = watcher,
            SignalChannel = channel,
            Offset = new FileInfo(filePath).Length, // tail mode — start at EOF
        };

        watcher.Changed += (_, _) =>
        {
            Console.WriteLine("FILE CHANGED");
            channel.Writer.TryWrite(true);
        };
            
        watcher.Error   += (_, e) => _log.LogError(e.GetException(), "Watcher error on {File}", filePath);

        _files[filePath] = entry;

        // background consumer: debounces signals, reads new bytes, broadcasts
        _ = ConsumeAsync(filePath, entry);
    }

    private async Task ConsumeAsync(string filePath, WatchedFile wf)
    {
        var debounceMs = 200;
        var pending = false;

        try
        {
            await foreach (var _ in wf.SignalChannel.Reader.ReadAllAsync(wf.Cts.Token))
            {
                if (pending) continue;
                pending = true;

                // debounce window — coalesce rapid successive writes
                await Task.Delay(debounceMs, wf.Cts.Token);
                pending = false;

                await FlushNewLinesAsync(filePath, wf);
            }
        }
        catch (OperationCanceledException) { /* stopped intentionally */ }
        catch (Exception ex)
        {
            _log.LogError(ex, "Watcher consumer crashed for {File}", filePath);
        }
    }

    private async Task FlushNewLinesAsync(string filePath, WatchedFile wf)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists || fileInfo.Length <= wf.Offset) return;

            await using var fs = new FileStream(
                filePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            fs.Seek(wf.Offset, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);
            var chunk = await reader.ReadToEndAsync();
            wf.Offset = fs.Length;

            var entries = _parser.Parse(chunk);
            foreach (var e in entries)
                e.Id = Interlocked.Increment(ref wf.SeqId);

            if (entries.Count == 0) return;

            // batch send — one SignalR message for N lines, not N messages
            var group = GroupName(filePath);
            await _hub.Clients.Group(group).SendAsync("NewEntries", new
            {
                file = filePath,
                entries,
                latestOffset = wf.Offset,
            });
        }
        catch (IOException ex)
        {
            // file locked / rotated mid-read — retry next signal
            _log.LogWarning(ex, "Transient read failure on {File}, will retry", filePath);
        }
    }

    public void Unwatch(string filePath)
    {
        if (_files.TryRemove(filePath, out var wf))
        {
            wf.Cts.Cancel();
            wf.Watcher.Dispose();
        }
    }

    // REST resync endpoint uses this to catch a client up after reconnect
    public async Task<List<LogEntryDto>> ReadFromOffsetAsync(string filePath, long offset)
    {
        await using var fs = new FileStream(
            filePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        if (offset > fs.Length) offset = 0;   // file truncated/rotated — full reread
        fs.Seek(offset, SeekOrigin.Begin);
        using var reader = new StreamReader(fs);
        var text = await reader.ReadToEndAsync();
        return _parser.Parse(text);
    }

    public static string GroupName(string filePath) =>
        $"file:{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(filePath))}";

    public void Dispose()
    {
        foreach (var wf in _files.Values)
        {
            wf.Cts.Cancel();
            wf.Watcher.Dispose();
        }
    }
}