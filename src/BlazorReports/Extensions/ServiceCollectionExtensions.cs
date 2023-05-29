using BlazorReports.Models;
using BlazorReports.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorReports.Extensions;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddBlazorReports(
    this IServiceCollection services, Action<BlazorReportsOptions>? options = null)
  {
    services.Configure(options ?? (_ => { }));
    services.AddSingleton<BlazorReportRegistry>();
    services.AddScoped<IReportService, ReportService>();

    return services;
  }
}
