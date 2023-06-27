using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using BlazorReports.Models;
using BlazorReports.Services.Browser.Helpers;
using BlazorReports.Services.Browser.Problems;
using BlazorReports.Services.Browser.Requests;
using BlazorReports.Services.Browser.Responses;
using OneOf;
using OneOf.Types;

namespace BlazorReports.Services.Browser;

/// <summary>
/// Represents a connection to the browser
/// </summary>
public sealed class BrowserService : IAsyncDisposable
{
  private readonly Browsers _browser;
  private readonly SemaphoreSlim _browserLock = new(1, 1);
  private readonly BlazorReportsBrowserOptions _browserOptions;
  private Process? _chromiumProcess;
  private Connection? _connection;
  private static DirectoryInfo? _devToolsActivePortDirectory;
  private readonly ConcurrentStack<BrowserPage> _browserPagePool = new();
  private int _currentBrowserPagePoolSize;
  private readonly SemaphoreSlim _poolLock = new(1, 1);

  /// <summary>
  /// The connection to the browser
  /// </summary>
  /// <param name="browser"> The browser to use </param>
  /// <param name="browserOptions"> The options for the browser </param>
  public BrowserService(Browsers browser, BlazorReportsBrowserOptions browserOptions)
  {
    _browser = browser;
    _browserOptions = browserOptions;
  }

  internal async ValueTask<OneOf<Success, ServerBusyProblem>> PrintReportFromBrowser(PipeWriter pipeWriter, string html,
    BlazorReportsPageSettings pageSettings, CancellationToken cancellationToken)
  {
    await StartBrowserHeadless(_browser);
    BrowserPage? browserPage = null;

    var retryCount = 0;
    const int maxRetryCount = 3;
    try
    {
      while (browserPage is null && retryCount < maxRetryCount)
      {
        var result = await GetBrowserPage(cancellationToken);
        result.Switch(
          browserPageResult =>
          {
            browserPage = browserPageResult;
          },
          async poolLimitReached =>
          {
            await Task.Delay(_browserOptions.ResponseTimeout.Divide(maxRetryCount), cancellationToken);
            retryCount++;
          });

        if (retryCount >= maxRetryCount)
        {
          return new ServerBusyProblem();
        }
      }
      if (browserPage is not null)
      {
        await browserPage.DisplayHtml(html, cancellationToken);
        await browserPage.ConvertPageToPdf(pipeWriter, pageSettings, cancellationToken);
      }
    }
    finally
    {
      if (browserPage is not null)
        _browserPagePool.Push(browserPage);
    }

    return new Success();
  }

  private async ValueTask<OneOf<BrowserPage, PoolLimitReachedProblem>> GetBrowserPage(CancellationToken stoppingToken = default)
  {
    if (_connection is null)
      throw new InvalidOperationException("Browser is not running");

    await _poolLock.WaitAsync(stoppingToken); // Wait for the lock

    try
    {
      if (_browserPagePool.TryPop(out var item))
      {
        return item;
      }

      if (_currentBrowserPagePoolSize >= _browserOptions.MaxPoolSize)
      {
        return new PoolLimitReachedProblem();
      }

      var createTargetMessage = new BrowserMessage("Target.createTarget");
      createTargetMessage.Parameters.Add("url", "about:blank");
      // createTargetMessage.Parameters.Add("enableBeginFrameControl", true);
      await _connection.ConnectAsync(stoppingToken);
      return await _connection.SendAsync(
        createTargetMessage, CreateTargetResponseSerializationContext.Default.BrowserResultResponseCreateTargetResponse,
        async targetResponse =>
        {
          var pageUrl =
            $"{_connection.Uri.Scheme}://{_connection.Uri.Host}:{_connection.Uri.Port}/devtools/page/{targetResponse.Result.TargetId}";
          var browserPage = new BrowserPage(new Uri(pageUrl), _browserOptions);
          await browserPage.InitializeAsync(stoppingToken);
          _currentBrowserPagePoolSize++;
          return browserPage;
        }, stoppingToken);

    }
    finally
    {
      _poolLock.Release(); // Release the lock
    }
  }

  private async ValueTask StartBrowserHeadless(Browsers browsers)
  {
    // If there's already a connection, no need to start a new browser instance
    if (_connection is not null) return;

    // Try to get the lock
    await _browserLock.WaitAsync();

    try
    {
      // Check again to make sure a browser instance wasn't created while waiting for the lock
      if (_connection is not null) return;

      var chromiumExeFileName = BrowserFinder.Find(browsers);

      if (!File.Exists(chromiumExeFileName))
        throw new FileNotFoundException($"Could not find browser in location '{chromiumExeFileName}'");

      var temporaryPath = Path.GetTempPath();
      var devToolsDirectory = Path.Combine(temporaryPath, Guid.NewGuid().ToString());
      Directory.CreateDirectory(devToolsDirectory);
      _devToolsActivePortDirectory = new DirectoryInfo(devToolsDirectory);
      var devToolsActivePortFile = Path.Combine(devToolsDirectory, "DevToolsActivePort");

      if (File.Exists(devToolsActivePortFile))
        File.Delete(devToolsActivePortFile);

      _chromiumProcess = CreateChromiumProcess(chromiumExeFileName, devToolsDirectory);
      try
      {
        var started = _chromiumProcess.Start();
        if (!started)
          throw new Exception("Could not start browser process");
      }
      catch (Exception exception)
      {
        Console.WriteLine(exception);
        throw;
      }

      var lines = await ReadDevToolsActiveFile(devToolsActivePortFile);
      if (lines.Length != 2)
      {
        throw new Exception($"Could not read DevToolsActivePort file '{devToolsActivePortFile}'");
      }

      var uri = new Uri($"ws://127.0.0.1:{lines[0]}{lines[1]}");
      var connection = new Connection(uri, _browserOptions.ResponseTimeout);
      await connection.InitializeAsync();
      _connection = connection;
    }
    finally
    {
      // Release the lock
      _browserLock.Release();
    }
  }

  /// <summary>
  /// Creates a new Chromium process
  /// </summary>
  /// <param name="chromiumExeFileName"> The path to the Chromium executable </param>
  /// <param name="devToolsDirectory"> The directory to store the DevTools files </param>
  /// <returns> The Chromium process </returns>
  private Process CreateChromiumProcess(string chromiumExeFileName, string devToolsDirectory)
  {
    var chromiumProcess = new Process();
    var defaultChromiumArgument = new List<string>
    {
      "--headless=new",
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

    if (_browserOptions.NoSandbox)
      defaultChromiumArgument.Add("--no-sandbox");

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

  private static async ValueTask<string[]> ReadDevToolsActiveFile(string devToolsActivePortFile)
  {
    if (_devToolsActivePortDirectory is null || !_devToolsActivePortDirectory.Exists)
      throw new DirectoryNotFoundException($"The {nameof(_devToolsActivePortDirectory)} is null");

    var watcher = new FileSystemWatcher
    {
      Path = _devToolsActivePortDirectory.FullName,
      Filter = Path.GetFileName(devToolsActivePortFile),
      EnableRaisingEvents = true
    };

    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // 10 second timeout
    var tcs = new TaskCompletionSource<string[]>();

    void CreatedHandler(object s, FileSystemEventArgs e)
    {
      if (e.ChangeType != WatcherChangeTypes.Created) return;
      HandleFileCreationAsync(devToolsActivePortFile, tcs, 5, 2).ConfigureAwait(false);
    }

    watcher.Created += CreatedHandler;

    // Register the CancellationToken's callback
    var callback = cts.Token.Register(() =>
      tcs.TrySetException(
        new TimeoutException($"A timeout of 10 seconds exceeded, the file '{devToolsActivePortFile}' did not exist")));

    try
    {
      // if the file already exists, immediately return the lines
      if (File.Exists(devToolsActivePortFile))
      {
        return await File.ReadAllLinesAsync(devToolsActivePortFile, cts.Token);
      }

      return await tcs.Task; // Wait for the file to be created or the timeout to occur
    }
    finally
    {
      await callback.DisposeAsync(); // Dispose of callback to remove the createdHandler
      watcher.Dispose(); // Dispose of the watcher
    }
  }

  private static async Task HandleFileCreationAsync(string filePath, TaskCompletionSource<string[]> tcs, int maxRetries,
    int expectedLines)
  {
    var retryCount = 0;
    while (true)
    {
      try
      {
        if (File.Exists(filePath))
        {
          var lines = await File.ReadAllLinesAsync(filePath);
          if (lines.Length >= expectedLines)
          {
            tcs.TrySetResult(lines);
            break;
          }
        }

        if (++retryCount == maxRetries)
        {
          tcs.TrySetException(
            new IOException(
              $"Unable to read file '{filePath}' with {expectedLines} lines after {maxRetries} attempts"));
          break;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(100 * retryCount)); // Exponential backoff
      }
      catch (IOException)
      {
        if (++retryCount == maxRetries)
          tcs.TrySetException(new IOException($"Unable to read file '{filePath}' after {maxRetries} attempts"));
        else
          await Task.Delay(TimeSpan.FromMilliseconds(100 * retryCount)); // Exponential backoff
      }
      catch (Exception ex)
      {
        tcs.TrySetException(ex);
        break;
      }
    }
  }

  /// <summary>
  /// Disposes of the browser service
  /// </summary>
  public async ValueTask DisposeAsync()
  {
    _chromiumProcess?.Kill();
    _chromiumProcess?.Dispose();
    if (_connection != null)
      await _connection.DisposeAsync();
    foreach (var browserPage in _browserPagePool)
      await browserPage.DisposeAsync();
    if (_devToolsActivePortDirectory is not null && _devToolsActivePortDirectory.Exists)
      Directory.Delete(_devToolsActivePortDirectory.FullName, true);
  }
}
