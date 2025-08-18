namespace CoffeBot.Abstractions;

public interface IAuthUrlBuilder
{
    string BuildAuthorizeUrl(string state, string codeChallenge, string scope);
}