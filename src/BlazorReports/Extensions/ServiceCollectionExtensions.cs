using BlazorReports.Models;
using BlazorReports.Services;
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
  /// <returns></returns>
  public static IServiceCollection AddBlazorReports(
    this IServiceCollection services, Action<BlazorReportsOptions>? options = null)
  {
    services.Configure(options ?? (_ => { }));
    services.AddSingleton<BlazorReportRegistry>();
    services.AddScoped<IReportService, ReportService>();

    return services;
  }
}
