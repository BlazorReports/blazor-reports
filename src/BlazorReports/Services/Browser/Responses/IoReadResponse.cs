namespace BlazorReports.Services.Browser.Responses;

/// <summary>
/// Response returned from the browser request Io.read
/// Reference: https://chromedevtools.github.io/devtools-protocol/tot/IO/#method-read
/// </summary>
/// <param name="Base64Encoded"> Set if the data is base64-encoded</param>
/// <param name="Data"> Data that were read.</param>
/// <param name="Eof"> Set if the end-of-file condition occured while reading.</param>
public record IoReadResponse
(
  bool Base64Encoded,
  string Data,
  bool Eof
);
