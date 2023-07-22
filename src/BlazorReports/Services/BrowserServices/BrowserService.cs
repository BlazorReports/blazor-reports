using BlazorReports.Models;
using BlazorReports.Services.BrowserServices.Problems;
using OneOf;

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
  /// Gets a browser instance
  /// </summary>
  /// <returns> The browser instance </returns>
  public async ValueTask<OneOf<Browser, BrowserProblem>> GetBrowser()
  {
    // If there's already a connection, no need to start a new browser instance
    if (_browser is not null) return _browser;

    // Try to get the lock
    await _browserLock.WaitAsync();

    try
    {
      // Check again to make sure a browser instance wasn't created while waiting for the lock
      if (_browser is not null) return _browser;

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
