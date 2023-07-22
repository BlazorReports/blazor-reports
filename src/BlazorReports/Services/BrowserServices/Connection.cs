using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using BlazorReports.Services.BrowserServices.Requests;

namespace BlazorReports.Services.BrowserServices;

/// <summary>
/// Represents a connection to the browser
/// </summary>
internal sealed class Connection : IAsyncDisposable
{
  private ClientWebSocket _webSocket = new();
  private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
  private readonly SemaphoreSlim _sendSignal = new(0);
  private readonly ConcurrentQueue<BrowserMessage> _sendQueue = new();
  private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonDocument>> _responseTasks =
    new();
  private const int BufferSize = 100 * 1024;
  private int _lastMessageId;
  private Task? _sendTask;
  private Task? _receiveTask;
  private readonly SemaphoreSlim _connectionLock = new(1, 1);
  private readonly CancellationTokenSource _cts = new();
  private readonly TimeSpan _responseTimeout;

  /// <summary>
  /// The uri of the connection
  /// </summary>
  public readonly Uri Uri;

  /// <summary>
  /// The constructor of the connection
  /// </summary>
  /// <param name="uri"> The uri of the connection</param>
  /// <param name="responseTimeout"> The response timeout</param>
  public Connection(Uri uri, TimeSpan responseTimeout)
  {
    Uri = uri;
    _responseTimeout = responseTimeout;
  }

  public async Task InitializeAsync(CancellationToken stoppingToken = default)
  {
    await _connectionLock.WaitAsync(stoppingToken);
    try
    {
      if (_webSocket.State is not WebSocketState.None)
        return;

      await _webSocket.ConnectAsync(Uri, stoppingToken);

      _sendTask = ProcessSendQueueAsync();
      _receiveTask = ProcessResponsesAsync();
    }
    finally
    {
      _connectionLock.Release();
    }
  }

  /// <summary>
  /// Connects to the browser
  /// </summary>
  public async ValueTask ConnectAsync(CancellationToken stoppingToken = default)
  {
    if (_webSocket.State is WebSocketState.Open)
      return;

    await _connectionLock.WaitAsync(stoppingToken);

    try
    {
      await _webSocket.ConnectAsync(Uri, stoppingToken);
    }
    catch (Exception)
    {
      _webSocket.Dispose();
      _webSocket = new ClientWebSocket();
      await _webSocket.ConnectAsync(Uri, stoppingToken);
    }
    finally
    {
      _connectionLock.Release();
    }
  }

  private async Task ProcessSendQueueAsync()
  {
    var bufferToSend = _bufferPool.Rent(BufferSize);
    var bufferToSendMemory = new Memory<byte>(bufferToSend);

    try
    {
      while (!_cts.IsCancellationRequested)
      {
        await _sendSignal.WaitAsync(_cts.Token);

        if (!_sendQueue.TryDequeue(out var message))
          continue;

        var buffer = JsonSerializer.SerializeToUtf8Bytes(
          message,
          BrowserMessageSerializationContext.Default.BrowserMessage
        );
        buffer.CopyTo(bufferToSendMemory);
        await _webSocket.SendAsync(
          bufferToSendMemory[..buffer.Length],
          WebSocketMessageType.Text,
          true,
          _cts.Token
        );
      }
    }
    catch (OperationCanceledException)
    {
      // Log or handle the fact that the operation was cancelled
      Debug.WriteLine("Operation was cancelled.");
    }
    finally
    {
      _bufferPool.Return(bufferToSend);
    }
  }

  private async Task ProcessResponsesAsync()
  {
    var bufferToReceive = _bufferPool.Rent(BufferSize);
    var bufferToReceiveMemory = new Memory<byte>(bufferToReceive);

    try
    {
      while (!_cts.IsCancellationRequested)
      {
        var result = await _webSocket.ReceiveAsync(bufferToReceiveMemory, _cts.Token);

        var messageReceived = bufferToReceiveMemory[..result.Count];
        var jsonDoc = JsonDocument.Parse(messageReceived);
        var root = jsonDoc.RootElement;
        if (!root.TryGetProperty("id", out var methodElement))
          continue;

        var id = methodElement.GetInt32();

        if (_responseTasks.TryRemove(id, out var taskSource))
        {
          taskSource.SetResult(jsonDoc);
        }
      }
    }
    catch (OperationCanceledException)
    {
      // Log or handle the fact that the operation was cancelled
      Debug.WriteLine("Operation was cancelled.");
    }
    catch (WebSocketException ex)
    {
      Debug.WriteLine(ex.Message);
    }
    catch (JsonException ex)
    {
      Debug.WriteLine(ex.Message);
    }
    catch (Exception ex)
    {
      Debug.WriteLine(ex.Message);
    }
    finally
    {
      _bufferPool.Return(bufferToReceive);
    }
  }

  private async Task<JsonDocument> SendMessageAsync(
    BrowserMessage message,
    CancellationToken stoppingToken
  )
  {
    message.Id = Interlocked.Increment(ref _lastMessageId);
    _sendQueue.Enqueue(message);
    _sendSignal.Release();

    var tcs = new TaskCompletionSource<JsonDocument>();
    _responseTasks[message.Id] = tcs;

    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
    timeoutCts.CancelAfter(_responseTimeout);

    if (await Task.WhenAny(tcs.Task, Task.Delay(-1, timeoutCts.Token)) == tcs.Task)
    {
      return await tcs.Task;
    }
    else
    {
      _responseTasks.TryRemove(message.Id, out _);
      throw new TimeoutException("The operation timed out.");
    }
  }

  public async ValueTask<T> SendAsync<T>(
    BrowserMessage message,
    JsonTypeInfo<T> returnDataJsonTypeInfo,
    CancellationToken stoppingToken = default
  )
  {
    var response = await SendMessageAsync(message, stoppingToken);
    var parsedMessage = response.RootElement.Deserialize(returnDataJsonTypeInfo);

    if (parsedMessage is null)
      throw new Exception("Could not deserialize response");

    return parsedMessage;
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
    var response = await SendMessageAsync(message, stoppingToken);
    var parsedMessage = response.RootElement.Deserialize(returnDataJsonTypeInfo);

    if (parsedMessage is null)
      throw new Exception("Could not deserialize response");

    return await responseHandler(parsedMessage);
  }

  /// <summary>
  /// Sends a message to the browser
  /// </summary>
  /// <param name="message"> The message to send</param>
  /// <param name="returnDataJsonTypeInfo"> The json type info of the return data</param>
  /// <param name="responseAction"> The response action</param>
  /// <param name="stoppingToken"> Token to stop the task</param>
  public async ValueTask SendAsync<T>(
    BrowserMessage message,
    JsonTypeInfo<T> returnDataJsonTypeInfo,
    Action<T> responseAction,
    CancellationToken stoppingToken = default
  )
  {
    var response = await SendMessageAsync(message, stoppingToken);
    var parsedMessage = response.RootElement.Deserialize(returnDataJsonTypeInfo);

    if (parsedMessage is null)
      throw new Exception("Could not deserialize response");

    responseAction(parsedMessage);
  }

  public void SendAsync(BrowserMessage message)
  {
    message.Id = Interlocked.Increment(ref _lastMessageId);
    _sendQueue.Enqueue(message);
    _sendSignal.Release();
  }

  public async ValueTask SendAsync<T>(
    BrowserMessage message,
    JsonTypeInfo<T> returnDataJsonTypeInfo,
    Func<T, Task> responseAction,
    CancellationToken stoppingToken = default
  )
  {
    message.Id = Interlocked.Increment(ref _lastMessageId);
    _sendQueue.Enqueue(message);
    _sendSignal.Release();

    var tcs = new TaskCompletionSource<JsonDocument>();
    _responseTasks[message.Id] = tcs;

    if (await Task.WhenAny(tcs.Task, Task.Delay(_responseTimeout, stoppingToken)) == tcs.Task)
    {
      var response = await tcs.Task;
      var parsedMessage = response.RootElement.Deserialize(returnDataJsonTypeInfo);

      if (parsedMessage is null)
        throw new Exception("Could not deserialize response");

      await responseAction(parsedMessage);
    }
    else
    {
      _responseTasks.TryRemove(message.Id, out _);
      throw new TimeoutException("The operation timed out.");
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

  public async ValueTask DisposeAsync()
  {
    await _cts.CancelAsync();

    if (_sendTask is not null)
      await _sendTask;
    if (_receiveTask is not null)
      await _receiveTask;

    _webSocket.Dispose();
    _sendSignal.Dispose();
    _responseTasks.Clear();
    _connectionLock.Dispose();
  }
}
