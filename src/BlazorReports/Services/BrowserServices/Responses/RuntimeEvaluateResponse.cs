using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorReports.Services.BrowserServices.Responses;
/// <summary>
/// Response returned from the "Runtime.evaluate" DevTools command
/// </summary>
/// <param name="Result">The evaluation result object if the command succeeded</param>
/// <param name="ExceptionDetails">Details about any exception thrown in JS</param>
/// <param name="WasThrown">Whether an exception was thrown</param>
public sealed record RuntimeEvaluateResponse(
    [property: JsonPropertyName("result")] RuntimeEvaluateResult? Result,
    [property: JsonPropertyName("exceptionDetails")] RuntimeEvaluateExceptionDetails? ExceptionDetails,
    [property: JsonPropertyName("wasThrown")] bool WasThrown
);

/// <summary>
/// Contains the result of a "Runtime.evaluate" call
/// </summary>
/// <param name="Type">The type of the result (e.g., "string", "object", "undefined")</param>
/// <param name="Value">The raw JSON value (if <c>returnByValue</c> was used)</param>
/// <param name="Description">A textual description (especially for objects/functions)</param>
public sealed record RuntimeEvaluateResult(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("value")] JsonElement? Value,
    [property: JsonPropertyName("description")] string? Description
);

/// <summary>
/// Contains exception details if JS threw an error
/// </summary>
/// <param name="Text">The error message text</param>
public sealed record RuntimeEvaluateExceptionDetails(
    [property: JsonPropertyName("text")] string? Text
);

/// <summary>
/// JSON serialization context for the "Runtime.evaluate" DevTools command
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BrowserResultResponse<RuntimeEvaluateResponse>))]
internal sealed partial class RuntimeEvaluateResponseSerializationContext : JsonSerializerContext;
