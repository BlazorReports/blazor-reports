using BlazorReports.Models;
using BlazorReports.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
  /// <param name="setupAction"> The <see cref="BlazorReportRegistrationOptions" /> to use. </param>
  /// <typeparam name="T"></typeparam>
  /// <typeparam name="TD"></typeparam>
  /// <returns> The <see cref="IApplicationBuilder" />. </returns>
  /// <exception cref="InvalidOperationException"></exception>
  public static IApplicationBuilder RegisterBlazorReport<T, TD>(
    this IApplicationBuilder app,
    Action<BlazorReportRegistrationOptions>? setupAction = null
  )
    where T : ComponentBase
    where TD : class
  {
    using var serviceScope = app.ApplicationServices.CreateScope();
    var options = GetReportRegistrationOptions(serviceScope, setupAction);
    var reportRegistry = serviceScope.ServiceProvider.GetRequiredService<BlazorReportRegistry>();

    reportRegistry.AddReport<T, TD>(options);

    return app;
  }

  /// <summary>
  /// Registers a Blazor report with component type <typeparamref name="T" />.
  /// </summary>
  /// <param name="app"> The <see cref="IApplicationBuilder" /> to register the report with. </param>
  /// <param name="setupAction"> The <see cref="BlazorReportRegistrationOptions" /> to use. </param>
  /// <typeparam name="T"></typeparam>
  /// <returns> The <see cref="IApplicationBuilder" />. </returns>
  /// <exception cref="InvalidOperationException"></exception>
  public static IApplicationBuilder RegisterBlazorReport<T>(
    this IApplicationBuilder app,
    Action<BlazorReportRegistrationOptions>? setupAction = null
  )
    where T : ComponentBase
  {
    using var serviceScope = app.ApplicationServices.CreateScope();
    var options = GetReportRegistrationOptions(serviceScope, setupAction);
    var reportRegistry = serviceScope.ServiceProvider.GetRequiredService<BlazorReportRegistry>();

    reportRegistry.AddReport<T>(options);

    return app;
  }

  /// <summary>
  /// Registers a Blazor report with a component type <typeparamref name="T" />.  and registers a minimal api endpoint to generate the report.
  /// </summary>
  /// <param name="endpoints"> The <see cref="IEndpointRouteBuilder" /> to register the report with. </param>
  /// <param name="setupAction"> The <see cref="BlazorReportRegistrationOptions" /> to use. </param>
  /// <typeparam name="T"> The component type. </typeparam>
  /// <returns> The <see cref="RouteHandlerBuilder" />. </returns>
  /// <exception cref="InvalidOperationException"></exception>
  public static RouteHandlerBuilder MapBlazorReport<T>(
    this IEndpointRouteBuilder endpoints,
    Action<BlazorReportRegistrationOptions>? setupAction = null
  )
    where T : ComponentBase
  {
    using var serviceScope = endpoints.ServiceProvider.CreateScope();
    var options = GetReportRegistrationOptions(serviceScope, setupAction);

    var reportRegistry = serviceScope.ServiceProvider.GetRequiredService<BlazorReportRegistry>();
    var blazorReport = reportRegistry.AddReport<T>(options);

    return endpoints
      .MapPost(
        $"{blazorReport.NormalizedName}",
        async (
          [FromServices] IReportService reportService,
          HttpContext context,
          CancellationToken token
        ) =>
        {
          context.Response.ContentType = "application/pdf";
          context.Response.Headers.Append(
            "Content-Disposition",
            $"attachment; filename=\"{blazorReport.Name}.pdf\""
          );
          var result = await reportService.GenerateReport(
            context.Response.BodyWriter,
            blazorReport,
            token
          );

          var errorStatusCode = result.Match(
            _ => (int?)null,
            _ => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status499ClientClosedRequest,
            _ => StatusCodes.Status500InternalServerError
          );

          if (errorStatusCode is not null)
          {
            context.Response.StatusCode = errorStatusCode.Value;
            await context.Response.BodyWriter.CompleteAsync();
          }
        }
      )
      .Produces<FileStreamHttpResult>(200, "application/pdf")
      .Produces(StatusCodes.Status503ServiceUnavailable);
  }

  /// <summary>
  /// Registers a Blazor report with a component type <typeparamref name="T" /> and data type <typeparamref name="TD" />.  and registers a minimal api endpoint to generate the report.
  /// </summary>
  /// <param name="endpoints"> The <see cref="IEndpointRouteBuilder" /> to register the report with. </param>
  /// <param name="setupAction"> The <see cref="BlazorReportRegistrationOptions" /> to use. </param>
  /// <typeparam name="T"> The component type. </typeparam>
  /// <typeparam name="TD"> The data type. </typeparam>
  /// <returns> The <see cref="RouteHandlerBuilder" />. </returns>
  /// <exception cref="InvalidOperationException"></exception>
  public static RouteHandlerBuilder MapBlazorReport<T, TD>(
    this IEndpointRouteBuilder endpoints,
    Action<BlazorReportRegistrationOptions>? setupAction = null
  )
    where T : ComponentBase
    where TD : class
  {
    using var serviceScope = endpoints.ServiceProvider.CreateScope();
    var options = GetReportRegistrationOptions(serviceScope, setupAction);

    var reportRegistry = serviceScope.ServiceProvider.GetRequiredService<BlazorReportRegistry>();
    var blazorReport = reportRegistry.AddReport<T, TD>(options);

    return endpoints
      .MapPost(
        $"{blazorReport.NormalizedName}",
        async (
          TD data,
          [FromServices] IReportService reportService,
          HttpContext context,
          CancellationToken token
        ) =>
        {
          context.Response.ContentType = "application/pdf";
          context.Response.Headers.Append(
            "Content-Disposition",
            $"attachment; filename=\"{blazorReport.Name}.pdf\""
          );
          var result = await reportService.GenerateReport(
            context.Response.BodyWriter,
            blazorReport,
            data,
            token
          );

          var errorStatusCode = result.Match(
            _ => (int?)null,
            _ => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status499ClientClosedRequest,
            _ => StatusCodes.Status500InternalServerError
          );

          if (errorStatusCode is not null)
          {
            context.Response.StatusCode = errorStatusCode.Value;
            await context.Response.BodyWriter.CompleteAsync();
          }
        }
      )
      .Produces<FileStreamHttpResult>(200, "application/pdf")
      .Produces(StatusCodes.Status503ServiceUnavailable);
  }

  private static BlazorReportRegistrationOptions GetReportRegistrationOptions(
    IServiceScope serviceScope,
    Action<BlazorReportRegistrationOptions>? setupAction = null
  )
  {
    var options = new BlazorReportRegistrationOptions();
    var globalOptions = serviceScope
      .ServiceProvider.GetRequiredService<IOptionsSnapshot<BlazorReportsOptions>>()
      .Value;
    options.PageSettings = globalOptions.PageSettings;
    setupAction?.Invoke(options);
    return options;
  }
}
