using BlazorReports.Models;
using BlazorReports.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorReports.Extensions;

public static class ReportExtensions
{
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
        return Results.File(report.ToArray(), "application/pdf");
      });
  }

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
        return Results.File(report.ToArray(), "application/pdf");
      });
  }
}
