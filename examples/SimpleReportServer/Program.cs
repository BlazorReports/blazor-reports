using BlazorReports.Extensions;
using BlazorReports.Models;
using ExampleTemplates.Reports;
using SimpleReportServer;
using SimpleReportServer.Reports;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddBlazorReports();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

var reportsGroup = app.MapGroup("reports");

reportsGroup.MapBlazorReport<SimpleJsAsyncReport, SimpleJsAsyncReportData>(opts =>
{
  opts.JavascriptSettings.WaitForJavascriptCompletedSignal = true;
});
reportsGroup.MapBlazorReport<HelloReport, HelloReportData>();
reportsGroup.MapBlazorReport<HelloReport, HelloReportData>(opts =>
{
  opts.ReportName = "HelloReportHtml";
  opts.OutputFormat = ReportOutputFormat.Html;
});
reportsGroup.MapBlazorReport<ReportWithRepeatingHeaderPerPage>(opts =>
{
  opts.OutputFormat = ReportOutputFormat.Pdf;
});

app.Run();
