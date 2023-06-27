namespace BlazorReports.Models;

/// <summary>
/// Options for BlazorReports Browser
/// </summary>
public class BlazorReportsBrowserOptions
{
  /// <summary>
  /// Configures the browser to run without a sandbox
  /// </summary>
  public bool NoSandbox { get; set; }
  /// <summary>
  /// Sets the maximum pool size for the browser. Default is 10
  /// </summary>
  public int MaxPoolSize { get; set; } = 10;
  /// <summary>
  /// Sets the response timeout for the browser. Default is 30 seconds
  /// </summary>
  public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
