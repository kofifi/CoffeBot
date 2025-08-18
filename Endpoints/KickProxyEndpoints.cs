using System.Net.Http;
using System.Net.Http.Headers;
using CoffeBot.Abstractions;

namespace CoffeBot.Endpoints;

public static class KickProxyEndpoints
{
    private static readonly string[] Methods = new[] { "GET", "POST", "PUT", "PATCH", "DELETE" };

    public static IEndpointRouteBuilder MapKickProxyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/kick");
        group.MapMethods("{**path}", Methods, Handler);
        return app;
    }

    private static async Task<IResult> Handler(
        string path,
        HttpContext ctx,
        ITokenStore store,
        IKickApiClient api)
    {
        var (access, _) = store.Read(ctx);
        if (access is null) return Results.Unauthorized();

        HttpContent? content = null;
        if (ctx.Request.ContentLength > 0)
        {
            content = new StreamContent(ctx.Request.Body);
            if (!string.IsNullOrEmpty(ctx.Request.ContentType))
                content.Headers.ContentType = MediaTypeHeaderValue.Parse(ctx.Request.ContentType);
        }

        var method = new HttpMethod(ctx.Request.Method);
        var query = ctx.Request.QueryString.HasValue ? ctx.Request.QueryString.Value : string.Empty;
        var (status, body) = await api.SendAsync(method, path + query, access, content, ctx.RequestAborted);

        return Results.Content(body, "application/json", statusCode: status);
    }
}

