using System.Diagnostics;
using System.Runtime.InteropServices;
using BlazorReports.Models;
using BlazorReports.Services.BrowserServices.Helpers;
using Microsoft.Extensions.Logging;

namespace BlazorReports.Services.BrowserServices;

/// <summary>
/// Factory for creating browser instances
/// </summary>
internal sealed class BrowserFactory(
  ILogger<BrowserFactory> browserFactoryLogger,
  ILogger<Browser> browserLogger
) : IBrowserFactory
{
  /// <summary>
  /// Creates a new browser instance
  /// </summary>
  /// <param name="browserOptions"> The browser options </param>
  /// <returns> The browser instance </returns>
  /// <exception cref="FileNotFoundException"> Thrown when the browser executable is not found </exception>
  /// <exception cref="Exception"> Thrown when the browser process could not be started </exception>
  public async ValueTask<Browser> CreateBrowser(BlazorReportsBrowserOptions browserOptions)
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
      browserFactoryLogger.LogError(exception, "Could not start browser process");
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
    return new Browser(
      chromiumProcess,
      devToolsActivePortDirectory,
      connection,
      browserOptions,
      browserLogger
    );
  }

  /// <summary>
  /// Creates a new Chromium process
  /// </summary>
  /// <param name="chromiumExeFileName"> The path to the Chromium executable </param>
  /// <param name="devToolsDirectory"> The directory to store the DevTools files </param>
  /// <param name="browserOptions"> The browser options </param>
  /// <returns> The Chromium process </returns>
  private Process CreateChromiumProcess(
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
  private void ChromiumProcess_Exited(object? sender, EventArgs e)
  {
    // Log errors with details
    if (sender is not Process process)
      return;
    if (process.ExitCode == 0)
      return;

    browserFactoryLogger.LogError(
      "Chromium process exited with code \'{ProcessExitCode}\'",
      process.ExitCode
    );
    var exception = Marshal.GetExceptionForHR(process.ExitCode);
    browserFactoryLogger.LogError(
      exception,
      "Chromium process exited with code \'{ProcessExitCode}\'",
      process.ExitCode
    );
  }

  private static async ValueTask<string[]> ReadDevToolsActiveFile(
    string devToolsActivePortFile,
    DirectoryInfo devToolsActivePortDirectory
  )
  {
    if (devToolsActivePortDirectory is null || !devToolsActivePortDirectory.Exists)
      throw new DirectoryNotFoundException($"The {nameof(devToolsActivePortDirectory)} is null");

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
}
