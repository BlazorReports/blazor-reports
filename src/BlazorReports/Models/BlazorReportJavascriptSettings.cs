using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazorReports.Models;

/// <summary>
/// Settings for the internal javascript api
/// </summary>
public class BlazorReportJavascriptSettings
{
  /// <summary>
  /// The signal that the report is ready
  /// </summary>
  public string ReportIsReadySignal { get; set; } = "reportIsReady";

  /// <summary>
  ///  The amount of time a reports javascript can take until it is considered to have timed out
  /// </summary>
  public TimeSpan ReportTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
