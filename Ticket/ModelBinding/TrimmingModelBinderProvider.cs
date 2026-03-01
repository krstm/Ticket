using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ticket.ModelBinding;

public class TrimmingModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context.Metadata.ModelType == typeof(string))
        {
            var loggerFactory = context.Services.GetRequiredService<ILoggerFactory>();
            var fallback = new SimpleTypeModelBinder(typeof(string), loggerFactory);
            return new TrimmingModelBinder(fallback);
        }

        return null;
    }
}
