using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Synchro.Api.Services.RostroSimulado;

// Comparacion VISUAL de demostracion, no reconocimiento biometrico ni prueba de
// vida. Acepta PNG de canvas RGB/RGBA/gris, reduce a 32x32 y normaliza luminancia.
// La identidad procede de la sesion/reserva; no de metadatos de la imagen.
internal static class PatronVisualSimulado
{
    public const int MaximoBytes=1024*1024;
    public const string Modelo="SYNCHRO-SIM-32x32-v1";
    public static double[]? Extraer(byte[] imagen)
    {
        if(imagen.Length is <57 or >MaximoBytes||!imagen.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))return null;
        int ancho=0,alto=0,canales=0,pos=8;bool cabecera=false,fin=false;
        using var comprimido=new MemoriaProtegida();
        while(pos<=imagen.Length-12)
        {
            uint longitud=BinaryPrimitives.ReadUInt32BigEndian(imagen.AsSpan(pos,4));
            if(longitud>(uint)(imagen.Length-pos-12))return null;
            int n=(int)longitud;var tipo=imagen.AsSpan(pos+4,4);var datos=imagen.AsSpan(pos+8,n);
            if(Crc(imagen.AsSpan(pos+4,n+4))!=BinaryPrimitives.ReadUInt32BigEndian(imagen.AsSpan(pos+8+n,4)))return null;
            if(tipo.SequenceEqual("IHDR"u8))
            {
                if(cabecera||pos!=8||n!=13||datos[8]!=8||datos[10]!=0||datos[11]!=0||datos[12]!=0)return null;
                uint w=BinaryPrimitives.ReadUInt32BigEndian(datos[..4]),h=BinaryPrimitives.ReadUInt32BigEndian(datos.Slice(4,4));
                if(w is <32 or >1024||h is <32 or >1024)return null;
                ancho=(int)w;alto=(int)h;canales=datos[9]switch{0=>1,2=>3,6=>4,_=>0};if(canales==0)return null;cabecera=true;
            }
            else if(tipo.SequenceEqual("IDAT"u8)){if(!cabecera)return null;comprimido.Write(datos);}
            else if(tipo.SequenceEqual("IEND"u8)){if(n!=0)return null;fin=true;pos+=12;break;}
            pos+=n+12;
        }
        if(!fin||pos!=imagen.Length||comprimido.Length==0)return null;
        int stride=ancho*canales;byte[] inflado=new byte[(stride+1)*alto],anterior=new byte[stride],actual=new byte[stride];
        var patron=new double[1024];var cuenta=new int[1024];int minimo=255,maximo=0;bool entregado=false;
        try
        {
            comprimido.Position=0;using var zlib=new ZLibStream(comprimido,CompressionMode.Decompress,true);
            zlib.ReadExactly(inflado);if(zlib.ReadByte()!=-1)return null;
            for(int y=0;y<alto;y++)
            {
                int inicio=y*(stride+1),filtro=inflado[inicio];if(filtro>4)return null;
                for(int i=0;i<stride;i++)
                {
                    int a=i>=canales?actual[i-canales]:0,b=anterior[i],c=i>=canales?anterior[i-canales]:0;
                    int pred=filtro switch{0=>0,1=>a,2=>b,3=>(a+b)/2,4=>Paeth(a,b,c),_=>0};
                    actual[i]=unchecked((byte)(inflado[inicio+1+i]+pred));
                }
                for(int x=0;x<ancho;x++)
                {
                    int i=x*canales;int gris=canales==1?actual[i]:(77*actual[i]+150*actual[i+1]+29*actual[i+2])/256;
                    if(canales==4)gris=(gris*actual[i+3]+255*(255-actual[i+3]))/255;
                    minimo=Math.Min(minimo,gris);maximo=Math.Max(maximo,gris);
                    int celda=(y*32/alto)*32+x*32/ancho;patron[celda]+=gris;cuenta[celda]++;
                }
                (anterior,actual)=(actual,anterior);
            }
            if(maximo-minimo<20)return null;
            double norma=0;for(int i=0;i<patron.Length;i++){patron[i]/=cuenta[i]*255d;norma+=patron[i]*patron[i];}
            if(norma<=0)return null;norma=Math.Sqrt(norma);for(int i=0;i<patron.Length;i++)patron[i]/=norma;
            entregado=true;return patron;
        }
        catch(Exception ex)when(ex is InvalidDataException or EndOfStreamException){return null;}
        finally
        {
            if(!entregado)Array.Clear(patron);
            CryptographicOperations.ZeroMemory(inflado);CryptographicOperations.ZeroMemory(anterior);CryptographicOperations.ZeroMemory(actual);
            if(comprimido.TryGetBuffer(out var bytes))CryptographicOperations.ZeroMemory(bytes.AsSpan());
        }
    }
    private static int Paeth(int a,int b,int c)
    {int p=a+b-c,pa=Math.Abs(p-a),pb=Math.Abs(p-b),pc=Math.Abs(p-c);return pa<=pb&&pa<=pc?a:pb<=pc?b:c;}
    internal static uint Crc(ReadOnlySpan<byte> datos)
    {
        uint crc=uint.MaxValue;foreach(byte b in datos){crc^=b;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0u);}return ~crc;
    }
}
