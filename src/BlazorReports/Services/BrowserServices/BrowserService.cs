using System.IO.Pipelines;
using BlazorReports.Models;
using BlazorReports.Services.BrowserServices.Problems;
using OneOf;
using OneOf.Types;

namespace BlazorReports.Services.BrowserServices;

/// <summary>
/// Represents a connection to the browser
/// </summary>
public sealed class BrowserService : IAsyncDisposable
{
  private readonly BlazorReportsBrowserOptions _browserOptions;
  private readonly SemaphoreSlim _browserLock = new(1, 1);
  private Browser? _browser;

  /// <summary>
  /// The connection to the browser
  /// </summary>
  /// <param name="browserOptions"> The options for the browser </param>
  public BrowserService(BlazorReportsBrowserOptions browserOptions)
  {
    _browserOptions = browserOptions;
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
    var hasBrowserStartProblem = browserGetResult.TryPickT1(
      out var browserStartProblem,
      out var browser
    );
    if (hasBrowserStartProblem)
    {
      return browserStartProblem;
    }

    BrowserPage? browserPage = null;
    var browserPagedDisposed = false;

    var retryCount = 0;
    const int maxRetryCount = 3;
    try
    {
      var operationCancelled = false;
      while (browserPage is null)
      {
        var result = await browser.GetBrowserPage(cancellationToken);
        var hasPoolLimitReached = result.TryPickT1(out _, out var browserPageOrProblem);
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
        await browser.DisposeBrowserPage(browserPage);
        browserPagedDisposed = true;
        Console.WriteLine(e);
        return new BrowserProblem();
      }
    }
    finally
    {
      if (browserPage is not null && !browserPagedDisposed)
        browser.ReturnBrowserPage(browserPage);
    }

    return new Success();
  }

  /// <summary>
  /// Gets a browser instance
  /// </summary>
  /// <returns> The browser instance </returns>
  private async ValueTask<OneOf<Browser, BrowserProblem>> GetBrowser()
  {
    // If there's already a connection, no need to start a new browser instance
    if (_browser is not null)
      return _browser;

    // Try to get the lock
    await _browserLock.WaitAsync();

    try
    {
      // Check again to make sure a browser instance wasn't created while waiting for the lock
      if (_browser is not null)
        return _browser;

      _browser = await Browser.CreateBrowser(_browserOptions);
      return _browser;
    }
    catch (Exception)
    {
      // If there was an error, make sure to set the browser to null
      return new BrowserProblem();
    }
    finally
    {
      // Release the lock
      _browserLock.Release();
    }
  }

  /// <summary>
  /// Disposes of the browser service
  /// </summary>
  public async ValueTask DisposeAsync()
  {
    if (_browser != null)
      await _browser.DisposeAsync();
  }
}
