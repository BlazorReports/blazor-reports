using BlazorReports.Components;
using BlazorReports.Models;
using ChromiumHtmlToPdfLib;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorReports.Services;

/// <summary>
/// Service for generating reports
/// </summary>
public class ReportService : IReportService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly BlazorReportRegistry _reportRegistry;

  /// <summary>
  /// Creates a new instance of <see cref="ReportService"/>
  /// </summary>
  /// <param name="serviceProvider"> The service provider </param>
  /// <param name="reportRegistry"> The report registry </param>
  public ReportService(
    IServiceProvider serviceProvider,
    BlazorReportRegistry reportRegistry)
  {
    _serviceProvider = serviceProvider;
    _reportRegistry = reportRegistry;
  }

  /// <summary>
  /// Generates a report using the specified component and data
  /// </summary>
  /// <param name="data"> The data to use in the report </param>
  /// <typeparam name="T"> The component to use in the report </typeparam>
  /// <typeparam name="TD"> The type of data to use in the report </typeparam>
  /// <returns> The generated report </returns>
  public async Task<MemoryStream> GenerateReport<T, TD>(TD data) where T : ComponentBase where TD : class
  {
    using var scope = _serviceProvider.CreateScope();
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

    var baseStyles = string.Empty;
    if (!string.IsNullOrEmpty(_reportRegistry.BaseStyles))
    {
      baseStyles = _reportRegistry.BaseStyles;
    }

    var componentParameters = new Dictionary<string, object?>();
    componentParameters.Add("BaseStyles", baseStyles);
    componentParameters.Add("Data", data);
    componentParameters.Add("GlobalAssets", _reportRegistry.GlobalAssets);

    await using var htmlRenderer = new HtmlRenderer(scope.ServiceProvider, loggerFactory);
    var html = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
    {
      var parameters = ParameterView.FromDictionary(componentParameters);
      var output = await htmlRenderer.RenderComponentAsync<T>(parameters);
      return output.ToHtmlString();
    });

    var reportStream = new MemoryStream();
    using var converter = new Converter();
    converter.ConvertToPdf(html, reportStream, _reportRegistry.DefaultPageSettings);
    return reportStream;
  }

  /// <summary>
  /// Generates a report using the specified component and data
  /// </summary>
  /// <param name="blazorReport"> The report to generate </param>
  /// <param name="data"> The data to use in the report </param>
  /// <typeparam name="T"> The type of data to use in the report </typeparam>
  /// <returns> The generated report </returns>
  public async Task<MemoryStream> GenerateReport<T>(BlazorReport blazorReport, T? data) where T : class
  {
    using var scope = _serviceProvider.CreateScope();
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

    var baseStyles = string.Empty;
    if (!string.IsNullOrEmpty(_reportRegistry.BaseStyles))
    {
      baseStyles = _reportRegistry.BaseStyles;
    }

    if (!string.IsNullOrEmpty(blazorReport.BaseStylesPath))
    {
      baseStyles = await File.ReadAllTextAsync(blazorReport.BaseStylesPath);
    }

    var reportAssets = new Dictionary<string, string>();
    if (!string.IsNullOrEmpty(blazorReport.AssetsPath))
    {
      var assetsPath = blazorReport.AssetsPath;
      var assetsDirectory = new DirectoryInfo(assetsPath);
      if (assetsDirectory.Exists)
      {
        foreach (var file in assetsDirectory.GetFiles())
        {
          var fileBytes = await File.ReadAllBytesAsync(file.FullName);
          var base64Uri = $"data:image/webp;base64,{Convert.ToBase64String(fileBytes)}";
          reportAssets.Add(file.Name, base64Uri);
        }
      }
    }

    var childComponentParameters = new Dictionary<string, object?>();
    if (blazorReport.Component.BaseType == typeof(BlazorReportsBase) && _reportRegistry.GlobalAssets.Count != 0)
    {
      childComponentParameters.Add("GlobalAssets", _reportRegistry.GlobalAssets);
    }

    if (reportAssets.Count != 0)
    {
      childComponentParameters.Add("ReportAssets", reportAssets);
    }

    if (data is not null)
    {
      childComponentParameters.Add("Data", data);
    }

    var baseComponentParameters = new Dictionary<string, object?>();
    if (!string.IsNullOrEmpty(baseStyles))
    {
      baseComponentParameters.Add("BaseStyles", baseStyles);
    }

    baseComponentParameters.Add("ChildComponentType", blazorReport.Component);
    baseComponentParameters.Add("ChildComponentParameters", childComponentParameters);

    await using var htmlRenderer = new HtmlRenderer(scope.ServiceProvider, loggerFactory);
    var html = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
    {
      var parameters = ParameterView.FromDictionary(baseComponentParameters);
      var output = await htmlRenderer.RenderComponentAsync<BlazorReportsTemplate>(parameters);
      return output.ToHtmlString();
    });

    var pageSettings = blazorReport.PageSettings ?? _reportRegistry.DefaultPageSettings;

    var reportStream = new MemoryStream();
    using var converter = new Converter();
    converter.ConvertToPdf(html, reportStream, pageSettings);
    return reportStream;
  }

  /// <summary>
  /// Generates a report using the specified component
  /// </summary>
  /// <param name="blazorReport"> The report to generate </param>
  /// <returns> The generated report </returns>
  public async Task<MemoryStream> GenerateReport(BlazorReport blazorReport)
  {
    return await GenerateReport<object>(blazorReport, null);
  }

  /// <summary>
  /// Gets a blazor report by name
  /// </summary>
  /// <param name="name"> The name of the report to get </param>
  /// <returns> The blazor report </returns>
  public BlazorReport? GetReportByName(string name)
  {
    var reportNormalizedName = name.ToLowerInvariant().Trim();
    var foundReport = _reportRegistry.Reports.TryGetValue(reportNormalizedName, out var report);
    return foundReport ? report : null;
  }
}
