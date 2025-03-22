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
          var contentType = blazorReport.GetContentType();
          var extension = blazorReport.GetFileExtension();
          context.Response.Headers.Append(
            "Content-Disposition",
            $"attachment; filename=\"{blazorReport.Name}.{extension}\""
          );
          context.Response.ContentType = contentType;
          var result = await reportService.GenerateReport(
            context.Response.BodyWriter,
            blazorReport,
            token
          );

          return result.Match<Results<FileStreamHttpResult, ProblemHttpResult>>(
            success => TypedResults.File(context.Response.Body, contentType),
            serverBusy =>
            {
              context.Response.Headers.Clear();
              context.Response.ContentType = "application/problem+json";
              return TypedResults.Problem(
                title: "Server Busy",
                detail: "The report service is currently handling the maximum number of concurrent reports.",
                statusCode: StatusCodes.Status503ServiceUnavailable
              );
            },
            cancelled =>
            {
              context.Response.Headers.Clear();
              context.Response.ContentType = "application/problem+json";
              return TypedResults.Problem(
                title: "Request Cancelled",
                detail: "The request was cancelled by the client.",
                statusCode: StatusCodes.Status499ClientClosedRequest
              );
            },
            browserProblem =>
            {
              context.Response.Headers.Clear();
              context.Response.ContentType = "application/problem+json";
              return TypedResults.Problem(
                title: "Browser Error",
                detail: "An internal error occurred while processing the report.",
                statusCode: StatusCodes.Status500InternalServerError
              );
            },
            jsTimeout =>
            {
              context.Response.Headers.Clear();
              context.Response.ContentType = "application/problem+json";
              return TypedResults.Problem(
                title: "JavaScript Timeout",
                detail: "The JavaScript did not signal completion before the timeout expired.",
                statusCode: StatusCodes.Status408RequestTimeout
              );
            },
            notCompleted =>
            {
              context.Response.Headers.Clear();
              context.Response.ContentType = "application/problem+json";
              return TypedResults.Problem(
                title: "Completion Not Signaled",
                detail: "WaitForJavascriptCompletedSignal was enabled, but .completed() was not called in JavaScript.",
                statusCode: StatusCodes.Status500InternalServerError
              );
            }
          );
        }
      )
      .Produces<FileStreamHttpResult>(StatusCodes.Status200OK, blazorReport.GetContentType())
      .ProducesProblem(StatusCodes.Status408RequestTimeout)
      .ProducesProblem(StatusCodes.Status499ClientClosedRequest)
      .ProducesProblem(StatusCodes.Status500InternalServerError)
      .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
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
        $"/{blazorReport.NormalizedName}",
        async Task<Results<FileStreamHttpResult, ProblemHttpResult>> (
          TD data,
          [FromServices] IReportService reportService,
          HttpContext context,
          CancellationToken token
        ) =>
        {
          var contentType = blazorReport.GetContentType();
          var extension = blazorReport.GetFileExtension();
          context.Response.Headers.Append(
            "Content-Disposition",
            $"attachment; filename=\"{blazorReport.Name}.{extension}\""
          );
          context.Response.ContentType = contentType;
          var result = await reportService.GenerateReport(
            context.Response.BodyWriter,
            blazorReport,
            data,
            token
          );

          return result.Match<Results<FileStreamHttpResult, ProblemHttpResult>>(
            success => TypedResults.File(context.Response.Body, contentType),
            serverBusy =>
            {
              context.Response.Headers.Clear();
              context.Response.ContentType = "application/problem+json";
              return TypedResults.Problem(
                title: "Server Busy",
                detail: "The report service is currently handling the maximum number of concurrent reports.",
                statusCode: StatusCodes.Status503ServiceUnavailable
              );
            },
            cancelled =>
            {
              context.Response.Headers.Clear();
              context.Response.ContentType = "application/problem+json";
              return TypedResults.Problem(
                title: "Request Cancelled",
                detail: "The request was cancelled by the client.",
                statusCode: StatusCodes.Status499ClientClosedRequest
              );
            },
            browserProblem =>
            {
              context.Response.Headers.Clear();
              context.Response.ContentType = "application/problem+json";
              return TypedResults.Problem(
                title: "Browser Error",
                detail: "An internal error occurred while processing the report.",
                statusCode: StatusCodes.Status500InternalServerError
              );
            },
            jsTimeout =>
            {
              context.Response.Headers.Clear();
              context.Response.ContentType = "application/problem+json";
              return TypedResults.Problem(
                title: "JavaScript Timeout",
                detail: "The JavaScript did not signal completion before the timeout expired.",
                statusCode: StatusCodes.Status408RequestTimeout
              );
            },
            notCompleted =>
            {
              context.Response.Headers.Clear();
              context.Response.ContentType = "application/problem+json";
              return TypedResults.Problem(
                title: "Completion Not Signaled",
                detail: "WaitForJavascriptCompletedSignal was enabled, but .completed() was not called in JavaScript.",
                statusCode: StatusCodes.Status500InternalServerError
              );
            }
          );
        }
      )
      .Produces<FileStreamHttpResult>(StatusCodes.Status200OK, blazorReport.GetContentType())
      .ProducesProblem(StatusCodes.Status408RequestTimeout)
      .ProducesProblem(StatusCodes.Status499ClientClosedRequest)
      .ProducesProblem(StatusCodes.Status500InternalServerError)
      .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
  }

  private static BlazorReportRegistrationOptions GetReportRegistrationOptions(
    IServiceScope serviceScope,
    Action<BlazorReportRegistrationOptions>? setupAction = null
  )
  {
    BlazorReportRegistrationOptions options = new();
    var globalOptions = serviceScope
      .ServiceProvider.GetRequiredService<IOptionsSnapshot<BlazorReportsOptions>>()
      .Value;
    options.PageSettings = globalOptions.PageSettings;
    setupAction?.Invoke(options);
    return options;
  }
}
