using System.Data;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;

internal sealed class PinRetiroService
{
    private readonly IConfiguration _configuracion;

    public PinRetiroService(IConfiguration configuracion)
    {
        _configuracion = configuracion;
    }

    // Abrir exclusivamente la base de pruebas.
    private async Task<SqlConnection> AbrirConexionPruebasAsync(
        CancellationToken cancelacion)
    {
        string? cadena = _configuracion
            .GetConnectionString("SynchroDBPruebas");

        if (string.IsNullOrWhiteSpace(cadena))
        {
            throw new InvalidOperationException(
                "No se configuro la conexion SynchroDBPruebas.");
        }

        var datosConexion = new SqlConnectionStringBuilder(cadena);

        if (!string.Equals(
            datosConexion.InitialCatalog,
            "SynchroDB_Pruebas",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "La conexion no apunta a la base de pruebas.");
        }

        if (!string.Equals(datosConexion.DataSource, @"(localdb)\SYNCHRO_Aislado", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(datosConexion.AttachDBFilename))
            throw new InvalidOperationException("Instancia de pruebas no autorizada.");

        var conexion = new SqlConnection(cadena);

        try
        {
            await conexion.OpenAsync(cancelacion);

            if (!string.Equals(
                conexion.Database,
                "SynchroDB_Pruebas",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "La base conectada no es la de pruebas.");
            }

            await using var marcador = new SqlCommand("IF NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'SynchroFixtureDeposito' AND CONVERT(nvarchar(10),value)=N'1') THROW 57302,'Falta marcador de pruebas.',1;", conexion);
            await marcador.ExecuteNonQueryAsync(cancelacion);
            return conexion;
        }
        catch
        {
            await conexion.DisposeAsync();
            throw;
        }
    }

    // Consultar el salt asociado a una encomienda.
    public async Task<byte[]?> ObtenerSaltAsync(
        int usoId,
        CancellationToken cancelacion = default)
    {
        if (usoId <= 0)
        {
            throw new ArgumentException(
                "El identificador de encomienda no es valido.");
        }

        await using var conexion =
            await AbrirConexionPruebasAsync(cancelacion);

        await using var comando = new SqlCommand(
            @"SELECT PinSalt
              FROM dbo.CodigosRetiro
              WHERE UsoId = @UsoId",
            conexion);

        comando.Parameters.Add(
            "@UsoId", SqlDbType.Int).Value = usoId;

        object? resultado =
            await comando.ExecuteScalarAsync(cancelacion);

        return resultado as byte[];


    }

    // Validar un PIN utilizando la base de pruebas.
    // No recibe hashes desde el usuario.
    public async Task<string> ValidarPinAsync(
        int usoId,
        int destinatarioId,
        string pin,
        CancellationToken cancelacion = default)
    {
        if (usoId <= 0 || destinatarioId <= 0)
        {
            throw new ArgumentException(
                "Los identificadores no son validos.");
        }

        if (pin is null ||
            pin.Length != 6 ||
            pin.Any(c => c < '0' || c > '9'))
        {
            throw new ArgumentException(
                "El PIN debe contener seis digitos.");
        }

        // Obtener el salt desde SQL Server.
        byte[]? salt = await ObtenerSaltAsync(
            usoId, cancelacion);

        if (salt is null)
        {
            return "NoDisponible";
        }

        if (salt.Length != 32)
        {
            throw new InvalidOperationException(
                "El salt almacenado no es valido.");
        }

        // Calcular el hash en el backend, nunca en el cliente.
        byte[] hash = CodigoPinRetiro.CalcularHash(
            pin,
            usoId,
            destinatarioId,
            salt,
            _configuracion);

        try
        {
            await using var conexion =
                await AbrirConexionPruebasAsync(cancelacion);

            await using var comando = new SqlCommand(
                "dbo.ValidarPinRetiroSynchro",
                conexion)
            {
                CommandType = CommandType.StoredProcedure
            };

            comando.Parameters.Add(
                "@UsoId", SqlDbType.Int
            ).Value = usoId;

            comando.Parameters.Add(
                "@DestinatarioId", SqlDbType.Int
            ).Value = destinatarioId;

            comando.Parameters.Add(
                "@PinHashCandidato",
                SqlDbType.VarBinary, 32
            ).Value = hash;

            comando.Parameters.Add(
                "@PinSaltLeido",
                SqlDbType.VarBinary, 32
            ).Value = salt;

            object? resultado =
                await comando.ExecuteScalarAsync(cancelacion);

            return resultado as string
                ?? throw new InvalidOperationException(
                    "SQL Server no devolvio un resultado valido.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hash);
        }
    }

}
