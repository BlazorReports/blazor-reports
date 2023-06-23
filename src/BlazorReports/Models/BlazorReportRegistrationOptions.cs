namespace BlazorReports.Models;

/// <summary>
///  Options for registering a report.
/// </summary>
public class BlazorReportRegistrationOptions
{
  /// <summary>
  /// The name of the report. This is utilized to generate the route for the report.
  /// </summary>
  public string? ReportName { get; set; }
  /// <summary>
  /// Base styles path for the report.
  /// </summary>
  public string? BaseStylesPath { get; set; }
  /// <summary>
  /// Assets path for the report.
  /// </summary>
  public string? AssetsPath { get; set; }

  /// <summary>
  /// Settings for generating a PDF
  /// </summary>
  public BlazorReportsPageSettings PageSettings { get; set; } = new();
}
