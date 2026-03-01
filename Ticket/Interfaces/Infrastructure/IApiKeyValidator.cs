namespace Ticket.Interfaces.Infrastructure;

public interface IApiKeyValidator
{
    bool IsValid(string? providedKey);
}
