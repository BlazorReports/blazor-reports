using System.Text.Json.Serialization;

namespace BlazorReports.Services.Browser.Requests;

/// <summary>
/// Message sent to the browser
/// </summary>
internal class BrowserMessage(string Method)
{
  /// <summary>
  /// The id of the message
  /// </summary>
  public int Id { get; set; }
  /// <summary>
  /// The method executed by the browser
  /// </summary>
  public string Method { get; } = Method;
  /// <summary>
  /// The parameters that we want to feed into the browser
  /// </summary>
  [JsonPropertyName("params")]
  public Dictionary<string, object> Parameters { get; } = new();
}

/// <summary>
/// The serialization context for the PageGetFrameTreeResponse
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BrowserMessage))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(double))]
internal partial class BrowserMessageSerializationContext : JsonSerializerContext
{
}
