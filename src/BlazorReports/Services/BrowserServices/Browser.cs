using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using BlazorReports.Models;
using BlazorReports.Services.BrowserServices.Helpers;
using BlazorReports.Services.BrowserServices.Problems;
using BlazorReports.Services.BrowserServices.Requests;
using BlazorReports.Services.BrowserServices.Responses;
using OneOf;
using OneOf.Types;

namespace BlazorReports.Services.BrowserServices;

/// <summary>
/// Represents a browser instance
/// </summary>
public class Browser : IAsyncDisposable
{
  private readonly DirectoryInfo _devToolsActivePortDirectory;
  private readonly Process _chromiumProcess;
  private readonly Connection _connection;
  private readonly BlazorReportsBrowserOptions _browserOptions;
  private readonly ConcurrentStack<BrowserPage> _browserPagePool = new();
  private int _currentBrowserPagePoolSize;
  private readonly SemaphoreSlim _poolLock = new(1, 1);

  private Browser(
    Process chromiumProcess,
    DirectoryInfo devToolsActivePortDirectory,
    Connection connection,
    BlazorReportsBrowserOptions browserOptions
  )
  {
    _chromiumProcess = chromiumProcess;
    _devToolsActivePortDirectory = devToolsActivePortDirectory;
    _connection = connection;
    _browserOptions = browserOptions;
  }

  /// <summary>
  /// Prints the report from the browser
  /// </summary>
  /// <param name="pipeWriter"> The pipe writer </param>
  /// <param name="html"> The html string to convert to a report </param>
  /// <param name="pageSettings"> The page settings </param>
  /// <param name="cancellationToken"> The cancellation token </param>
  /// <returns> The result of the operation </returns>
  public async ValueTask<
    OneOf<Success, ServerBusyProblem, OperationCancelledProblem, BrowserProblem>
  > GenerateReport(
    PipeWriter pipeWriter,
    string html,
    BlazorReportsPageSettings pageSettings,
    CancellationToken cancellationToken
  )
  {
    BrowserPage? browserPage = null;
    var browserPagedDisposed = false;

    var retryCount = 0;
    const int maxRetryCount = 3;
    try
    {
      var operationCancelled = false;
      while (browserPage is null)
      {
        var result = await GetBrowserPage(cancellationToken);
        var hasPoolLimitReached = result.TryPickT1(out _, out browserPage);
        if (hasPoolLimitReached)
        {
          try
          {
            await Task.Delay(
              _browserOptions.ResponseTimeout.Divide(maxRetryCount),
              cancellationToken
            );
          }
          catch (TaskCanceledException)
          {
            operationCancelled = true;
          }
          finally
          {
            retryCount++;
          }
        }

        if (operationCancelled)
          return new OperationCancelledProblem();

        if (retryCount >= maxRetryCount)
        {
          return new ServerBusyProblem();
        }
      }

      try
      {
        await browserPage.DisplayHtml(html, cancellationToken);
        await browserPage.ConvertPageToPdf(pipeWriter, pageSettings, cancellationToken);
      }
      catch (Exception e)
      {
        await DisposeBrowserPage(browserPage);
        browserPagedDisposed = true;
        Console.WriteLine(e);
        return new BrowserProblem();
      }
    }
    finally
    {
      if (browserPage is not null && !browserPagedDisposed)
        _browserPagePool.Push(browserPage);
    }

    return new Success();
  }

  /// <summary>
  /// Gets the browser page
  /// </summary>
  /// <param name="stoppingToken"> The stopping token </param>
  /// <returns> The browser page </returns>
  private async ValueTask<OneOf<BrowserPage, PoolLimitReachedProblem>> GetBrowserPage(
    CancellationToken stoppingToken = default
  )
  {
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

      return await CreateBrowserPage(stoppingToken);
    }
    finally
    {
      _poolLock.Release(); // Release the lock
    }
  }

  private async ValueTask DisposeBrowserPage(BrowserPage browserPage)
  {
    await _poolLock.WaitAsync();

    try
    {
      if (_browserPagePool.Contains(browserPage))
        return;
      await browserPage.DisposeAsync();
      _currentBrowserPagePoolSize--;

      var closeTargetMessage = new BrowserMessage("Target.closeTarget");
      closeTargetMessage.Parameters.Add("targetId", browserPage.TargetId);
      await _connection.ConnectAsync();
      _connection.SendAsync(closeTargetMessage);
    }
    finally
    {
      _poolLock.Release();
    }
  }

  /// <summary>
  /// Creates a new browser page
  /// </summary>
  /// <param name="stoppingToken"> The stopping token </param>
  /// <returns> The browser page </returns>
  private async ValueTask<BrowserPage> CreateBrowserPage(CancellationToken stoppingToken = default)
  {
    var createTargetMessage = new BrowserMessage("Target.createTarget");
    createTargetMessage.Parameters.Add("url", "about:blank");
    // createTargetMessage.Parameters.Add("enableBeginFrameControl", true);
    await _connection.ConnectAsync(stoppingToken);
    return await _connection.SendAsync(
      createTargetMessage,
      CreateTargetResponseSerializationContext.Default.BrowserResultResponseCreateTargetResponse,
      async targetResponse =>
      {
        var pageUrl =
          $"{_connection.Uri.Scheme}://{_connection.Uri.Host}:{_connection.Uri.Port}/devtools/page/{targetResponse.Result.TargetId}";
        var browserPage = new BrowserPage(targetResponse.Result.TargetId, new Uri(pageUrl), _browserOptions);
        await browserPage.InitializeAsync(stoppingToken);
        _currentBrowserPagePoolSize++;
        return browserPage;
      },
      stoppingToken
    );
  }

  /// <summary>
  /// Creates a new browser instance
  /// </summary>
  /// <param name="browserOptions"> The browser options </param>
  /// <returns> The browser instance </returns>
  /// <exception cref="FileNotFoundException"> Thrown when the browser executable is not found </exception>
  /// <exception cref="Exception"> Thrown when the browser process could not be started </exception>
  public static async ValueTask<Browser> CreateBrowser(BlazorReportsBrowserOptions browserOptions)
  {
    var browserExecutableLocation = browserOptions.BrowserExecutableLocation is not null
      ? browserOptions.BrowserExecutableLocation.FullName
      : BrowserFinder.Find(browserOptions.Browser);

    if (!File.Exists(browserExecutableLocation))
      throw new FileNotFoundException(
        $"Could not find browser in location '{browserExecutableLocation}'"
      );

    var temporaryPath = Path.GetTempPath();
    var devToolsDirectory = Path.Combine(temporaryPath, Guid.NewGuid().ToString());
    Directory.CreateDirectory(devToolsDirectory);
    var devToolsActivePortDirectory = new DirectoryInfo(devToolsDirectory);
    var devToolsActivePortFile = Path.Combine(devToolsDirectory, "DevToolsActivePort");

    if (File.Exists(devToolsActivePortFile))
      File.Delete(devToolsActivePortFile);

    var chromiumProcess = CreateChromiumProcess(
      browserExecutableLocation,
      devToolsDirectory,
      browserOptions
    );
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

    var lines = await ReadDevToolsActiveFile(devToolsActivePortFile, devToolsActivePortDirectory);
    if (lines.Length != 2)
    {
      throw new Exception($"Could not read DevToolsActivePort file '{devToolsActivePortFile}'");
    }

    var uri = new Uri($"ws://127.0.0.1:{lines[0]}{lines[1]}");
    var connection = new Connection(uri, browserOptions.ResponseTimeout);
    await connection.InitializeAsync();
    return new Browser(chromiumProcess, devToolsActivePortDirectory, connection, browserOptions);
  }

  /// <summary>
  /// Creates a new Chromium process
  /// </summary>
  /// <param name="chromiumExeFileName"> The path to the Chromium executable </param>
  /// <param name="devToolsDirectory"> The directory to store the DevTools files </param>
  /// <param name="browserOptions"> The browser options </param>
  /// <returns> The Chromium process </returns>
  private static Process CreateChromiumProcess(
    string chromiumExeFileName,
    string devToolsDirectory,
    BlazorReportsBrowserOptions browserOptions
  )
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

    if (browserOptions.NoSandbox)
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
    if (sender is not Process process)
      return;
    if (process.ExitCode == 0)
      return;

    Console.WriteLine($"Chromium process exited with code '{process.ExitCode}'");
    var exception = Marshal.GetExceptionForHR(process.ExitCode);
    Console.WriteLine(exception);
  }

  private static async ValueTask<string[]> ReadDevToolsActiveFile(
    string devToolsActivePortFile,
    DirectoryInfo devToolsActivePortDirectory
  )
  {
    if (devToolsActivePortDirectory is null || !devToolsActivePortDirectory.Exists)
      throw new DirectoryNotFoundException($"The {nameof(_devToolsActivePortDirectory)} is null");

    var watcher = new FileSystemWatcher
    {
      Path = devToolsActivePortDirectory.FullName,
      Filter = Path.GetFileName(devToolsActivePortFile),
      EnableRaisingEvents = true
    };

    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // 10 second timeout
    var tcs = new TaskCompletionSource<string[]>();

    void CreatedHandler(object s, FileSystemEventArgs e)
    {
      if (e.ChangeType != WatcherChangeTypes.Created)
        return;
      HandleFileCreationAsync(devToolsActivePortFile, tcs, 5, 2).ConfigureAwait(false);
    }

    watcher.Created += CreatedHandler;

    // Register the CancellationToken's callback
    var callback = cts.Token.Register(
      () =>
        tcs.TrySetException(
          new TimeoutException(
            $"A timeout of 10 seconds exceeded, the file '{devToolsActivePortFile}' did not exist"
          )
        )
    );

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

  private static async Task HandleFileCreationAsync(
    string filePath,
    TaskCompletionSource<string[]> tcs,
    int maxRetries,
    int expectedLines
  )
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
              $"Unable to read file '{filePath}' with {expectedLines} lines after {maxRetries} attempts"
            )
          );
          break;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(100 * retryCount)); // Exponential backoff
      }
      catch (IOException)
      {
        if (++retryCount == maxRetries)
          tcs.TrySetException(
            new IOException($"Unable to read file '{filePath}' after {maxRetries} attempts")
          );
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
  /// Disposes of the browser
  /// </summary>
  public async ValueTask DisposeAsync()
  {
    foreach (var browserPage in _browserPagePool)
      await browserPage.DisposeAsync();
    _chromiumProcess.Kill();
    _chromiumProcess.Dispose();
    await _connection.DisposeAsync();
    if (_devToolsActivePortDirectory.Exists)
      Directory.Delete(_devToolsActivePortDirectory.FullName, true);
    GC.SuppressFinalize(this);
  }
}
