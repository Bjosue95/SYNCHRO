using System.Buffers.Binary;
using System.IO.Compression;
using Synchro.Api.Services.RostroSimulado;
using Xunit;

namespace Synchro.Api.Tests;

public sealed class PatronVisualRetiroV2Tests
{
    // Rostros esquematicos creados por codigo; nunca son fotos de personas.
    private static byte[] Imagen(bool otra = false, int moverX = 0, int moverY = 0,
        int brillo = 0, bool uniforme = false)
    {
        using var salida = new MemoryStream();
        salida.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        void Chunk(string nombre, byte[] datos)
        {
            byte[] tipo = System.Text.Encoding.ASCII.GetBytes(nombre);
            byte[] contenido = tipo.Concat(datos).ToArray();
            Span<byte> numero = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(numero, datos.Length);
            salida.Write(numero);
            salida.Write(contenido);
            BinaryPrimitives.WriteUInt32BigEndian(numero,
                PatronVisualSimulado.Crc(contenido));
            salida.Write(numero);
        }

        byte[] ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), 64);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), 64);
        ihdr[8] = 8; // grayscale 8 bits
        Chunk("IHDR", ihdr);

        using var comprimido = new MemoryStream();
        using (var zip = new ZLibStream(comprimido, CompressionLevel.Fastest, true))
        {
            for (int y = 0; y < 64; y++)
            {
                zip.WriteByte(0); // filtro PNG
                for (int x = 0; x < 64; x++)
                {
                    int xx = x / 2 - moverX;
                    int yy = y / 2 - moverY;

                    int valor = uniforme ? 128 : 80;
                    if (!uniforme)
                    {
                        double cabeza =
                            Math.Pow((xx - 16) / 12d, 2) +
                            Math.Pow((yy - 16) / 14d, 2);
                        if (cabeza < 1d) valor += 90;

                        int ojoIzquierdo = otra ? 10 : 12;
                        int ojoDerecho = otra ? 22 : 20;
                        int alturaOjos = otra ? 12 : 13;
                        if (Math.Pow(xx - ojoIzquierdo, 2) +
                            Math.Pow(yy - alturaOjos, 2) < 6.25d ||
                            Math.Pow(xx - ojoDerecho, 2) +
                            Math.Pow(yy - alturaOjos, 2) < 6.25d)
                            valor -= 75;

                        int nariz = otra ? 15 : 16;
                        if (Math.Abs(xx - nariz) < 1 &&
                            yy > (otra ? 16 : 14) && yy < (otra ? 22 : 20))
                            valor -= 55;

                        int boca = otra ? 25 : 23;
                        if (Math.Abs(yy - boca) < 1 &&
                            Math.Abs(xx - 16) < (otra ? 6 : 5))
                            valor -= 50;
                    }

                    zip.WriteByte((byte)Math.Clamp(valor + brillo, 0, 255));
                }
            }
        }
        Chunk("IDAT", comprimido.ToArray());
        Chunk("IEND", []);
        return salida.ToArray();
    }

    [Fact]
    public void VersionDistinta_NoReinterpretaReferenciasPrevias()
    {
        Assert.NotEqual(PatronVisualSimulado.Modelo, PatronVisualRetiroV2.Modelo);
        Assert.Equal(1024, PatronVisualRetiroV2.Dimension);
    }

    [Fact]
    public void MismaCaptura_DistanciaCero()
    {
        double[] referencia = PatronVisualRetiroV2.Extraer(Imagen())!;
        double? distancia = PatronVisualRetiroV2.CalcularDistancia(referencia,
            PatronVisualRetiroV2.Extraer(Imagen()));
        Assert.NotNull(distancia);
        Assert.InRange(distancia.Value, 0, 0.000001d);
    }

    [Fact]
    public void IluminacionYMovimientoModerados_ConservanCoincidencia()
    {
        double[] referencia = PatronVisualRetiroV2.Extraer(Imagen())!;
        double[] actual = PatronVisualRetiroV2.Extraer(
            Imagen(moverX: 1, moverY: -1, brillo: 30))!;

        Assert.NotNull(referencia);
        Assert.NotNull(actual);
        double? distancia = PatronVisualRetiroV2.CalcularDistancia(referencia, actual);
        Assert.NotNull(distancia);
        Assert.True(distancia <= PatronVisualRetiroV2.UmbralDistancia,
            "La captura similar fue rechazada por el comparador V2.");
    }

    [Fact]
    public void OtraCapturaEsquematica_NoSeConfundeConReferencia()
    {
        double[] referencia = PatronVisualRetiroV2.Extraer(Imagen())!;
        double[] otra = PatronVisualRetiroV2.Extraer(Imagen(otra: true))!;

        double? distancia = PatronVisualRetiroV2.CalcularDistancia(referencia, otra);
        Assert.NotNull(distancia);
        Assert.True(distancia > PatronVisualRetiroV2.UmbralDistancia,
            "El comparador V2 no debe autorizar la figura distinta.");
    }

    [Fact]
    public void DatosInvalidosYCapturasUniformes_SeRechazan()
    {
        Assert.Null(PatronVisualRetiroV2.Extraer("no es PNG"u8.ToArray()));
        Assert.Null(PatronVisualRetiroV2.Extraer(Imagen(uniforme: true)));
        Assert.Null(PatronVisualRetiroV2.CalcularDistancia(
            new double[1024], new double[1023]));
        double[] invalidos = new double[1024];
        invalidos[0] = double.NaN;
        Assert.Null(PatronVisualRetiroV2.CalcularDistancia(
            invalidos, new double[1024]));
    }
}
