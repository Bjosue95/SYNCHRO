namespace Synchro.Api.Services;

internal static class ValidacionCredencialesCuenta
{
    public static string Nombre(string? nombre)
    {
        string? normal=nombre?.Trim().Normalize();
        if(normal is null||normal.Length is <3 or >80||normal.Any(c=>!char.IsLetterOrDigit(c)&&c is not('_' or '-' or '.')))
            throw new ArgumentException("Nombre de usuario invalido.");
        return normal;
    }
    public static void Password(string? password)
    {if(password is null||password.Length is <12 or >128)throw new ArgumentException("Contrasena invalida.");}
}
