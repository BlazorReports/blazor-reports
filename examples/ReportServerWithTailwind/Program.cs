using BlazorReports.Extensions;
using BlazorReports.Models;
using ExampleTemplates.Dtos;
using ExampleTemplates.Reports;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBlazorReports(opts => { });

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
}

var reportsGroup = app.MapGroup("reports");

// Report that includes an ECharts chart
reportsGroup.MapBlazorReport<MyChartComponent, List<ChartDataItem>>(opts =>
{
  opts.AssetsPath = "wwwroot/js";
  opts.OutputFormat = ReportOutputFormat.Pdf;
});

app.Run();
