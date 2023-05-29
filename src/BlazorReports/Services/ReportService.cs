using BlazorReports.Models;
using ChromiumHtmlToPdfLib;
using ChromiumHtmlToPdfLib.Settings;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorReports.Services;

public class ReportService : IReportService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly BlazorReportRegistry _reportRegistry;

  public ReportService(IServiceProvider serviceProvider, BlazorReportRegistry reportRegistry)
  {
    _serviceProvider = serviceProvider;
    _reportRegistry = reportRegistry;
  }

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

    var pageSettings = new PageSettings();
    using var reportStream = new MemoryStream();
    using var converter = new Converter();
    converter.ConvertToPdf(html, reportStream, pageSettings);
    return reportStream;
  }

  public async Task<MemoryStream> GenerateReport<T>(BlazorReport blazorReport, T data) where T : class
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

    var componentParameters = new Dictionary<string, object?>();
    componentParameters.Add("BaseStyles", baseStyles);
    componentParameters.Add("Data", data);
    componentParameters.Add("GlobalAssets", _reportRegistry.GlobalAssets);
    componentParameters.Add("ReportAssets", reportAssets);

    await using var htmlRenderer = new HtmlRenderer(scope.ServiceProvider, loggerFactory);
    var html = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
    {
      var parameters = ParameterView.FromDictionary(componentParameters);
      var output = await htmlRenderer.RenderComponentAsync(blazorReport.Component, parameters);
      return output.ToHtmlString();
    });

    var pageSettings = new PageSettings();
    using var reportStream = new MemoryStream();
    using var converter = new Converter();
    converter.ConvertToPdf(html, reportStream, pageSettings);
    return reportStream;
  }

  public async Task<MemoryStream> GenerateReport(BlazorReport blazorReport)
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

    var componentParameters = new Dictionary<string, object?>();
    if (!string.IsNullOrEmpty(baseStyles))
    {
      componentParameters.Add("BaseStyles", baseStyles);
    }
    if (_reportRegistry.GlobalAssets.Count != 0)
    {
      componentParameters.Add("GlobalAssets", _reportRegistry.GlobalAssets);
    }
    if (reportAssets.Count != 0)
    {
      componentParameters.Add("ReportAssets", reportAssets);
    }

    await using var htmlRenderer = new HtmlRenderer(scope.ServiceProvider, loggerFactory);
    var html = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
    {
      var parameters = ParameterView.FromDictionary(componentParameters);
      var output = await htmlRenderer.RenderComponentAsync(blazorReport.Component, parameters);
      return output.ToHtmlString();
    });

    var pageSettings = new PageSettings();
    using var reportStream = new MemoryStream();
    using var converter = new Converter();
    converter.ConvertToPdf(html, reportStream, pageSettings);
    return reportStream;
  }

  public BlazorReport? GetReportByName(string name)
  {
    var reportNormalizedName = name.ToLowerInvariant().Trim();
    var foundReport = _reportRegistry.Reports.TryGetValue(reportNormalizedName, out var report);
    return foundReport ? report : null;
  }
}
