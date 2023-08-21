using BlazorReports.Models;
using Microsoft.Extensions.Options;

namespace BlazorReports.Services.BrowserServices.Factories;

internal sealed class BrowserPageFactory(
  IOptions<BlazorReportsOptions> options,
  IConnectionFactory connectionFactory
) : IBrowserPageFactory
{
  /// <summary>
  /// Creates a new browser page
  /// </summary>
  /// <param name="targetId"> The target id </param>
  /// <param name="pageUri"> The page uri </param>
  /// <returns> The browser page </returns>
  public async ValueTask<BrowserPage> CreateBrowserPage(string targetId, Uri pageUri)
  {
    var pageConnection = await connectionFactory.CreateConnection(
      pageUri,
      options.Value.BrowserOptions.ResponseTimeout
    );
    var browserPage = new BrowserPage(targetId, pageConnection);
    return browserPage;
  }
}
