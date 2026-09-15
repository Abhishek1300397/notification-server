using System.Text.RegularExpressions;
using Notification.Application.Abstractions.Notifications;
using Notification.Domain.Entities;

namespace Notification.Application.Notifications;

public sealed class SimpleTemplateRenderer : ITemplateRenderer
{
    private static readonly Regex Placeholder = new(@"\{\{\s*(?<key>[^}]+?)\s*\}\}", RegexOptions.Compiled);

    public RenderedTemplate Render(
        NotificationTemplate template,
        IReadOnlyDictionary<string, string> data)
    {
        return new RenderedTemplate(
            Replace(template.Subject, data),
            Replace(template.Body, data));
    }

    private static string Replace(string input, IReadOnlyDictionary<string, string> data)
    {
        return Placeholder.Replace(input, match =>
        {
            var key = match.Groups["key"].Value.Trim();
            foreach (var pair in data)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value;
                }
            }

            return match.Value;
        });
    }
}
