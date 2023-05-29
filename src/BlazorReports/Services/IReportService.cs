using BlazorReports.Models;
using Microsoft.AspNetCore.Components;

namespace BlazorReports.Services;

public interface IReportService
{
  Task<MemoryStream> GenerateReport<T, TD>(TD data) where T : ComponentBase where TD : class;
  Task<MemoryStream> GenerateReport<T>(BlazorReport blazorReport, T data) where T : class;
  Task<MemoryStream> GenerateReport(BlazorReport blazorReport);
  BlazorReport? GetReportByName(string name);
}
