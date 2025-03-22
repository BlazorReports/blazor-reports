using BlazorReports.Models;
using BlazorReports.Services.BrowserServices.Problems;
using OneOf;
using OneOf.Types;
using System.IO.Pipelines;

namespace BlazorReports.Services.BrowserServices;

/// <summary>
/// Service for interacting with the browser
/// </summary>
public interface IBrowserService
{
  /// <summary>
  /// Generates a report using the specified HTML
  /// </summary>
  /// <param name="pipeWriter"> The pipe writer to write the report to </param>
  /// <param name="html"> The HTML to use in the report </param>
  /// <param name="pageSettings"> The page settings to use in the report </param>
  /// <param name="currentReportJavascriptSettings"> The current report javascript settings </param>
  /// <param name="cancellationToken"> The cancellation token </param>
  /// <returns> The result of the operation </returns>
  ValueTask<
    OneOf<
      Success,
      ServerBusyProblem,
      OperationCancelledProblem,
      BrowserProblem,
      JavascriptTimedoutProblem
    >
  > GenerateReport(
    PipeWriter pipeWriter,
    string html,
    BlazorReportsPageSettings pageSettings,
    BlazorReportCurrentReportJavascriptSettings currentReportJavascriptSettings,
    CancellationToken cancellationToken
  );
}
