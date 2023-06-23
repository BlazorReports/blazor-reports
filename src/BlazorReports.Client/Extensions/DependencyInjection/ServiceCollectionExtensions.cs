using System.Net;
using BlazorReports.Client.Policies;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorReports.Client.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddBlazorReportsClient(
    this IServiceCollection services, Uri serverUri)
  {
    services.AddHttpClient("BlazorReports", client =>
      {
        client.BaseAddress = serverUri;
        client.DefaultRequestVersion = HttpVersion.Version11;
        client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
      })
      .AddPolicyHandler(BlazorReportsClientPolicy.GetRetryPolicy())
      .AddPolicyHandler(BlazorReportsClientPolicy.GetCircuitBreakerPolicy());

    services.AddSingleton<IBlazorReportsClient, BlazorReportsClient>();

    return services;
  }
}
