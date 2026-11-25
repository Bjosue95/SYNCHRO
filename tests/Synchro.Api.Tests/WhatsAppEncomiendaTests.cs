using Synchro.Api.Services.Encomiendas;

namespace Synchro.Api.Tests;

public sealed class WhatsAppEncomiendaTests
{
    [Fact]
    public void AceptacionesTienenAlcancesDistintosSinSecretos()
    {
        Assert.Equal("SYNCHRO ACEPTO", WhatsAppEncomienda.Aceptacion());
        Assert.Equal("SYNCHRO ACEPTO 81", WhatsAppEncomienda.Aceptacion(81));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-configurado")]
    [InlineData("++999123456700")]
    [InlineData("504/12345678")]
    [InlineData("50412345678?text=PIN")]
    public void SinNumeroOficialValidoNoSeInventaInvitacion(string? oficial)
    {
        Assert.Null(WhatsAppEncomienda.Invitacion(81, oficial));
    }

    [Fact]
    public void InvitacionAbreDestinatarioYEnlaceInteriorAbreSoloOficial()
    {
        const string oficial = "+999123456700", destinatario = "+999123456702";
        var mensaje = WhatsAppEncomienda.Invitacion(81, oficial)!;
        var compartir = new Uri(WhatsAppEncomienda.Enlace(destinatario, mensaje)!);
        Assert.Equal("/999123456702", compartir.AbsolutePath);
        Assert.Equal(mensaje, Uri.UnescapeDataString(compartir.Query[6..]));
        var aceptar = WhatsAppEncomienda.Enlace(oficial, WhatsAppEncomienda.Aceptacion(81))!;
        Assert.Contains(aceptar, mensaje);
        Assert.Equal("SYNCHRO ACEPTO 81", Uri.UnescapeDataString(new Uri(aceptar).Query[6..]));
        Assert.Contains("No necesitas cuenta ni iniciar sesión", mensaje);
        Assert.DoesNotContain(destinatario, mensaje);
        Assert.DoesNotContain("token", aceptar);
        Assert.DoesNotContain("PIN", aceptar);
    }
}
