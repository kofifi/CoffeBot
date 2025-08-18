// Abstractions/IUserApiClient.cs
namespace CoffeBot.Abstractions;

public interface IUserApiClient
{
    Task<(int Status, string Body)> GetCurrentRawAsync(string accessToken, CancellationToken ct);
}