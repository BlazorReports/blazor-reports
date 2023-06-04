using System.Text.Json.Serialization;
using BlazorReports.Services.Browser.Types;

namespace BlazorReports.Services.Browser.Responses;

/// <summary>
/// Response returned from the browser request page.getFrameTree
/// </summary>
/// <param name="FrameTree"> Present frame tree structure. </param>
public record PageGetFrameTreeResponse
(
  BrowserFrameTree FrameTree
);

/// <summary>
/// The serialization context for the PageGetFrameTreeResponse
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BrowserResultResponse<PageGetFrameTreeResponse>))]
internal partial class PageGetFrameTreeResponseSerializationContext : JsonSerializerContext
{
}
