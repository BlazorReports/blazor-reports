using BlazorReports.Services.Browser;

namespace BlazorReports.Models;

/// <summary>
/// Options for BlazorReports
/// </summary>
public class BlazorReportsOptions
{
  /// <summary>
  /// The path to the base styles file to use in this report
  /// </summary>
  public string? BaseStylesPath { get; set; }
  /// <summary>
  /// The path to the assets folder to use in this report
  /// </summary>
  public string? AssetsPath { get; set; }

  /// <summary>
  /// The browser to use for generating a PDF
  /// </summary>
  public Browsers Browser { get; set; } = Browsers.Chrome;

  /// <summary>
  /// The path to the browser executable
  /// </summary>
  public FileInfo? BrowserExecutablePath { get; set; }
  /// <summary>
  /// Settings for generating a PDF
  /// </summary>
  public BlazorReportsPageSettings? PageSettings { get; set; }
}
