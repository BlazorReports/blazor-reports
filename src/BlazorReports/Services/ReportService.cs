using System.IO.Pipelines;
using BlazorReports.Components;
using BlazorReports.Models;
using BlazorReports.Services.Browser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorReports.Services;

/// <summary>
/// Service for generating reports
/// </summary>
public class ReportService : IReportService
{
  private readonly IOptions<BlazorReportsOptions> _options;
  private readonly IServiceProvider _serviceProvider;
  private readonly BlazorReportRegistry _reportRegistry;
  private readonly BrowserService _browserService;

  /// <summary>
  /// Creates a new instance of <see cref="ReportService"/>
  /// </summary>
  /// <param name="options"> The BlazorReportsOptions </param>
  /// <param name="serviceProvider"> The service provider </param>
  /// <param name="reportRegistry"> The report registry </param>
  public ReportService(
    IOptions<BlazorReportsOptions> options,
    IServiceProvider serviceProvider,
    BlazorReportRegistry reportRegistry
  )
  {
    _options = options;
    _serviceProvider = serviceProvider;
    _reportRegistry = reportRegistry;
    _browserService = new BrowserService();
  }

  /// <summary>
  /// Generates a report using the specified component and data
  /// </summary>
  /// <param name="data"> The data to use in the report </param>
  /// <typeparam name="T"> The component to use in the report </typeparam>
  /// <typeparam name="TD"> The type of data to use in the report </typeparam>
  /// <returns> The generated report </returns>
  public async Task<PipeReader> GenerateReport<T, TD>(TD data) where T : ComponentBase where TD : class
  {
    using var scope = _serviceProvider.CreateScope();
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

    var baseStyles = string.Empty;
    if (!string.IsNullOrEmpty(_reportRegistry.BaseStyles))
    {
      baseStyles = _reportRegistry.BaseStyles;
    }

    var componentParameters = new Dictionary<string, object?>
    {
      {"BaseStyles", baseStyles},
      {"Data", data},
      {"GlobalAssets", _reportRegistry.GlobalAssets}
    };

    await using var htmlRenderer = new HtmlRenderer(scope.ServiceProvider, loggerFactory);
    var html = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
    {
      var parameters = ParameterView.FromDictionary(componentParameters);
      var output = await htmlRenderer.RenderComponentAsync<T>(parameters);
      return output.ToHtmlString();
    });

    await _browserService.StartBrowserHeadless(_options.Value.Browser);
    var browserPage = await _browserService.GetBrowserPage();
    await browserPage.DisplayHtml(html);
    var reportStream = browserPage.ConvertPageToPdf(_reportRegistry.DefaultPageSettings);
    _browserService.ReturnBrowserPage(browserPage);
    return reportStream;
  }

  /// <summary>
  /// Generates a report using the specified component and data
  /// </summary>
  /// <param name="blazorReport"> The report to generate </param>
  /// <param name="data"> The data to use in the report </param>
  /// <typeparam name="T"> The type of data to use in the report </typeparam>
  /// <returns> The generated report </returns>
  public async Task<PipeReader> GenerateReport<T>(BlazorReport blazorReport, T? data) where T : class
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

    await _browserService.StartBrowserHeadless(_options.Value.Browser);
    var browserPage = await _browserService.GetBrowserPage();
    await browserPage.DisplayHtml(html);
    var reportStream = browserPage.ConvertPageToPdf(pageSettings);
    _browserService.ReturnBrowserPage(browserPage);
    return reportStream;
  }

  /// <summary>
  /// Generates a report using the specified component
  /// </summary>
  /// <param name="blazorReport"> The report to generate </param>
  /// <returns> The generated report </returns>
  public async Task<PipeReader> GenerateReport(BlazorReport blazorReport)
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
