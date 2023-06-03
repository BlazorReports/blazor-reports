using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BlazorReports.Services.Browser.Helpers;
using BlazorReports.Services.Browser.Requests;
using BlazorReports.Services.Browser.Responses;

namespace BlazorReports.Services.Browser;

/// <summary>
/// Represents a connection to the browser
/// </summary>
public sealed class BrowserService
{
  private Connection? _connection;

  private readonly ConcurrentStack<BrowserPage> _browserPagePool = new();

  internal async Task<BrowserPage> GetBrowserPage(CancellationToken stoppingToken = default)
  {
    if (_connection is null)
      throw new InvalidOperationException("Browser is not running");

    if (_browserPagePool.TryPop(out var item))
    {
      return item;
    }

    var createTargetMessage = new BrowserMessage("Target.createTarget");
    createTargetMessage.Parameters.Add("url", "about:blank");
    // createTargetMessage.Parameters.Add("enableBeginFrameControl", true);
    return await _connection.SendAsync<BrowserResultResponse<CreateTargetResponse>, BrowserPage>(createTargetMessage,
      targetResponse =>
      {
        var pageUrl =
          $"{_connection.Uri.Scheme}://{_connection.Uri.Host}:{_connection.Uri.Port}/devtools/page/{targetResponse.Result.TargetId}";
        return Task.FromResult(new BrowserPage(new Uri(pageUrl)));
      }, stoppingToken);
  }

  internal void ReturnBrowserPage(BrowserPage browserPage)
  {
    _browserPagePool.Push(browserPage);
  }

  internal async Task StartBrowserHeadless(Browsers browsers)
  {
    if (_connection is not null) return;

    var chromiumExeFileName = BrowserFinder.Find(browsers);

    if (!File.Exists(chromiumExeFileName))
      throw new FileNotFoundException($"Could not find browser in location '{chromiumExeFileName}'");

    var currentPath = Directory.GetCurrentDirectory();
    var devToolsDirectory = Path.Combine(currentPath, "browser-dev-tools");
    Directory.CreateDirectory(devToolsDirectory);
    var devToolsActivePortFile = Path.Combine(devToolsDirectory, "DevToolsActivePort");

    if (File.Exists(devToolsActivePortFile))
      File.Delete(devToolsActivePortFile);

    var chromiumProcess = CreateChromiumProcess(chromiumExeFileName, devToolsDirectory);
    try
    {
      var started = chromiumProcess.Start();
      if (!started)
        throw new Exception("Could not start browser process");
    }
    catch (Exception exception)
    {
      Console.WriteLine(exception);
      throw;
    }

    var lines = await ReadDevToolsActiveFile(devToolsActivePortFile);
    var uri = new Uri($"ws://127.0.0.1:{lines[0]}{lines[1]}");
    _connection = new Connection(uri);
  }

  /// <summary>
  /// Creates a new Chromium process
  /// </summary>
  /// <param name="chromiumExeFileName"> The path to the Chromium executable </param>
  /// <param name="devToolsDirectory"> The directory to store the DevTools files </param>
  /// <returns> The Chromium process </returns>
  private static Process CreateChromiumProcess(string chromiumExeFileName, string devToolsDirectory)
  {
    var chromiumProcess = new Process();
    var defaultChromiumArgument = new List<string>
    {
      "--headless",
      "--disable-gpu",
      "--hide-scrollbars",
      "--mute-audio",
      "--disable-background-networking",
      "--disable-background-timer-throttling",
      "--disable-default-apps",
      "--disable-extensions",
      "--disable-hang-monitor",
      "--disable-prompt-on-repost",
      "--disable-sync",
      "--disable-translate",
      "--metrics-recording-only",
      "--no-first-run",
      "--disable-crash-reporter",
      "--remote-debugging-port=\"0\"",
      $"--user-data-dir=\"{devToolsDirectory}\""
    };

    var processStartInfo = new ProcessStartInfo
    {
      FileName = chromiumExeFileName,
      Arguments = string.Join(" ", defaultChromiumArgument),
      CreateNoWindow = true
    };

    chromiumProcess.StartInfo = processStartInfo;
    chromiumProcess.Exited += ChromiumProcess_Exited;
    return chromiumProcess;
  }

  /// <summary>
  /// Raised when the Chromium process exits
  /// </summary>
  /// <param name="sender"> The Chromium process </param>
  /// <param name="e"> The event args </param>
  private static void ChromiumProcess_Exited(object? sender, EventArgs e)
  {
    // Log errors with details
    if (sender is not Process process) return;
    if (process.ExitCode == 0) return;

    Console.WriteLine($"Chromium process exited with code '{process.ExitCode}'");
    var exception = Marshal.GetExceptionForHR(process.ExitCode);
    Console.WriteLine(exception);
  }

  private static async Task<string[]> ReadDevToolsActiveFile(string devToolsActivePortFile)
  {
    var directoryName = Path.GetDirectoryName(devToolsActivePortFile);
    if (directoryName is null)
      throw new Exception($"Could not get directory name from '{devToolsActivePortFile}'");

    var watcher = new FileSystemWatcher
    {
      Path = directoryName,
      Filter = Path.GetFileName(devToolsActivePortFile),
      EnableRaisingEvents = true
    };

    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));  // 10 second timeout
    var tcs = new TaskCompletionSource<string[]>();

    void CreatedHandler(object s, FileSystemEventArgs e)
    {
      if (e.ChangeType != WatcherChangeTypes.Created) return;
      HandleFileCreationAsync(devToolsActivePortFile, tcs, 5).ConfigureAwait(false);
    }

    watcher.Created += CreatedHandler;

    // Register the CancellationToken's callback
    var callback = cts.Token.Register(() =>
    {
      tcs.TrySetException(new TimeoutException($"A timeout of 10 seconds exceeded, the file '{devToolsActivePortFile}' did not exist"));
    });

    // if the file already exists, immediately return the lines
    if (File.Exists(devToolsActivePortFile))
    {
      await callback.DisposeAsync(); // Dispose of callback to remove the createdHandler
      watcher.Dispose();  // Dispose of the watcher
      return await File.ReadAllLinesAsync(devToolsActivePortFile, cts.Token);
    }

    try
    {
      return await tcs.Task;  // Wait for the file to be created or the timeout to occur
    }
    finally
    {
      await callback.DisposeAsync(); // Dispose of callback to remove the createdHandler
      watcher.Dispose();  // Dispose of the watcher
    }
  }

  private static async Task HandleFileCreationAsync(string filePath, TaskCompletionSource<string[]> tcs, int maxRetries)
  {
    var retryCount = 0;
    while (true)
    {
      try
      {
        var lines = await File.ReadAllLinesAsync(filePath);
        tcs.TrySetResult(lines);
        break;
      }
      catch (IOException)
      {
        if (++retryCount == maxRetries)
          tcs.TrySetException(new IOException($"Unable to read file '{filePath}' after {maxRetries} attempts"));
        else
          await Task.Delay(TimeSpan.FromMilliseconds(100 * retryCount));  // Exponential backoff
      }
      catch (Exception ex)
      {
        tcs.TrySetException(ex);
        break;
      }
    }
  }
}
