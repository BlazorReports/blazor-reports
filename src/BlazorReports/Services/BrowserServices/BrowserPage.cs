using System.Buffers;
using System.IO.Pipelines;
using System.Security.Cryptography;
using System.Text;
using BlazorReports.Enums;
using BlazorReports.Models;
using BlazorReports.Services.BrowserServices.Requests;
using BlazorReports.Services.BrowserServices.Responses;

namespace BlazorReports.Services.BrowserServices;

/// <summary>
/// Represents a page in the browser
/// </summary>
public sealed class BrowserPage : IAsyncDisposable
{
  /// <summary>
  /// The id of the page in the browser
  /// </summary>
  internal readonly string TargetId;
  private readonly Connection _connection;
  private readonly CustomFromBase64Transform _transform;

  /// <summary>
  /// Creates a new instance of the BrowserPage
  /// </summary>
  /// <param name="targetId"> The id of the page in the browser</param>
  /// <param name="uri"> The uri of the page</param>
  /// <param name="browserOptions"> The browser options</param>
  public BrowserPage(string targetId, Uri uri, BlazorReportsBrowserOptions browserOptions)
  {
    TargetId = targetId;
    _connection = new Connection(uri, browserOptions.ResponseTimeout);
    _transform = new CustomFromBase64Transform(FromBase64TransformMode.IgnoreWhiteSpaces);
  }

  /// <summary>
  /// Initializes the connection to the browser
  /// </summary>
  /// <param name="stoppingToken"></param>
  /// <returns></returns>
  public async Task InitializeAsync(CancellationToken stoppingToken = default)
  {
    await _connection.InitializeAsync(stoppingToken);
  }

  /// <summary>
  /// Displays the HTML in the browser
  /// </summary>
  /// <param name="html"> HTML to display</param>
  /// <param name="stoppingToken"> Cancellation token</param>
  /// <exception cref="ArgumentException"> Thrown when the HTML is null or whitespace</exception>
  internal async Task DisplayHtml(string html, CancellationToken stoppingToken = default)
  {
    await _connection.ConnectAsync(stoppingToken);
    if (string.IsNullOrWhiteSpace(html))
      throw new ArgumentException("Value cannot be null or whitespace.", nameof(html));

    // Enables or disables the cache
    var cacheMessage = new BrowserMessage("Network.setCacheDisabled");
    cacheMessage.Parameters.Add("cacheDisabled", false);
    _connection.SendAsync(cacheMessage);

    var getPageFrameTreeMessage = new BrowserMessage("Page.getFrameTree");
    await _connection.SendAsync(
      getPageFrameTreeMessage,
      PageGetFrameTreeResponseSerializationContext
        .Default
        .BrowserResultResponsePageGetFrameTreeResponse,
      response =>
      {
        var pageSetDocumentContentMessage = new BrowserMessage("Page.setDocumentContent");
        pageSetDocumentContentMessage.Parameters.Add("frameId", response.Result.FrameTree.Frame.Id);
        pageSetDocumentContentMessage.Parameters.Add("html", html);
        _connection.SendAsync(pageSetDocumentContentMessage);
      },
      stoppingToken
    );
  }

  internal async ValueTask ConvertPageToPdf(
    PipeWriter pipeWriter,
    BlazorReportsPageSettings pageSettings,
    CancellationToken stoppingToken = default
  )
  {
    await _connection.ConnectAsync(stoppingToken);
    var message = CreatePrintToPdfBrowserMessage(pageSettings);

    await _connection.SendAsync(
      message,
      PagePrintToPdfResponseSerializationContext
        .Default
        .BrowserResultResponsePagePrintToPdfResponse,
      async pagePrintToPdfResponse =>
      {
        if (string.IsNullOrEmpty(pagePrintToPdfResponse.Result.Stream))
          return;

        var ioReadMessage = new BrowserMessage("IO.read");
        ioReadMessage.Parameters.Add("handle", pagePrintToPdfResponse.Result.Stream);
        ioReadMessage.Parameters.Add("size", 50 * 1024);

        var finished = false;
        while (true)
        {
          if (finished)
            break;
          await _connection.SendAsync(
            ioReadMessage,
            IoReadResponseSerializationContext.Default.BrowserResultResponseIoReadResponse,
            async ioReadResponse =>
            {
              if (ioReadResponse.Result.Eof)
              {
                await ClosePdfStream(pagePrintToPdfResponse.Result.Stream, stoppingToken);
                finished = true;
                return;
              }

              await ReadAndTransform(
                ioReadResponse.Result.Data.AsMemory(),
                pipeWriter,
                stoppingToken
              );
            },
            stoppingToken
          );
        }

        // Notify the PipeReader that there is no more data to be written
        await pipeWriter.CompleteAsync();
      },
      stoppingToken
    );
  }

  private static BrowserMessage CreatePrintToPdfBrowserMessage(
    BlazorReportsPageSettings pageSettings
  )
  {
    var message = new BrowserMessage("Page.printToPDF");
    message.Parameters.Add(
      "landscape",
      pageSettings.Orientation == BlazorReportsPageOrientation.Landscape
    );
    message.Parameters.Add("paperHeight", pageSettings.PaperHeight);
    message.Parameters.Add("paperWidth", pageSettings.PaperWidth);
    message.Parameters.Add("marginTop", pageSettings.MarginTop);
    message.Parameters.Add("marginBottom", pageSettings.MarginBottom);
    message.Parameters.Add("marginLeft", pageSettings.MarginLeft);
    message.Parameters.Add("marginRight", pageSettings.MarginRight);
    message.Parameters.Add("printBackground", !pageSettings.IgnoreBackground);
    message.Parameters.Add("transferMode", "ReturnAsStream");

    return message;
  }

  private async ValueTask ReadAndTransform(
    ReadOnlyMemory<char> data,
    PipeWriter writer,
    CancellationToken stoppingToken
  )
  {
    if (data.Length == 0)
      return;
    var sharedPool = ArrayPool<byte>.Shared;
    var dataBytes = sharedPool.Rent(data.Length);
    var inputBlock = sharedPool.Rent(CustomFromBase64Transform.InputBlockSize);

    try
    {
      var totalBytes = Encoding.UTF8.GetBytes(data.Span, dataBytes);
      var index = 0;
      var writerBuffer = writer.GetMemory(CustomFromBase64Transform.OutputBlockSize);
      var dataBytesSpan = dataBytes.AsMemory();

      while (index < totalBytes)
      {
        stoppingToken.ThrowIfCancellationRequested();
        var bytesRead = Math.Min(totalBytes - index, CustomFromBase64Transform.InputBlockSize);
        var inputBlockMemory = dataBytesSpan.Slice(index, bytesRead);
        index += bytesRead;

        var count = _transform.TransformBlock(
          inputBlockMemory.Span,
          0,
          bytesRead,
          writerBuffer.Span,
          0
        );
        writer.Advance(count);
        var flushResult = await writer.FlushAsync(stoppingToken);
        if (flushResult.IsCanceled || flushResult.IsCompleted)
          break;
        writerBuffer = writer.GetMemory(count); // Get a new buffer after advancing
      }
    }
    finally
    {
      sharedPool.Return(dataBytes);
      sharedPool.Return(inputBlock);
      _transform.Reset();
    }
  }

  private async ValueTask ClosePdfStream(string stream, CancellationToken stoppingToken = default)
  {
    await _connection.ConnectAsync(stoppingToken);
    var ioCloseMessage = new BrowserMessage("IO.close");
    ioCloseMessage.Parameters.Add("handle", stream);
    _connection.SendAsync(ioCloseMessage);
  }

  /// <summary>
  /// Disposes the BrowserPage
  /// </summary>
  public async ValueTask DisposeAsync()
  {
    await _connection.DisposeAsync();
    _transform.Dispose();
  }
}
