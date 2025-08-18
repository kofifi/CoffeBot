namespace CoffeBot.Abstractions;

public interface IStateService
{
    void Save(HttpContext ctx, string state, string codeVerifier, string nonce);
    (string? State, string? CodeVerifier, string? Nonce) Read(HttpContext ctx);
    void Clear(HttpContext ctx);
}