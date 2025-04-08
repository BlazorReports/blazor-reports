namespace BlazorReports.Models;

/// <summary>
/// Settings for the internal javascript api
/// </summary>
public class BlazorReportCurrentReportJavascriptSettings
{
  /// <summary>
  /// The signal that the report is ready
  /// </summary>
  public string ReportIsReadySignal { get; set; } = default!;

  /// <summary>
  /// Decides if the report should wait for the complete signal in the javascript
  /// </summary>
  public bool WaitForJavascriptCompletedSignal { get; set; }

  /// <summary>
  ///  The amount of time a reports javascript can take until it is considered to have timed out
  /// </summary>
  public TimeSpan? WaitForCompletedSignalTimeout { get; set; }

  /// <summary>
  /// The default settings for the current report javascript settings
  /// </summary>
  public static BlazorReportCurrentReportJavascriptSettings Default =>
    new BlazorReportCurrentReportJavascriptSettings
    {
      WaitForJavascriptCompletedSignal = false,
      WaitForCompletedSignalTimeout = TimeSpan.FromSeconds(3),
    };
}
