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
}
