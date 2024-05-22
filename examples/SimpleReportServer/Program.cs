using BlazorReports.Extensions;
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

app.Run();
