using System.Text.Json.Serialization;

namespace BlazorReports.Services.BrowserServices.Responses;

/// <summary>
/// Response returned from the createTarget request
/// Reference: https://chromedevtools.github.io/devtools-protocol/tot/Target/#method-createTarget
/// </summary>
/// <param name="TargetId"> The id of the page opened.</param>
public record CreateTargetResponse(string TargetId);

/// <summary>
/// The serialization context for the CreateTargetResponse
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BrowserResultResponse<CreateTargetResponse>))]
internal partial class CreateTargetResponseSerializationContext : JsonSerializerContext { }
