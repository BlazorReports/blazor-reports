using System.IO.Pipelines;
using BlazorReports.Components;
using BlazorReports.Models;
using BlazorReports.Services.Browser;
using BlazorReports.Services.Browser.Problems;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneOf;
using OneOf.Types;

namespace BlazorReports.Services;

/// <summary>
/// Service for generating reports
/// </summary>
public sealed class ReportService : IReportService, IAsyncDisposable
{
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
    _serviceProvider = serviceProvider;
    _reportRegistry = reportRegistry;
    _browserService = new BrowserService(options.Value.BrowserOptions);
  }

  /// <summary>
  /// Generates a report using the specified component and data
  /// </summary>
  /// <param name="pipeWriter"> The pipe writer to write the report to </param>
  /// <param name="data"> The data to use in the report </param>
  /// <param name="cancellationToken"> The cancellation token </param>
  /// <typeparam name="T"> The component to use in the report </typeparam>
  /// <typeparam name="TD"> The type of data to use in the report </typeparam>
  /// <returns> The generated report </returns>
  public async ValueTask<OneOf<Success, ServerBusyProblem, OperationCancelledProblem>> GenerateReport<T, TD>(
    PipeWriter pipeWriter, TD data, CancellationToken cancellationToken = default)
    where T : ComponentBase where TD : class
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

    return await _browserService.PrintReportFromBrowser(pipeWriter, html, _reportRegistry.DefaultPageSettings,
      cancellationToken);
  }

  /// <summary>
  /// Generates a report using the specified component and data
  /// </summary>
  /// <param name="pipeWriter"> The pipe writer to write the report to </param>
  /// <param name="blazorReport"> The report to generate </param>
  /// <param name="data"> The data to use in the report </param>
  /// <param name="cancellationToken"> The cancellation token </param>
  /// <typeparam name="T"> The type of data to use in the report </typeparam>
  /// <returns> The generated report </returns>
  public async ValueTask<OneOf<Success, ServerBusyProblem, OperationCancelledProblem>> GenerateReport<T>(
    PipeWriter pipeWriter, BlazorReport blazorReport, T? data,
    CancellationToken cancellationToken = default) where T : class
  {
    using var scope = _serviceProvider.CreateScope();
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

    var baseStyles = string.Empty;
    if (!string.IsNullOrEmpty(blazorReport.BaseStyles))
    {
      baseStyles = blazorReport.BaseStyles;
    }
    else if (!string.IsNullOrEmpty(_reportRegistry.BaseStyles))
    {
      baseStyles = _reportRegistry.BaseStyles;
    }

    var childComponentParameters = new Dictionary<string, object?>();
    if (blazorReport.Component.BaseType == typeof(BlazorReportsBase) && _reportRegistry.GlobalAssets.Count != 0)
    {
      childComponentParameters.Add("GlobalAssets", _reportRegistry.GlobalAssets);
    }

    if (blazorReport.Assets.Count != 0)
    {
      childComponentParameters.Add("ReportAssets", blazorReport.Assets);
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

    return await _browserService.PrintReportFromBrowser(pipeWriter, html, pageSettings, cancellationToken);
  }

  /// <summary>
  /// Generates a report using the specified component
  /// </summary>
  /// <param name="pipeWriter"> The pipe writer to write the report to </param>
  /// <param name="blazorReport"> The report to generate </param>
  /// <param name="cancellationToken"> The cancellation token </param>
  /// <returns> The generated report </returns>
  public ValueTask<OneOf<Success, ServerBusyProblem, OperationCancelledProblem>> GenerateReport(PipeWriter pipeWriter,
    BlazorReport blazorReport, CancellationToken cancellationToken = default)
  {
    return GenerateReport<object>(pipeWriter, blazorReport, null, cancellationToken);
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

  /// <summary>
  /// Disposes the blazor report service
  /// </summary>
  public async ValueTask DisposeAsync()
  {
    await _browserService.DisposeAsync();
  }
}
