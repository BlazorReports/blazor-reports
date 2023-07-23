using System.Collections.Concurrent;
using System.IO.Pipelines;
using BlazorReports.Models;
using BlazorReports.Services.BrowserServices.Problems;
using OneOf;
using OneOf.Types;

namespace BlazorReports.Services.BrowserServices;

/// <summary>
/// Represents a connection to the browser
/// </summary>
internal sealed class BrowserService : IAsyncDisposable
{
  private readonly BlazorReportsBrowserOptions _browserOptions;
  private readonly ConcurrentQueue<Browser> _browserQueue = new();
  private readonly SemaphoreSlim _semaphore;
  private readonly SemaphoreSlim _browserStartLock = new(1, 1);
  private int _currentBrowserPoolSize;

  /// <summary>
  /// The connection to the browser
  /// </summary>
  /// <param name="browserOptions"> The options for the browser </param>
  public BrowserService(BlazorReportsBrowserOptions browserOptions)
  {
    _browserOptions = browserOptions;
    _semaphore = new SemaphoreSlim(0, browserOptions.MaxBrowserPoolSize);
  }

  /// <summary>
  /// Generates a report using the specified HTML
  /// </summary>
  /// <param name="pipeWriter"> The pipe writer to write the report to </param>
  /// <param name="html"> The HTML to use in the report </param>
  /// <param name="pageSettings"> The page settings to use in the report </param>
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
    var browserGetResult = await GetBrowser();
    var hasBrowserPoolLimitReachedProblem = browserGetResult.TryPickT1(
      out _,
      out var browserOrBrowserProblem
    );
    if (hasBrowserPoolLimitReachedProblem)
      return new ServerBusyProblem();

    var hasBrowserStartProblem = browserOrBrowserProblem.TryPickT1(
      out var browserStartProblem,
      out var browser
    );
    if (hasBrowserStartProblem)
      return browserStartProblem;

    return await browser.GenerateReport(pipeWriter, html, pageSettings, cancellationToken);
  }

  /// <summary>
  /// Gets a browser instance
  /// </summary>
  /// <returns> The browser instance </returns>
  private async ValueTask<OneOf<Browser, PoolLimitReachedProblem, BrowserProblem>> GetBrowser()
  {
    while (_currentBrowserPoolSize < _browserOptions.MaxBrowserPoolSize)
    {
      await _browserStartLock.WaitAsync();
      try
      {
        if (_currentBrowserPoolSize >= _browserOptions.MaxBrowserPoolSize)
          break;
        var browser = await Browser.CreateBrowser(_browserOptions);
        _browserQueue.Enqueue(browser);
        _currentBrowserPoolSize++;
        _semaphore.Release();
        return browser;
      }
      finally
      {
        _browserStartLock.Release();
      }
    }

    // This will block if there are no available slots
    await _semaphore.WaitAsync();

    try
    {
      var retryCount = 0;
      while (retryCount < 3)
      {
        _browserQueue.TryDequeue(out var browser);
        if (browser is not null)
        {
          _browserQueue.Enqueue(browser);
          return browser;
        }
        retryCount++;
        await Task.Delay(TimeSpan.FromSeconds(5));
      }

      return new PoolLimitReachedProblem();
    }
    finally
    {
      _semaphore.Release();
    }
  }

  /// <summary>
  /// Disposes of the browser service
  /// </summary>
  public async ValueTask DisposeAsync()
  {
    foreach (var browser in _browserQueue)
      await browser.DisposeAsync();
  }
}
