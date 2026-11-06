
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Synchro.Api.Tests;

// Autenticacion simulada, exclusivamente para pruebas.
public sealed class AutenticacionDestinatarioPruebas
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Esquema = "DestinatarioDePrueba";

    public AutenticacionDestinatarioPruebas(
        IOptionsMonitor<AuthenticationSchemeOptions> opciones,
        ILoggerFactory logger,
        UrlEncoder codificador)
        : base(opciones, logger, codificador)
    {
    }

    protected override Task<AuthenticateResult>
        HandleAuthenticateAsync()
    {
        // Este encabezado solo lo interpreta el servidor de pruebas.
        string? valor = Request.Headers[
            "X-Synchro-Test-Persona"].ToString();

        if (!int.TryParse(valor, out int personaId)
            || personaId <= 0)
        {
            return Task.FromResult(
                AuthenticateResult.NoResult());
        }

        var identidad = new ClaimsIdentity(
            new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    personaId.ToString()),

                new Claim(
                    "tipo_usuario",
                    "Destinatario")
            },
            Esquema);

        var usuario = new ClaimsPrincipal(identidad);

        return Task.FromResult(
            AuthenticateResult.Success(
                new AuthenticationTicket(usuario, Esquema)));
    }
}
