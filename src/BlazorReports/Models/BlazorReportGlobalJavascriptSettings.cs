using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazorReports.Models;
/// <summary>
/// 
/// </summary>
public class BlazorReportGlobalJavascriptSettings
{
  /// <summary>
  /// The signal that the report is ready
  /// </summary>
  public string ReportIsReadySignal { get; set; } = "reportIsReady";
  /// <summary>
  /// Decides if the report should wait for the complete signal in the javascript.
  /// (Default: false)
  /// </summary>
  public bool WaitForJavascriptCompletedSignal { get; set; }
  /// <summary>
  ///  The amount of time a reports javascript can take until it is considered to have timed out
  ///  (Default: null)
  /// </summary>
  public TimeSpan WaitForCompletedSignalTimeout { get; set; } = TimeSpan.FromSeconds(3);
}
