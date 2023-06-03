using System.Buffers;
using System.IO.Pipelines;
using System.Security.Cryptography;
using System.Text;
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

  internal PipeReader ConvertPageToPdf(BlazorReportsPageSettings pageSettings,
    CancellationToken stoppingToken = default)
  {
    var pipe = new Pipe();

    _ = Task.Run(async () => // running writing to the pipe in background
    {
      await WriteToPipe(pipe.Writer, pageSettings, stoppingToken);
    }, stoppingToken);

    // The reader as a stream can be returned and it will be populated as WriteToPipe executes.
    return pipe.Reader;
  }

  private async Task WriteToPipe(PipeWriter writer, BlazorReportsPageSettings pageSettings,
    CancellationToken stoppingToken = default)
  {
    var message = CreatePrintToPdfBrowserMessage(pageSettings);

    await _connection.SendAsync<BrowserResultResponse<PagePrintToPdfResponse>>(message, async pagePrintToPdfResponse =>
    {
      if (string.IsNullOrEmpty(pagePrintToPdfResponse.Result.Stream)) return;

      var ioReadMessage = new BrowserMessage("IO.read");
      ioReadMessage.Parameters.Add("handle", pagePrintToPdfResponse.Result.Stream);
      ioReadMessage.Parameters.Add("size", 200 * 1024);

      using var transform = new FromBase64Transform(FromBase64TransformMode.IgnoreWhiteSpaces);
      var finished = false;
      while (true)
      {
        if (finished) break;
        await _connection.SendAsync<BrowserResultResponse<IoReadResponse>>(ioReadMessage, async ioReadResponse =>
        {
          if (ioReadResponse.Result.Eof)
          {
            await ClosePdfStream(pagePrintToPdfResponse.Result.Stream, stoppingToken);
            finished = true;
            return;
          }

          await ReadAndTransform(ioReadResponse.Result.Data.AsMemory(), transform, writer, stoppingToken);
        }, stoppingToken);
      }

      // Notify the PipeReader that there is no more data to be written
      await writer.CompleteAsync();
    }, stoppingToken);
  }

  private static BrowserMessage CreatePrintToPdfBrowserMessage(BlazorReportsPageSettings pageSettings)
  {
    var message = new BrowserMessage("Page.printToPDF");
    message.Parameters.Add("landscape", pageSettings.Orientation == BlazorReportsPageOrientation.Landscape);
    message.Parameters.Add("paperHeight", pageSettings.PaperHeight);
    message.Parameters.Add("paperWidth", pageSettings.PaperWidth);
    message.Parameters.Add("marginTop", pageSettings.MarginTop);
    message.Parameters.Add("marginBottom", pageSettings.MarginBottom);
    message.Parameters.Add("marginLeft", pageSettings.MarginLeft);
    message.Parameters.Add("marginRight", pageSettings.MarginRight);
    message.Parameters.Add("transferMode", "ReturnAsStream");

    return message;
  }

  private static async Task ReadAndTransform(ReadOnlyMemory<char> data, FromBase64Transform transform,
    PipeWriter writer, CancellationToken stoppingToken)
  {
    if (data.Length == 0)
      return;
    var sharedPool = ArrayPool<byte>.Shared;
    var dataBytes = sharedPool.Rent(data.Length);
    var inputBlock = sharedPool.Rent(transform.InputBlockSize);
    var output = sharedPool.Rent(transform.OutputBlockSize);

    try
    {
      var totalBytes = Encoding.UTF8.GetBytes(data.Span, dataBytes);
      var index = 0;
      var buffer = writer.GetMemory(transform.OutputBlockSize);

      while (index < totalBytes)
      {
        stoppingToken.ThrowIfCancellationRequested();
        var bytesRead = Math.Min(totalBytes - index, transform.InputBlockSize);
        var inputBlockMemory = new Memory<byte>(dataBytes, index, bytesRead);
        index += bytesRead;

        var count = transform.TransformBlock(inputBlockMemory.Span.ToArray(), 0, bytesRead, output, 0);
        output.AsSpan(0, count).CopyTo(buffer.Span);

        writer.Advance(count);
        var flushResult = await writer.FlushAsync(stoppingToken);
        if (flushResult.IsCanceled || flushResult.IsCompleted)
          break;
        buffer = writer.GetMemory(count); // Get a new buffer after advancing
      }
    }
    finally
    {
      sharedPool.Return(dataBytes);
      sharedPool.Return(inputBlock);
      sharedPool.Return(output);
    }
  }

  private static async Task ReadAndTransformV2(ReadOnlyMemory<char> data, FromBase64Transform transform,
    PipeWriter writer, CancellationToken stoppingToken)
  {
    if (data.Length == 0)
      return;
    var sharedPool = ArrayPool<byte>.Shared;
    var dataBytes = sharedPool.Rent(data.Length);

    try
    {
      Convert.TryFromBase64Chars(data.Span, dataBytes, out var bytesWritten);
      var buffer = writer.GetMemory(bytesWritten);
      dataBytes.AsSpan(0, bytesWritten).CopyTo(buffer.Span);
      writer.Advance(bytesWritten);

      await writer.FlushAsync(stoppingToken);
    }
    finally
    {
      sharedPool.Return(dataBytes);
    }
  }

  private async Task ClosePdfStream(string stream, CancellationToken stoppingToken)
  {
    var ioCloseMessage = new BrowserMessage("IO.close");
    ioCloseMessage.Parameters.Add("handle", stream);
    await _connection.SendAsync(ioCloseMessage, stoppingToken: stoppingToken);
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
