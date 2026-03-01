using System.Net;
using System.Text.RegularExpressions;
using Ticket.Interfaces.Infrastructure;

namespace Ticket.Services.Infrastructure;

public class HtmlContentSanitizer : IContentSanitizer
{
    private static readonly Regex ScriptRegex = new("<script.*?>.*?</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex EventAttrRegex = new("on\\w+\\s*=\\s*\".*?\"", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public string Sanitize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var sanitized = ScriptRegex.Replace(input, string.Empty);
        sanitized = EventAttrRegex.Replace(sanitized, string.Empty);
        sanitized = sanitized.Trim();
        return WebUtility.HtmlEncode(sanitized);
    }
}
