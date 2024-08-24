using BlazorReports.Extensions;
using BlazorReports.Models;
using SimpleReportServer;

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

app.MapGroup("reports").MapBlazorReport<HelloReport, HelloReportData>();
app.MapGroup("reports")
  .MapBlazorReport<HelloReport, HelloReportData>(opts =>
  {
    opts.ReportName = "HelloReportHtml";
    opts.OutputFormat = ReportOutputFormat.Html;
  });

app.Run();
