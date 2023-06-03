using System.IO.Pipelines;
using BlazorReports.Models;
using Microsoft.AspNetCore.Components;

namespace BlazorReports.Services;

/// <summary>
/// Service for generating reports
/// </summary>
public interface IReportService
{
  /// <summary>
  /// Generates a report using the specified component and data
  /// </summary>
  /// <param name="data"> The data to use in the report </param>
  /// <typeparam name="T"> The component to use in the report </typeparam>
  /// <typeparam name="TD"> The type of data to use in the report </typeparam>
  /// <returns> The generated report </returns>
  Task<PipeReader> GenerateReport<T, TD>(TD data) where T : ComponentBase where TD : class;
  /// <summary>
  /// Generates a report using the specified component and data
  /// </summary>
  /// <param name="blazorReport"> The report to generate </param>
  /// <param name="data"> The data to use in the report </param>
  /// <typeparam name="T"> The type of data to use in the report </typeparam>
  /// <returns> The generated report </returns>
  Task<PipeReader> GenerateReport<T>(BlazorReport blazorReport, T? data) where T : class;
  /// <summary>
  /// Generates a report using the specified component
  /// </summary>
  /// <param name="blazorReport"> The report to generate </param>
  /// <returns> The generated report </returns>
  Task<PipeReader> GenerateReport(BlazorReport blazorReport);
  /// <summary>
  /// Gets a blazor report by name
  /// </summary>
  /// <param name="name"> The name of the report to get </param>
  /// <returns> The blazor report </returns>
  BlazorReport? GetReportByName(string name);
}
