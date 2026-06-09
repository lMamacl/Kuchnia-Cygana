namespace KuchniaUCygana.Domain.Interfaces;

/// <summary>
/// Dostarcza informacje o aktualnie zalogowanym użytkowniku.
/// Implementacja w Infrastructure korzysta z IHttpContextAccessor.
/// Używany w serwisach aplikacyjnych do audytingu operacji (ChangedByUserId).
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Zwraca ID zalogowanego użytkownika lub null jeśli brak sesji.
    /// </summary>
    int? GetUserId();

    /// <summary>
    /// Zwraca nazwę (username) zalogowanego użytkownika lub null jeśli brak sesji.
    /// </summary>
    string? GetUserName();

    string? GetIpAddress();

    /// <summary>
    /// Czy użytkownik jest zalogowany.
    /// </summary>
    bool IsAuthenticated { get; }
}
