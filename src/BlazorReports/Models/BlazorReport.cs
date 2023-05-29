namespace BlazorReports.Models;

public class BlazorReport
{
  public required Type Component { get; set; }
  public required Type? Data { get; set; }
  public string? BaseStylesPath { get; set; }
  public string? AssetsPath { get; set; }
}
