using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Cryptography;
internal sealed class PinRetiroDesarrolloService
{
    private readonly IConfiguration _configuracion;
    private readonly IWebHostEnvironment _entorno;

    public PinRetiroDesarrolloService(
        IConfiguration configuracion,
        IWebHostEnvironment entorno)
    {
        _configuracion = configuracion;
        _entorno = entorno;
    }

    // Permitir el servicio solamente en desarrollo.
    private string ObtenerConexionSegura()
    {
        string? cadena = _configuracion
            .GetConnectionString("SynchroDB");

        if (string.IsNullOrWhiteSpace(cadena))
        {
            throw new InvalidOperationException(
                "No se configuro SynchroDB.");
        }

        var datos = new SqlConnectionStringBuilder(cadena);

        if (!string.Equals(
            datos.InitialCatalog,
            "SynchroDB",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Base de datos no autorizada.");
        }

        bool desarrolloAutorizado =
            _entorno.IsDevelopment() &&
            string.Equals(
                datos.DataSource,
                @"(localdb)\MSSQLLocalDB",
                StringComparison.OrdinalIgnoreCase);

        bool pruebasAutorizadas =
            _entorno.IsEnvironment("Testing") &&
            string.Equals(
                datos.DataSource,
                @"(localdb)\SYNCHRO_Aislado",
                StringComparison.OrdinalIgnoreCase);

        if (!desarrolloAutorizado && !pruebasAutorizadas)
        {
            throw new InvalidOperationException(
                "Instancia SQL no autorizada.");
        }

        return cadena;
    }

    private async Task<SqlConnection> AbrirConexionAsync(
        CancellationToken cancelacion = default)
    {
        var conexion = new SqlConnection(
            ObtenerConexionSegura());

        try
        {
            await conexion.OpenAsync(cancelacion);

            if (!string.Equals(
                conexion.Database,
                "SynchroDB",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "La base conectada no esta autorizada.");
            }

            if (_entorno.IsEnvironment("Testing"))
            {
                await using var comando = new SqlCommand(
                    @"SELECT physical_name
          FROM sys.database_files
          WHERE file_id = 1",
                    conexion);

                string? rutaActual = await comando
                    .ExecuteScalarAsync(cancelacion) as string;

                string rutaEsperada = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile),
                    "SYNCHRO_Aislado_Datos",
                    "SynchroDB_Aislado.mdf");

                if (string.IsNullOrWhiteSpace(rutaActual) ||
                    !string.Equals(
                        Path.GetFullPath(rutaActual),
                        Path.GetFullPath(rutaEsperada),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "La base de pruebas no es la copia aislada.");
                }
            }

            return conexion;
        }
        catch
        {
            await conexion.DisposeAsync();
            throw;
        }
    }

    public async Task<string> EmitirPinAsync(
        int usoId,
        int destinatarioId,
        int vigenciaMinutos = 1440,
        CancellationToken cancelacion = default)
    {
        if (usoId <= 0 || destinatarioId <= 0)
            throw new ArgumentException(
                "Identificadores invalidos.");

        if (vigenciaMinutos < 5 || vigenciaMinutos > 1440)
            throw new ArgumentException(
                "Vigencia no permitida.");

        // Generar el PIN y sus datos criptograficos.
        string pin = CodigoPinRetiro.Generar();
        byte[] salt = CodigoPinRetiro.GenerarSalt();


        byte[] hash = Array.Empty<byte>();

        try
        {
            hash = CodigoPinRetiro.CalcularHash(
                pin,
                usoId,
                destinatarioId,
                salt,
                _configuracion);

            await using var conexion =
                await AbrirConexionAsync(cancelacion);


            await using var comando = new SqlCommand(
                "dbo.EmitirPinRetiroSynchro",
                conexion)
            {
                CommandType = CommandType.StoredProcedure
            };

            comando.Parameters.Add(
                "@UsoId", SqlDbType.Int).Value = usoId;

            comando.Parameters.Add(
                "@DestinatarioId", SqlDbType.Int
            ).Value = destinatarioId;

            comando.Parameters.Add(
                "@PinHash", SqlDbType.VarBinary, 64
            ).Value = hash;

            comando.Parameters.Add(
                "@PinSalt", SqlDbType.VarBinary, 32
            ).Value = salt;

            comando.Parameters.Add(
                "@VigenciaMinutos", SqlDbType.Int
            ).Value = vigenciaMinutos;

            object? resultado =
                await comando.ExecuteScalarAsync(cancelacion);

            if (resultado is not int id || id != usoId)
                throw new InvalidOperationException(
                    "SQL no confirmo la emision del PIN.");

            return pin;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hash);
            CryptographicOperations.ZeroMemory(salt);
        }
    }

    internal async Task ComprobarConexionAsync(
        CancellationToken cancelacion = default)
    {
        await using var conexion =
            await AbrirConexionAsync(cancelacion);
    }

    public async Task<string> ValidarPinAsync(
        int usoId,
        int destinatarioId,
        string pin,
        CancellationToken cancelacion = default)
    {
        if (usoId <= 0 || destinatarioId <= 0)
            throw new ArgumentException(
                "Identificadores invalidos.");

        if (pin is null ||
            pin.Length != 6 ||
            pin.Any(c => c < '0' || c > '9'))
            throw new ArgumentException(
                "El PIN debe tener seis digitos.");

        byte[]? salt = null;
        byte[] hash = Array.Empty<byte>();

        try
        {
            await using var conexion =
                await AbrirConexionAsync(cancelacion);

            // Obtener el salt almacenado en SQL.
            await using (var buscar = new SqlCommand(
                @"SELECT PinSalt
              FROM dbo.CodigosRetiro
              WHERE UsoId = @UsoId",
                conexion))
            {
                buscar.Parameters.Add(
                    "@UsoId", SqlDbType.Int).Value = usoId;

                salt = await buscar.ExecuteScalarAsync(
                    cancelacion) as byte[];
            }

            if (salt is null)
                return "NoDisponible";

            if (salt.Length != 32)
                throw new InvalidOperationException(
                    "El salt almacenado no es valido.");

            hash = CodigoPinRetiro.CalcularHash(
                pin,
                usoId,
                destinatarioId,
                salt,
                _configuracion);

            // SQL verifica el PIN y registra el resultado.
            await using var comando = new SqlCommand(
                "dbo.ValidarPinRetiroSynchro",
                conexion)
            {
                CommandType = CommandType.StoredProcedure
            };

            comando.Parameters.Add(
                "@UsoId", SqlDbType.Int).Value = usoId;

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
                    "SQL no devolvio un resultado valido.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hash);

            if (salt is not null)
                CryptographicOperations.ZeroMemory(salt);
        }
    }

}
