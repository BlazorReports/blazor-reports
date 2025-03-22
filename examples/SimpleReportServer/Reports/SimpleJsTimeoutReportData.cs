namespace SimpleReportServer.Reports;

public record SimpleJsTimeoutReportData(int TimeoutInSeconds)
{
  public TimeSpan TimeSpan => TimeSpan.FromSeconds(TimeoutInSeconds);
};
