namespace BlazorReports.Services.BrowserServices.Types;

/// <summary>
///
/// </summary>
/// <param name="Type"></param>
/// <param name="Subtype"></param>
/// <param name="ClassName"></param>
/// <param name="Value"></param>
/// <typeparam name="T"></typeparam>
public sealed record RemoteObject<T>(string Type, string Subtype, string ClassName, T Value);
