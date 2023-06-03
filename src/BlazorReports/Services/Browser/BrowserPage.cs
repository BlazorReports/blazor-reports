using System.Buffers;
using BlazorReports.Enums;
using BlazorReports.Models;
using BlazorReports.Services.Browser.Requests;
using BlazorReports.Services.Browser.Responses;

namespace BlazorReports.Services.Browser;

/// <summary>
/// Represents a page in the browser
/// </summary>
public sealed class BrowserPage
{
  private readonly Connection _connection;
  private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;

  /// <summary>
  /// Creates a new instance of the BrowserPage
  /// </summary>
  /// <param name="uri"></param>
  public BrowserPage(Uri uri)
  {
    _connection = new Connection(uri);
  }


  /// <summary>
  /// Displays the HTML in the browser
  /// </summary>
  /// <param name="html"> HTML to display</param>
  /// <param name="stoppingToken"> Cancellation token</param>
  /// <exception cref="ArgumentException"> Thrown when the HTML is null or whitespace</exception>
  internal async Task DisplayHtml(string html, CancellationToken stoppingToken = default)
  {
    if (string.IsNullOrWhiteSpace(html))
      throw new ArgumentException("Value cannot be null or whitespace.", nameof(html));

    // var pageNavigateMessage = new BrowserMessage("Page.navigate");
    // pageNavigateMessage.Parameters.Add("url", $"data:text/html,{html}");
    // await _connection.SendAsync(pageNavigateMessage, stoppingToken: stoppingToken);

    // Enables or disables the cache
    var cacheMessage = new BrowserMessage("Network.setCacheDisabled");
    cacheMessage.Parameters.Add("cacheDisabled", false);
    await _connection.SendAsync(cacheMessage, stoppingToken: stoppingToken);

    // Enables page domain notifications
    var pageEnableMessage = new BrowserMessage("Page.enable");
    await _connection.SendAsync(pageEnableMessage, stoppingToken: stoppingToken);

    // Enables network domain notifications
    var setLifecycleEventsEnabledMessage = new BrowserMessage("Page.setLifecycleEventsEnabled");
    setLifecycleEventsEnabledMessage.Parameters.Add("enabled", true);
    await _connection.SendAsync(setLifecycleEventsEnabledMessage, stoppingToken: stoppingToken);

    var getPageFrameTreeMessage = new BrowserMessage("Page.getFrameTree");
    await _connection.SendAsync<BrowserResultResponse<PageGetFrameTreeResponse>>(getPageFrameTreeMessage,
      async response =>
      {
        var pageSetDocumentContentMessage = new BrowserMessage("Page.setDocumentContent");
        pageSetDocumentContentMessage.Parameters.Add("frameId", response.Result.FrameTree.Frame.Id);
        pageSetDocumentContentMessage.Parameters.Add("html", html);
        await _connection.SendAsync(pageSetDocumentContentMessage, stoppingToken: stoppingToken);

        setLifecycleEventsEnabledMessage.Parameters.Clear();
        setLifecycleEventsEnabledMessage.Parameters.Add("enabled", false);
        await _connection.SendAsync(setLifecycleEventsEnabledMessage, stoppingToken: stoppingToken);

        // Disables page domain notifications
        var pageDisableMessage = new BrowserMessage("Page.disable");
        await _connection.SendAsync(pageDisableMessage, stoppingToken: stoppingToken);
      }, stoppingToken);
  }

  /// <summary>
  /// Converts the current page to PDF
  /// </summary>
  /// <param name="outputStream"> Stream to write the PDF</param>
  /// <param name="pageSettings"> Page settings</param>
  /// <param name="stoppingToken"> Cancellation token</param>
  /// <exception cref="ArgumentException"> Thrown when the output stream is not writable</exception>
  internal async Task ConvertPageToPdf(Stream outputStream, BlazorReportsPageSettings pageSettings,
    CancellationToken stoppingToken = default)
  {
    if (!outputStream.CanWrite)
      throw new ArgumentException("The output stream is not writable", nameof(outputStream));

    var message = new BrowserMessage("Page.printToPDF");
    message.Parameters.Add("landscape", pageSettings.Orientation == BlazorReportsPageOrientation.Landscape);
    message.Parameters.Add("paperHeight", pageSettings.PaperHeight);
    message.Parameters.Add("paperWidth", pageSettings.PaperWidth);
    message.Parameters.Add("marginTop", pageSettings.MarginTop);
    message.Parameters.Add("marginBottom", pageSettings.MarginBottom);
    message.Parameters.Add("marginLeft", pageSettings.MarginLeft);
    message.Parameters.Add("marginRight", pageSettings.MarginRight);
    message.Parameters.Add("transferMode", "ReturnAsStream");

    await _connection.SendAsync<BrowserResultResponse<PagePrintToPdfResponse>>(message, async pagePrintToPdfResponse =>
    {
      if (string.IsNullOrEmpty(pagePrintToPdfResponse.Result.Stream)) return;

      var ioReadMessage = new BrowserMessage("IO.read");
      ioReadMessage.Parameters.Add("handle", pagePrintToPdfResponse.Result.Stream);
      ioReadMessage.Parameters.Add("size", 1 * 1024 * 1024); // Get the pdf in chunks of 1MB

      outputStream.Position = 0;
      var finished = false;
      // ReSharper disable once LoopVariableIsNeverChangedInsideLoop (It is changed inside the lambda)
      while (!finished)
      {
        await _connection.SendAsync<BrowserResultResponse<IoReadResponse>>(ioReadMessage, async ioReadResponse =>
        {
          // Use a large enough buffer. The maximum possible size can be calculated.
          var bufferSize = (int) Math.Ceiling(ioReadResponse.Result.Data.Length / 4.0) * 3;
          var buffer = _bufferPool.Rent(bufferSize);
          if (Convert.TryFromBase64Chars(ioReadResponse.Result.Data, buffer, out var bytesWritten))
          {
            if (bytesWritten > 0)
              await outputStream.WriteAsync(buffer.AsMemory(0, bytesWritten), stoppingToken);
          }
          else
          {
            throw new Exception("Conversion failed");
          }

          _bufferPool.Return(buffer);

          if (!ioReadResponse.Result.Eof)
          {
            return;
          }

          var ioCloseMessage = new BrowserMessage("IO.close");
          ioCloseMessage.Parameters.Add("handle", pagePrintToPdfResponse.Result.Stream);
          await _connection.SendAsync(ioCloseMessage, stoppingToken: stoppingToken);
          finished = true;
        }, stoppingToken);
      }
    }, stoppingToken);
  }

  /// <summary>
  /// Closes the connection to the browser
  /// </summary>
  /// <returns></returns>
  public ValueTask CloseConnection()
  {
    return _connection.CloseAsync();
  }
}
