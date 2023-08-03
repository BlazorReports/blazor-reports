using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using BlazorReports.Models;
using BlazorReports.Services.BrowserServices.Problems;
using BlazorReports.Services.BrowserServices.Requests;
using BlazorReports.Services.BrowserServices.Responses;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;

namespace BlazorReports.Services.BrowserServices;

/// <summary>
/// Represents a browser instance
/// </summary>
internal sealed class Browser(
  Process chromiumProcess,
  DirectoryInfo devToolsActivePortDirectory,
  Connection connection,
  BlazorReportsBrowserOptions browserOptions,
  ILogger logger
) : IAsyncDisposable
{
  private readonly ConcurrentStack<BrowserPage> _browserPagePool = new();
  private int _currentBrowserPagePoolSize;
  private readonly SemaphoreSlim _poolLock = new(1, 1);

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
        var hasPoolLimitReached = result.TryPickT1(out _, out var browserPageOrProblem);
        if (hasPoolLimitReached)
        {
          try
          {
            await Task.Delay(
              browserOptions.ResponseTimeout.Divide(maxRetryCount),
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

        var hasBrowserProblem = browserPageOrProblem.TryPickT1(
          out var browserProblem,
          out browserPage
        );
        if (hasBrowserProblem)
        {
          // Failed to get a browser page
          return browserProblem;
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
        logger.LogError(e, "Failed to generate report");
        return new BrowserProblem();
      }
    }
    finally
    {
      if (browserPage is not null && !browserPagedDisposed)
        ReturnBrowserPage(browserPage);
    }

    return new Success();
  }

  /// <summary>
  /// Gets the browser page
  /// </summary>
  /// <param name="stoppingToken"> The stopping token </param>
  /// <returns> The browser page </returns>
  private async ValueTask<
    OneOf<BrowserPage, PoolLimitReachedProblem, BrowserProblem>
  > GetBrowserPage(CancellationToken stoppingToken = default)
  {
    await _poolLock.WaitAsync(stoppingToken); // Wait for the lock

    try
    {
      if (_browserPagePool.TryPop(out var item))
      {
        return item;
      }

      if (_currentBrowserPagePoolSize >= browserOptions.MaxBrowserPagePoolSize)
      {
        return new PoolLimitReachedProblem();
      }

      try
      {
        return await CreateBrowserPage(stoppingToken);
      }
      catch (Exception e)
      {
        logger.LogError(e, "Failed to create browser page");
        return new BrowserProblem();
      }
    }
    finally
    {
      _poolLock.Release(); // Release the lock
    }
  }

  /// <summary>
  /// Returns the browser page to the pool
  /// </summary>
  /// <param name="browserPage"> The browser page to return </param>
  private void ReturnBrowserPage(BrowserPage browserPage)
  {
    _browserPagePool.Push(browserPage);
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
      await connection.ConnectAsync();
      connection.SendAsync(closeTargetMessage);
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
    await connection.ConnectAsync(stoppingToken);
    return await connection.SendAsync(
      createTargetMessage,
      CreateTargetResponseSerializationContext.Default.BrowserResultResponseCreateTargetResponse,
      async targetResponse =>
      {
        var pageUrl =
          $"{connection.Uri.Scheme}://{connection.Uri.Host}:{connection.Uri.Port}/devtools/page/{targetResponse.Result.TargetId}";
        var browserPage = new BrowserPage(
          targetResponse.Result.TargetId,
          new Uri(pageUrl),
          browserOptions
        );
        await browserPage.InitializeAsync(stoppingToken);
        _currentBrowserPagePoolSize++;
        return browserPage;
      },
      stoppingToken
    );
  }

  /// <summary>
  /// Disposes of the browser
  /// </summary>
  public async ValueTask DisposeAsync()
  {
    logger.LogDebug(
      "Disposing of browser with process id: {ChromiumProcessId}",
      chromiumProcess.Id
    );
    _poolLock.Dispose();
    foreach (var browserPage in _browserPagePool)
      await browserPage.DisposeAsync();
    chromiumProcess.Kill();
    chromiumProcess.Dispose();
    await connection.DisposeAsync();
    if (devToolsActivePortDirectory.Exists)
      Directory.Delete(devToolsActivePortDirectory.FullName, true);
  }
}
