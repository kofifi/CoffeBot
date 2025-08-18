using CoffeBot.Abstractions;
using CoffeBot.Options;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;

namespace CoffeBot.Services;

public sealed class AuthUrlBuilder : IAuthUrlBuilder
{
    private readonly KickOptions _opt;
    public AuthUrlBuilder(IOptions<KickOptions> opt) => _opt = opt.Value;

    public string BuildAuthorizeUrl(string state, string codeChallenge, string scope)
    {
        var query = new QueryBuilder
        {
            {"response_type","code"},
            {"client_id", _opt.ClientId},
            {"redirect_uri", _opt.RedirectUri},
            {"scope", scope},
            {"code_challenge", codeChallenge},
            {"code_challenge_method","S256"},
            {"state", state}
        };
        return $"{_opt.AuthorizeEndpoint}{query.ToQueryString()}";
    }
}