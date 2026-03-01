using System.Globalization;
using System.Text;

namespace Ticket.Data.Querying;

public readonly record struct TicketPageMarker(Guid TicketId, DateTimeOffset CreatedAtUtc, int ServedAtTimestamp);

public static class TicketPageToken
{
    public static string Encode(Guid ticketId, DateTimeOffset createdAtUtc, int servedAtTimestamp)
    {
        var payload = $"{createdAtUtc.UtcDateTime:O}|{servedAtTimestamp}|{ticketId}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryParse(string token, out TicketPageMarker marker)
    {
        marker = default;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = raw.Split('|', 3, StringSplitOptions.TrimEntries);
            if (parts.Length != 3)
            {
                return false;
            }

            if (!DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var createdAt))
            {
                return false;
            }

            if (!int.TryParse(parts[1], out var served) || served < 0)
            {
                return false;
            }

            if (!Guid.TryParse(parts[2], out var ticketId))
            {
                return false;
            }

            marker = new TicketPageMarker(ticketId, createdAt.ToUniversalTime(), served);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
