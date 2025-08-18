namespace CoffeBot.Models;

// Dopasuj do realnej odpowiedzi /public/v1/users (tu minimalistycznie)
public sealed class CurrentUserDto
{
    public string? Username { get; set; }
    public int Id { get; set; }
    public object? Raw { get; set; } // opcjonalnie: przechowaj całość
}