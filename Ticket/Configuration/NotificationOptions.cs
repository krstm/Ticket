namespace Ticket.Configuration;

public class NotificationOptions
{
    public const string SectionName = "Notifications";

    public bool NotifyOnTicketCreated { get; set; } = false;
    public bool NotifyOnTicketResolved { get; set; } = false;
    public string PreferredChannel { get; set; } = "none";
}
