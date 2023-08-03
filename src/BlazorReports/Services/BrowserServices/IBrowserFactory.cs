using BlazorReports.Models;

namespace BlazorReports.Services.BrowserServices;

/// <summary>
/// Factory for creating browser instances
/// </summary>
internal interface IBrowserFactory
{
  /// <summary>
  /// Creates a new browser instance
  /// </summary>
  /// <param name="browserOptions"></param>
  /// <returns> The browser instance </returns>
  ValueTask<Browser> CreateBrowser(BlazorReportsBrowserOptions browserOptions);
}
