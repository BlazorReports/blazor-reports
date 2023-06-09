using BlazorReports.Services.Browser;

namespace BlazorReports.Models;

/// <summary>
/// Options for BlazorReports Browser
/// </summary>
public class BlazorReportsBrowserOptions
{
  /// <summary>
  /// The browser to use for generating a PDF. Defaults to Chrome
  /// </summary>
  public Browsers Browser { get; set; } = Browsers.Chrome;
  /// <summary>
  /// The path to the browser executable
  /// </summary>
  public FileInfo? BrowserExecutableLocation { get; set; }
  /// <summary>
  /// Configures the browser to run without a sandbox
  /// </summary>
  public bool NoSandbox { get; set; }
  /// <summary>
  /// Sets the maximum pool size for the browser. Defaults to 10
  /// </summary>
  public int MaxPoolSize { get; set; } = 10;
  /// <summary>
  /// Sets the response timeout for the browser. Defaults to 30 seconds
  /// </summary>
  public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
