namespace Ticket.Interfaces.Infrastructure;

public interface IContentSanitizer
{
    string Sanitize(string? input);
}
