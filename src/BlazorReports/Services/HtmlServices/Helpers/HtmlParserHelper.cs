
using HtmlAgilityPack;

namespace BlazorReports.Services.HtmlServices.Helpers;
/// <summary>
/// 
/// </summary>
internal sealed class HtmlParserHelper
{
  /// <summary>
  /// 
  /// </summary>
  /// <param name="html"></param>
  /// <param name="method"></param>
  /// <param name="classToIgnore"></param>
  /// <returns></returns>
  public static bool IsUsingMethodInScripts(string html, string method, string classToIgnore)
  {
    var htmlDoc = new HtmlDocument();
    htmlDoc.LoadHtml(html);

    var scriptNodes = htmlDoc.DocumentNode.SelectNodes("//script");
    if (scriptNodes == null)
      return false;

    foreach (var scriptNode in scriptNodes)
    {
      // Skip if script has the classToIgnore
      var classAttr = scriptNode.GetAttributeValue("class", "");
      if (!string.IsNullOrEmpty(classAttr) && classAttr.Contains(classToIgnore))
        continue;

      var scriptContent = scriptNode.InnerText;
      if (scriptContent.Contains($"{method}(")) // looks for `.completed(` etc.
        return true;
    }

    return false;
  }
}
