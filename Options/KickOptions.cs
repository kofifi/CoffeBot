namespace CoffeBot.Options;

public sealed class KickOptions
{
    public string AuthDomain { get; set; } = "https://id.kick.com";
    public string ApiBase { get; set; } = "https://api.kick.com";
    public string ClientId { get; set; } = "";
    public string? ClientSecret { get; set; }
    public string RedirectUri { get; set; } = "";
    public string Scope { get; set; } = "user:read";

    public string AuthorizeEndpoint => $"{AuthDomain.TrimEnd('/')}/oauth/authorize";
    public string TokenEndpoint     => $"{AuthDomain.TrimEnd('/')}/oauth/token";
    public string RevokeEndpoint    => $"{AuthDomain.TrimEnd('/')}/oauth/revoke";
    public string UsersEndpoint     => $"{ApiBase.TrimEnd('/')}/public/v1/users";
}