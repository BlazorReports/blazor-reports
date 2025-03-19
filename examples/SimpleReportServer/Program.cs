using BlazorReports.Extensions;
using BlazorReports.Models;
using ExampleTemplates.Reports;
using SimpleReportServer;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddBlazorReports(opts =>
{
  opts.BrowserOptions.ResponseTimeout = TimeSpan.FromSeconds(90);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}
var reportsGroup = app.MapGroup("reports");

// Report With Data
reportsGroup.MapBlazorReport<HelloReport, HelloReportData>();

// Report that returns Html
reportsGroup.MapBlazorReport<HelloReport, HelloReportData>(opts =>
{
  opts.ReportName = "HelloReportHtml";
  opts.OutputFormat = ReportOutputFormat.Html;
});

// Report that repeats the header per each page
reportsGroup.MapBlazorReport<ReportWithRepeatingHeaderPerPage>(opts =>
{
  opts.OutputFormat = ReportOutputFormat.Pdf;
});

app.Run();
