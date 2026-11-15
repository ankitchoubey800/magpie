using Microsoft.AspNetCore.Antiforgery;

namespace Magpie.Accounts.Api.Security;

/// <summary>
/// Runs before every endpoint in the group. For requests that change something
/// (POST, PUT, DELETE) it demands a valid anti-forgery token in the
/// X-XSRF-TOKEN header. Read-only requests (GET) pass straight through.
/// </summary>
internal sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public const string ProblemCode = "antiforgery";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Reading is always allowed. Only requests that CHANGE something need the token.
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
        {
            return await next(context);
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            // The "code" lets the web app recognise this exact problem,
            // fetch a fresh token and try once more.
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Security check failed",
                detail: "The request did not carry a valid anti-forgery token. Reload the page and try again.",
                extensions: new Dictionary<string, object?> { ["code"] = ProblemCode });
        }

        return await next(context);
    }
}
