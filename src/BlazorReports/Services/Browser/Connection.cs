using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
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
  private const int BufferSize = 2 * 1024 * 1024;

  /// <summary>
  /// The uri of the connection
  /// </summary>
  public readonly Uri Uri;

  private readonly JsonSerializerOptions _serializeOptions;

  /// <summary>
  /// The constructor of the connection
  /// </summary>
  /// <param name="uri"> The uri of the connection</param>
  public Connection(Uri uri)
  {
    Uri = uri;
    _webSocket = new ClientWebSocket();
    _serializeOptions = new JsonSerializerOptions
    {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
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
  /// <param name="message"></param>
  /// <param name="responseHandler"></param>
  /// <param name="stoppingToken"> Token to stop the task</param>
  public async ValueTask<TR> SendAsync<T, TR>(BrowserMessage message, Func<T, Task<TR>> responseHandler,
    CancellationToken stoppingToken = default)
  {
    message.Id = ++_lastMessageId;
    if (_webSocket.State != WebSocketState.Open)
    {
      await _webSocket.ConnectAsync(Uri, stoppingToken);
    }

    var buffer = JsonSerializer.SerializeToUtf8Bytes(message, _serializeOptions);
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

      var parsedMessage = JsonSerializer.Deserialize<T>(bufferToReceiveMemory[..result.Count].Span, _serializeOptions);
      if (parsedMessage is null)
        throw new Exception("Could not deserialize response");

      _bufferPool.Return(bufferToReceive);

      return await responseHandler(parsedMessage);
    }

    return default!;
  }

  /// <summary>
  /// Sends a message to the browser
  /// </summary>
  /// <param name="message"></param>
  /// <param name="responseHandler"></param>
  /// <param name="stoppingToken"> Token to stop the task</param>
  public async ValueTask SendAsync<T>(BrowserMessage message, Func<T, Task> responseHandler,
    CancellationToken stoppingToken = default)
  {
    message.Id = ++_lastMessageId;
    if (_webSocket.State != WebSocketState.Open)
    {
      await _webSocket.ConnectAsync(Uri, stoppingToken);
    }

    var buffer = JsonSerializer.SerializeToUtf8Bytes(message, _serializeOptions);
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

      var parsedMessage = JsonSerializer.Deserialize<T>(bufferToReceiveMemory[..result.Count].Span, _serializeOptions);
      if (parsedMessage is null)
        throw new Exception("Could not deserialize response");

      _bufferPool.Return(bufferToReceive);
      await responseHandler(parsedMessage);
      break;
    }
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

    var buffer = JsonSerializer.SerializeToUtf8Bytes(message, _serializeOptions);
    await _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, stoppingToken);
  }

  /// <summary>
  /// Receives a message from the browser
  /// </summary>
  /// <param name="messageHandler"> The message handler</param>
  /// <param name="stoppingToken"> Token to stop the task</param>
  public async ValueTask ReceiveAsync(Action<string, Memory<byte>> messageHandler, CancellationToken stoppingToken = default)
  {
    if (_webSocket.State != WebSocketState.Open)
    {
      await _webSocket.ConnectAsync(Uri, CancellationToken.None);
    }

    var buffer = new Memory<byte>(new byte[BufferSize]);

    while (_webSocket.State == WebSocketState.Open)
    {
      if (stoppingToken.IsCancellationRequested)
      {
        break;
      }

      var result = await _webSocket.ReceiveAsync(buffer, stoppingToken);

      if (result.MessageType == WebSocketMessageType.Close)
      {
        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", stoppingToken);
        break;
      }

      var message = buffer[..result.Count];
      var jsonDoc = JsonDocument.Parse(message);
      var root = jsonDoc.RootElement;
      if (root.TryGetProperty("method", out var methodElement))
      {
        var method = methodElement.GetString() ?? string.Empty;
        messageHandler(method, buffer[..result.Count]);
      }
    }
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
