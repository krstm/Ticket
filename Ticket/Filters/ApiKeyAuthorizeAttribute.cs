using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Ticket.Interfaces.Infrastructure;

namespace Ticket.Filters;

public class ApiKeyAuthorizeAttribute : TypeFilterAttribute
{
    public ApiKeyAuthorizeAttribute() : base(typeof(ApiKeyAuthorizeFilter))
    {
    }

    private class ApiKeyAuthorizeFilter : IAsyncAuthorizationFilter
    {
        private readonly IApiKeyValidator _validator;

        public ApiKeyAuthorizeFilter(IApiKeyValidator validator)
        {
            _validator = validator;
        }

        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var provided = context.HttpContext.Request.Headers["X-API-Key"].FirstOrDefault();
            if (!_validator.IsValid(provided))
            {
                context.Result = new UnauthorizedResult();
            }

            return Task.CompletedTask;
        }
    }
}
