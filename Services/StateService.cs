using CoffeBot.Abstractions;

namespace CoffeBot.Services;

public sealed class StateService : IStateService
{
    const string StateKey = "state";
    const string VerifierKey = "code_verifier";
    const string NonceKey = "nonce";

    public void Save(HttpContext ctx, string state, string codeVerifier, string nonce)
    {
        ctx.Session.SetString(StateKey, state);
        ctx.Session.SetString(VerifierKey, codeVerifier);
        ctx.Session.SetString(NonceKey, nonce);
    }

    public (string? State, string? CodeVerifier, string? Nonce) Read(HttpContext ctx)
        => (ctx.Session.GetString(StateKey), ctx.Session.GetString(VerifierKey), ctx.Session.GetString(NonceKey));

    public void Clear(HttpContext ctx)
    {
        ctx.Session.Remove(StateKey);
        ctx.Session.Remove(VerifierKey);
        ctx.Session.Remove(NonceKey);
    }
}