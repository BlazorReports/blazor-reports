using BlazorReports.Enums;
using ChromiumHtmlToPdfLib.Settings;
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

    if (options.Value.PageSettings is not null)
    {
      DefaultPageSettings = ConvertBlazorPageSettingsForPrint(options.Value.PageSettings);
    }
  }

  /// <summary>
  /// The default page settings for the BlazorReports
  /// </summary>
  public PageSettings DefaultPageSettings { get; set; } = new();

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
  public Dictionary<string, BlazorReport> Reports { get; } = new();

  /// <summary>
  /// Adds a report to the BlazorReportRegistry.
  /// </summary>
  /// <param name="options"> The options to use when adding the report. </param>
  /// <typeparam name="T"> The type of the report to add. </typeparam>
  /// <returns> The BlazorReport that was added. </returns>
  /// <exception cref="InvalidOperationException"> Thrown when a report with the same name already exists. </exception>
  public BlazorReport AddReport<T>(BlazorReportRegistrationOptions? options = null)
  {
    var reportNameToUse = options?.ReportName ?? typeof(T).Name;
    var normalizedReportName = reportNameToUse.ToLowerInvariant().Trim();

    if (Reports.ContainsKey(normalizedReportName))
      throw new InvalidOperationException($"Report with name {normalizedReportName} already exists");
    var blazorReport = new BlazorReport
    {
      Name = reportNameToUse,
      NormalizedName = normalizedReportName,
      Component = typeof(T),
      Data = null,
      BaseStylesPath = options?.BaseStylesPath ?? string.Empty,
      AssetsPath = options?.AssetsPath ?? string.Empty,
      PageSettings = options?.PdfSettings is not null ? ConvertBlazorPageSettingsForPrint(options.PdfSettings) : null
    };
    Reports.Add(normalizedReportName, blazorReport);
    return blazorReport;
  }

  /// <summary>
  /// Adds a report to the BlazorReportRegistry.
  /// </summary>
  /// <param name="options"> The options to use when adding the report. </param>
  /// <typeparam name="T"> The type of the report to add. </typeparam>
  /// <typeparam name="TD"> The type of the data to use for the report. </typeparam>
  /// <returns> The BlazorReport that was added. </returns>
  /// <exception cref="InvalidOperationException"> Thrown when a report with the same name already exists. </exception>
  public BlazorReport AddReport<T, TD>(BlazorReportRegistrationOptions? options = null)
  {
    var reportNameToUse = options?.ReportName ?? typeof(T).Name;
    var normalizedReportName = reportNameToUse.ToLowerInvariant().Trim();

    if (Reports.ContainsKey(normalizedReportName))
      throw new InvalidOperationException($"Report with name {normalizedReportName} already exists");
    var blazorReport = new BlazorReport
    {
      Name = reportNameToUse,
      NormalizedName = normalizedReportName,
      Component = typeof(T),
      Data = typeof(TD),
      BaseStylesPath = options?.BaseStylesPath ?? string.Empty,
      AssetsPath = options?.AssetsPath ?? string.Empty,
      PageSettings = options?.PdfSettings is not null ? ConvertBlazorPageSettingsForPrint(options.PdfSettings) : null
    };
    Reports.Add(normalizedReportName, blazorReport);
    return blazorReport;
  }

  private static PageSettings ConvertBlazorPageSettingsForPrint(BlazorReportsPageSettings pageSettings)
  {
    var convertedPageSettings = new PageSettings
    {
      Landscape = pageSettings.Orientation == BlazorReportsPageOrientation.Landscape,
      MarginTop = pageSettings.MarginTop,
      MarginBottom = pageSettings.MarginBottom,
      MarginLeft = pageSettings.MarginLeft,
      MarginRight = pageSettings.MarginRight,
      PaperHeight = pageSettings.PaperHeight,
      PaperWidth = pageSettings.PaperWidth,
    };

    return convertedPageSettings;
  }
}
