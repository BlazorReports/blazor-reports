using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using BlazorReports.Services.Browser.Requests;

namespace BlazorReports.Services.Browser;

/// <summary>
/// Represents a connection to the browser
/// </summary>
internal sealed class Connection
{
  private int _lastMessageId;
  private readonly ClientWebSocket _webSocket;
  private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
  private const int BufferSize = 100 * 1024;

  /// <summary>
  /// The uri of the connection
  /// </summary>
  public readonly Uri Uri;

  /// <summary>
  /// The constructor of the connection
  /// </summary>
  /// <param name="uri"> The uri of the connection</param>
  public Connection(Uri uri)
  {
    Uri = uri;
    _webSocket = new ClientWebSocket();
  }

  /// <summary>
  /// Connects to the browser
  /// </summary>
  public async ValueTask ConnectAsync(CancellationToken stoppingToken = default)
  {
    if (_webSocket.State != WebSocketState.Open)
    {
      await _webSocket.ConnectAsync(Uri, stoppingToken);
    }
  }

  /// <summary>
  /// Sends a message to the browser
  /// </summary>
  /// <param name="message"> The message to send</param>
  /// <param name="returnDataJsonTypeInfo"> The json type info of the return data</param>
  /// <param name="responseHandler"> The response handler</param>
  /// <param name="stoppingToken"> Token to stop the task</param>
  public async ValueTask<TR> SendAsync<T, TR>(
    BrowserMessage message,
    JsonTypeInfo<T> returnDataJsonTypeInfo,
    Func<T, Task<TR>> responseHandler,
    CancellationToken stoppingToken = default
  )
  {
    message.Id = ++_lastMessageId;
    if (_webSocket.State != WebSocketState.Open)
    {
      await _webSocket.ConnectAsync(Uri, stoppingToken);
    }

    var buffer = JsonSerializer.SerializeToUtf8Bytes(message, BrowserMessageSerializationContext.Default.BrowserMessage);
    await _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, stoppingToken);

    var bufferToReceive = _bufferPool.Rent(BufferSize);
    var bufferToReceiveMemory = new Memory<byte>(bufferToReceive);

    while (_webSocket.State == WebSocketState.Open)
    {
      if (stoppingToken.IsCancellationRequested)
      {
        break;
      }

      var result = await _webSocket.ReceiveAsync(bufferToReceiveMemory, stoppingToken);

      if (result.MessageType == WebSocketMessageType.Close)
      {
        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", stoppingToken);
        break;
      }

      var messageReceived = bufferToReceiveMemory[..result.Count];
      var jsonDoc = JsonDocument.Parse(messageReceived);
      var root = jsonDoc.RootElement;
      if (!root.TryGetProperty("id", out var methodElement)) continue;

      var id = methodElement.GetInt32();
      if (id != message.Id) continue;

      var parsedMessage = JsonSerializer.Deserialize(bufferToReceiveMemory[..result.Count].Span, returnDataJsonTypeInfo);
      if (parsedMessage is null)
        throw new Exception("Could not deserialize response");


      return await responseHandler(parsedMessage);
    }
    _bufferPool.Return(bufferToReceive);

    return default!;
  }

  /// <summary>
  /// Sends a message to the browser
  /// </summary>
  /// <param name="message"> The message to send</param>
  /// <param name="returnDataJsonTypeInfo"> The json type info of the return data</param>
  /// <param name="responseHandler"> The response handler</param>
  /// <param name="stoppingToken"> Token to stop the task</param>
  public async ValueTask SendAsync<T>(BrowserMessage message,
    JsonTypeInfo<T> returnDataJsonTypeInfo,
    Func<T, Task> responseHandler,
    CancellationToken stoppingToken = default)
  {
    message.Id = ++_lastMessageId;
    if (_webSocket.State != WebSocketState.Open)
    {
      await _webSocket.ConnectAsync(Uri, stoppingToken);
    }

    var buffer = JsonSerializer.SerializeToUtf8Bytes(message, BrowserMessageSerializationContext.Default.BrowserMessage);
    await _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, stoppingToken);
    var bufferToReceive = _bufferPool.Rent(BufferSize);
    var bufferToReceiveMemory = new Memory<byte>(bufferToReceive);

    while (_webSocket.State == WebSocketState.Open)
    {
      if (stoppingToken.IsCancellationRequested)
      {
        break;
      }

      var result = await _webSocket.ReceiveAsync(bufferToReceiveMemory, stoppingToken);

      if (result.MessageType == WebSocketMessageType.Close)
      {
        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", stoppingToken);
        break;
      }

      var messageReceived = bufferToReceiveMemory[..result.Count];
      var jsonDoc = JsonDocument.Parse(messageReceived);
      var root = jsonDoc.RootElement;
      if (!root.TryGetProperty("id", out var methodElement)) continue;

      var id = methodElement.GetInt32();
      if (id != message.Id) continue;

      var parsedMessage = JsonSerializer.Deserialize(bufferToReceiveMemory[..result.Count].Span, returnDataJsonTypeInfo);
      if (parsedMessage is null)
        throw new Exception("Could not deserialize response");

      await responseHandler(parsedMessage);
      break;
    }
    _bufferPool.Return(bufferToReceive);
  }

  /// <summary>
  /// Sends a message to the browser
  /// </summary>
  /// <param name="message"></param>
  /// <param name="stoppingToken"> Token to stop the task</param>
  public async ValueTask SendAsync(BrowserMessage message, CancellationToken stoppingToken = default)
  {
    message.Id = ++_lastMessageId;
    if (_webSocket.State != WebSocketState.Open)
    {
      await _webSocket.ConnectAsync(Uri, stoppingToken);
    }

    var buffer = JsonSerializer.SerializeToUtf8Bytes(message, BrowserMessageSerializationContext.Default.BrowserMessage);
    await _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, stoppingToken);
  }

  /// <summary>
  /// Closes the connection
  /// </summary>
  /// <param name="stoppingToken"></param>
  public async ValueTask CloseAsync(CancellationToken stoppingToken = default)
  {
    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", stoppingToken);
  }
}
