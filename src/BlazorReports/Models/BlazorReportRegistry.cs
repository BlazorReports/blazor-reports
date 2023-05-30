using Microsoft.Extensions.Options;

namespace BlazorReports.Models;

/// <summary>
/// The BlazorReportRegistry is a singleton that holds all of the BlazorReport objects.
/// </summary>
public class BlazorReportRegistry
{
  /// <summary>
  /// The BlazorReportRegistry is a singleton that holds all of the BlazorReport objects.
  /// </summary>
  /// <param name="options"> The BlazorReportsOptions object that contains the configuration for the BlazorReportRegistry. </param>
  public BlazorReportRegistry(IOptions<BlazorReportsOptions> options)
  {
    if (!string.IsNullOrWhiteSpace(options.Value.BaseStylesPath))
    {
      BaseStyles = File.ReadAllText(options.Value.BaseStylesPath);
    }
    if (!string.IsNullOrWhiteSpace(options.Value.AssetsPath))
    {
      var assetsPath = options.Value.AssetsPath;
      var assetsDirectory = new DirectoryInfo(assetsPath);
      if (assetsDirectory.Exists)
      {
        foreach (var file in assetsDirectory.GetFiles())
        {
          var fileBytes = File.ReadAllBytes(file.FullName);
          var base64Uri = $"data:image/webp;base64,{Convert.ToBase64String(fileBytes)}";
          GlobalAssets.Add(file.Name, base64Uri);
        }
      }
    }
  }
  /// <summary>
  /// The base styles for the BlazorReportRegistry.
  /// </summary>
  public string BaseStyles { get; set; } = string.Empty;
  /// <summary>
  /// The global assets for the BlazorReportRegistry.
  /// </summary>
  public Dictionary<string, string> GlobalAssets { get; set; } = new();
  /// <summary>
  /// The BlazorReport objects for the BlazorReportRegistry.
  /// </summary>
  public Dictionary<string, BlazorReport> Reports { get; set; } = new();
}
