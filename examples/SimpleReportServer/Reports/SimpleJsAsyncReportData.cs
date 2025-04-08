namespace SimpleReportServer.Reports;

public record SimpleJsAsyncReportData(int TimeoutInSeconds)
{
  public TimeSpan TimeSpan => TimeSpan.FromSeconds(TimeoutInSeconds);
};
