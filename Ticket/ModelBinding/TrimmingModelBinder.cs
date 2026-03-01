using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Ticket.ModelBinding;

public class TrimmingModelBinder : IModelBinder
{
    private readonly IModelBinder _fallbackBinder;

    public TrimmingModelBinder(IModelBinder fallbackBinder)
    {
        _fallbackBinder = fallbackBinder;
    }

    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        await _fallbackBinder.BindModelAsync(bindingContext);

        if (!bindingContext.Result.IsModelSet)
        {
            return;
        }

        if (bindingContext.Result.Model is string str)
        {
            var trimmed = string.IsNullOrWhiteSpace(str) ? null : str.Trim();
            bindingContext.Result = ModelBindingResult.Success(trimmed);
        }
    }
}
