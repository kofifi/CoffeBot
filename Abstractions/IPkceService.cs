namespace CoffeBot.Abstractions;

public interface IPkceService
{
    (string CodeVerifier, string CodeChallenge) CreatePair();
    string CreateRandomBase64Url(int bytesLength = 32);
}