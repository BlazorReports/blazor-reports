using BlazorReports.Models;
using BlazorReports.Services;
using BlazorReports.Services.BrowserServices;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorReports.Extensions;

/// <summary>
/// Extension methods for <see cref="IServiceCollection" />.
/// </summary>
public static class ServiceCollectionExtensions
{
  /// <summary>
  /// Adds BlazorReports services to the specified <see cref="IServiceCollection" />.
  /// </summary>
  /// <param name="services"></param>
  /// <param name="options"></param>
  /// <returns> The <see cref="IServiceCollection" /> so that additional calls can be chained. </returns>
  public static IServiceCollection AddBlazorReports(
    this IServiceCollection services,
    Action<BlazorReportsOptions>? options = null
  )
  {
    services.Configure(options ?? (_ => { }));
    services.AddSingleton<BlazorReportRegistry>();
    services.AddSingleton<IBrowserFactory, BrowserFactory>();
    services.AddSingleton<IBrowserService, BrowserService>();
    services.AddSingleton<IReportService, ReportService>();

    return services;
  }
}
