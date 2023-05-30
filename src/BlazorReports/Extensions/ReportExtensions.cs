using BlazorReports.Models;
using BlazorReports.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorReports.Extensions;

/// <summary>
///  Extension methods for <see cref="IApplicationBuilder" />.
/// </summary>
public static class ReportExtensions
{
  /// <summary>
  /// Registers a Blazor report with data type <typeparamref name="TD" /> and component type <typeparamref name="T" />.
  /// </summary>
  /// <param name="app"> The <see cref="IApplicationBuilder" /> to register the report with. </param>
  /// <param name="options"> The <see cref="BlazorReportRegistrationOptions" /> to use. </param>
  /// <typeparam name="T"></typeparam>
  /// <typeparam name="TD"></typeparam>
  /// <returns> The <see cref="IApplicationBuilder" />. </returns>
  /// <exception cref="InvalidOperationException"></exception>
  public static IApplicationBuilder RegisterBlazorReport<T, TD>(this IApplicationBuilder app,
    BlazorReportRegistrationOptions? options = null) where T : ComponentBase where TD : class
  {
    var reportRegistry = app.ApplicationServices.GetRequiredService<BlazorReportRegistry>();

    var reportName = options?.ReportName ?? typeof(T).Name;
    var normalizedReportName = reportName.ToLowerInvariant().Trim();
    if (reportRegistry.Reports.ContainsKey(normalizedReportName))
      throw new InvalidOperationException($"Report with name {normalizedReportName} already exists");

    reportRegistry.Reports.Add(normalizedReportName, new BlazorReport
    {
      Component = typeof(T),
      Data = typeof(TD),
      BaseStylesPath = options?.BaseStylesPath ?? string.Empty,
      AssetsPath = options?.AssetsPath ?? string.Empty,
    });

    return app;
  }

  /// <summary>
  /// Registers a Blazor report with component type <typeparamref name="T" />.
  /// </summary>
  /// <param name="app"> The <see cref="IApplicationBuilder" /> to register the report with. </param>
  /// <param name="options"> The <see cref="BlazorReportRegistrationOptions" /> to use. </param>
  /// <typeparam name="T"></typeparam>
  /// <returns> The <see cref="IApplicationBuilder" />. </returns>
  /// <exception cref="InvalidOperationException"></exception>
  public static IApplicationBuilder RegisterBlazorReport<T>(this IApplicationBuilder app,
    BlazorReportRegistrationOptions? options = null) where T : ComponentBase
  {
    var reportRegistry = app.ApplicationServices.GetRequiredService<BlazorReportRegistry>();

    var reportName = options?.ReportName ?? typeof(T).Name;
    var normalizedReportName = reportName.ToLowerInvariant().Trim();
    if (reportRegistry.Reports.ContainsKey(normalizedReportName))
      throw new InvalidOperationException($"Report with name {normalizedReportName} already exists");

    reportRegistry.Reports.Add(normalizedReportName, new BlazorReport
    {
      Component = typeof(T),
      Data = null,
      BaseStylesPath = options?.BaseStylesPath ?? string.Empty,
      AssetsPath = options?.AssetsPath ?? string.Empty,
    });

    return app;
  }

  /// <summary>
  /// Registers a Blazor report with a component type <typeparamref name="T" />.  and registers a minimal api endpoint to generate the report.
  /// </summary>
  /// <param name="endpoints"> The <see cref="IEndpointRouteBuilder" /> to register the report with. </param>
  /// <param name="options"> The <see cref="BlazorReportRegistrationOptions" /> to use. </param>
  /// <typeparam name="T"> The component type. </typeparam>
  /// <returns> The <see cref="RouteHandlerBuilder" />. </returns>
  /// <exception cref="InvalidOperationException"></exception>
  public static RouteHandlerBuilder MapBlazorReport<T>(this IEndpointRouteBuilder endpoints,
    BlazorReportRegistrationOptions? options = null) where T : ComponentBase
  {
    var reportRegistry = endpoints.ServiceProvider.GetRequiredService<BlazorReportRegistry>();

    var reportNameToUse = options?.ReportName ?? typeof(T).Name;
    var normalizedReportName = reportNameToUse.ToLowerInvariant().Trim();
    if (reportRegistry.Reports.ContainsKey(normalizedReportName))
      throw new InvalidOperationException($"Report with name {normalizedReportName} already exists");

    reportRegistry.Reports.Add(normalizedReportName, new BlazorReport
    {
      Component = typeof(T),
      Data = null,
      BaseStylesPath = options?.BaseStylesPath ?? string.Empty,
      AssetsPath = options?.AssetsPath ?? string.Empty,
    });

    return endpoints.MapPost($"reports/{normalizedReportName}",
      async ([FromServices] IReportService reportService) =>
      {
        var blazorReport = reportService.GetReportByName(normalizedReportName);
        if (blazorReport is null)
          return Results.Problem();

        using var report = await reportService.GenerateReport(blazorReport);
        return Results.File(report.ToArray(), "application/pdf", $"{normalizedReportName}.pdf");
      });
  }

  /// <summary>
  /// Registers a Blazor report with a component type <typeparamref name="T" /> and data type <typeparamref name="TD" />.  and registers a minimal api endpoint to generate the report.
  /// </summary>
  /// <param name="endpoints"> The <see cref="IEndpointRouteBuilder" /> to register the report with. </param>
  /// <param name="options"> The <see cref="BlazorReportRegistrationOptions" /> to use. </param>
  /// <typeparam name="T"> The component type. </typeparam>
  /// <typeparam name="TD"> The data type. </typeparam>
  /// <returns> The <see cref="RouteHandlerBuilder" />. </returns>
  /// <exception cref="InvalidOperationException"></exception>
  public static RouteHandlerBuilder MapBlazorReport<T, TD>(this IEndpointRouteBuilder endpoints,
    BlazorReportRegistrationOptions? options = null) where T : ComponentBase where TD : class
  {
    var reportRegistry = endpoints.ServiceProvider.GetRequiredService<BlazorReportRegistry>();

    var reportNameToUse = options?.ReportName ?? typeof(T).Name;
    var normalizedReportName = reportNameToUse.ToLowerInvariant().Trim();
    if (reportRegistry.Reports.ContainsKey(normalizedReportName))
      throw new InvalidOperationException($"Report with name {normalizedReportName} already exists");

    reportRegistry.Reports.Add(normalizedReportName, new BlazorReport
    {
      Component = typeof(T),
      Data = typeof(TD),
      BaseStylesPath = options?.BaseStylesPath ?? string.Empty,
      AssetsPath = options?.AssetsPath ?? string.Empty,
    });

    return endpoints.MapPost($"reports/{normalizedReportName}",
      async (TD data, [FromServices] IReportService reportService) =>
      {
        var blazorReport = reportService.GetReportByName(normalizedReportName);
        if (blazorReport is null)
          return Results.Problem();

        using var report = await reportService.GenerateReport(blazorReport, data);
        return Results.File(report.ToArray(), "application/pdf", $"{normalizedReportName}.pdf");
      });
  }
}
