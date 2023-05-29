using Microsoft.Extensions.Options;

namespace BlazorReports.Models;

public class BlazorReportRegistry
{
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
  public string BaseStyles { get; set; } = string.Empty;
  public Dictionary<string, string> GlobalAssets { get; set; } = new();
  public Dictionary<string, BlazorReport> Reports { get; set; } = new();
}
