using Microsoft.Extensions.Logging;

namespace BlazorReports.Services.BrowserServices.Factories;

/// <summary>
/// Represents a connection factory
/// </summary>
/// <param name="logger"> The logger</param>
internal sealed class ConnectionFactory(ILogger<Connection> logger) : IConnectionFactory
{
  /// <summary>
  /// Creates a new connection
  /// </summary>
  /// <param name="uri"> The uri of the page</param>
  /// <param name="responseTimeout"> The response timeout</param>
  /// <returns> The connection</returns>
  public async ValueTask<Connection> CreateConnection(Uri uri, TimeSpan responseTimeout)
  {
    var connection = new Connection(uri, responseTimeout, logger);
    await connection.InitializeAsync();
    return connection;
  }
}
