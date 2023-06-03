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
