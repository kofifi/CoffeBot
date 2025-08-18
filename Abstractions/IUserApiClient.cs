// Abstractions/IUserApiClient.cs
namespace CoffeBot.Abstractions;

using CoffeBot.Models;

public interface IUserApiClient
{
    Task<(int Status, string Body)> GetCurrentRawAsync(string accessToken, CancellationToken ct);
    Task<CurrentUserDto> GetCurrentAsync(string accessToken, CancellationToken ct);
}