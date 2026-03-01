using Microsoft.Extensions.Options;
using Ticket.Configuration;
using Ticket.Interfaces.Infrastructure;

namespace Ticket.Services.Infrastructure;

public class ApiKeyValidator : IApiKeyValidator
{
    private readonly ApiKeyOptions _options;

    public ApiKeyValidator(IOptions<ApiKeyOptions> options)
    {
        _options = options.Value;
    }

    public bool IsValid(string? providedKey)
    {
        if (string.IsNullOrWhiteSpace(_options.CategoryManagement))
        {
            return false;
        }

        return string.Equals(_options.CategoryManagement, providedKey, StringComparison.Ordinal);
    }
}
