namespace BlazorReports.Models;

public class BlazorReportRegistrationOptions
{
  public string? ReportName { get; set; }
  public string? BaseStylesPath { get; set; }
  public string? AssetsPath { get; set; }
  public Dictionary<string, string> Assets { get; set; } = new();
}
