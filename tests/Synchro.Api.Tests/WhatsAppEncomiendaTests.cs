using Synchro.Api.Services.Encomiendas;

namespace Synchro.Api.Tests;

public sealed class WhatsAppEncomiendaTests
{
    private static readonly Guid CodigoConsentimiento = Guid.Parse("9e32a8b1-4c65-4f80-95b4-72a0ed3f16c2");
    [Fact]
    public void AceptacionesTienenAlcancesDistintosSinSecretos()
    {
        Assert.Equal("SYNCHRO ACEPTO", WhatsAppEncomienda.Aceptacion());
        Assert.Equal("SYNCHRO ACEPTO 81", WhatsAppEncomienda.Aceptacion(81));
        Assert.Equal("SYNCHRO ACEPTO ENCOMIENDA 9e32a8b14c654f8095b472a0ed3f16c2",
            WhatsAppEncomienda.AceptacionDestinatario(CodigoConsentimiento));
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
        Assert.Null(WhatsAppEncomienda.Invitacion(CodigoConsentimiento, oficial));
    }

    [Fact]
    public void InvitacionAbreDestinatarioYEnlaceInteriorAbreSoloOficial()
    {
        const string oficial = "+999123456700", destinatario = "+999123456702";
        var mensaje = WhatsAppEncomienda.Invitacion(CodigoConsentimiento, oficial)!;
        var compartir = new Uri(WhatsAppEncomienda.Enlace(destinatario, mensaje)!);
        Assert.Equal("/999123456702", compartir.AbsolutePath);
        Assert.Equal(mensaje, Uri.UnescapeDataString(compartir.Query[6..]));
        var aceptar = WhatsAppEncomienda.Enlace(oficial, WhatsAppEncomienda.AceptacionDestinatario(CodigoConsentimiento))!;
        Assert.Contains(aceptar, mensaje);
        Assert.Equal("SYNCHRO ACEPTO ENCOMIENDA 9e32a8b14c654f8095b472a0ed3f16c2", Uri.UnescapeDataString(new Uri(aceptar).Query[6..]));
        Assert.Contains("La aceptación no permite retirar por sí sola.", mensaje);
        Assert.Contains("No hay un segundo cobro al retirar.", mensaje);
        Assert.DoesNotContain("SYNCHRO ACEPTO 81", mensaje);
        Assert.DoesNotContain(destinatario, mensaje);
        Assert.DoesNotContain("token", aceptar);
        Assert.DoesNotContain("PIN", aceptar);
    }
}
