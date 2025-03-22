namespace SimpleReportServer.Reports;

public record SimpleJsTimeoutReportData(int TimeoutInSeconds, bool ShouldContainCompletedJsMethod)
{
  public TimeSpan TimeSpan => TimeSpan.FromSeconds(TimeoutInSeconds);
};
