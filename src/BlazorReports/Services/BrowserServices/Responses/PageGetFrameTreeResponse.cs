using System.Text.Json.Serialization;
using BlazorReports.Services.BrowserServices.Types;

namespace BlazorReports.Services.BrowserServices.Responses;

/// <summary>
/// Response returned from the browser request page.getFrameTree
/// </summary>
/// <param name="FrameTree"> Present frame tree structure. </param>
public sealed record PageGetFrameTreeResponse(BrowserFrameTree FrameTree);

/// <summary>
/// The serialization context for the PageGetFrameTreeResponse
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BrowserResultResponse<PageGetFrameTreeResponse>))]
internal sealed partial class PageGetFrameTreeResponseSerializationContext : JsonSerializerContext;
