namespace BlazorReports.Services.Browser.Responses;

/// <summary>
/// Response returned from the createTarget request
/// Reference: https://chromedevtools.github.io/devtools-protocol/tot/Target/#method-createTarget
/// </summary>
/// <param name="TargetId"> The id of the page opened.</param>
public record CreateTargetResponse
(
  string TargetId
);
