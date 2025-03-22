using System.Text;
using Microsoft.AspNetCore.Hosting;

namespace BlazorReports.Services.JavascriptServices;

internal sealed class JavascriptContainer
{
  /// <summary>
  /// A dictionary of (FileName -> FileContent) for all .js files.
  /// </summary>
  public Dictionary<string, string> Scripts { get; } = new();

  /// <summary>
  /// Scans the wwwroot/js folder (or any folder you specify) and reads
  /// all .js file contents into memory.
  /// </summary>
  public JavascriptContainer(IWebHostEnvironment env)
  {
    // 1. Get the physical path to the wwwroot folder
    var webRootPath = env.WebRootPath;

    // 2. Build path to the "js" subfolder
    var jsDir = Path.Combine(webRootPath, "js");

    if (Directory.Exists(jsDir))
    {
      // 3. Get all *.js files recursively
      var allJsFiles = Directory.GetFiles(jsDir, "*.js", SearchOption.AllDirectories);

      foreach (var filePath in allJsFiles)
      {
        // For example: "C:\MyApp\wwwroot\js\somefolder\file.js"
        // We'll use the file name (e.g. "file.js") or a relative path as key
        var fileName = Path.GetFileName(filePath);

        // 4. Read the file text content
        var content = File.ReadAllText(filePath, Encoding.UTF8);

        // 5. Store in the dictionary
        Scripts[fileName] = content;
      }
    }
  }
}
