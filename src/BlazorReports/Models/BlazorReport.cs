namespace BlazorReports.Models;

/// <summary>
///  Represents a Blazor report.
/// </summary>
public class BlazorReport
{
  /// <summary>
  /// The type of the component to render.
  /// </summary>
  public required Type Component { get; set; }
  /// <summary>
  /// The type of the data to pass to the component.
  /// </summary>
  public required Type? Data { get; set; }
  /// <summary>
  /// Base styles path to use for the report.
  /// </summary>
  public string? BaseStylesPath { get; set; }
  /// <summary>
  /// Assets path to use for the report.
  /// </summary>
  public string? AssetsPath { get; set; }
}
