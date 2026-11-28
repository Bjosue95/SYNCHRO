
using System.Data;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Synchro.Api.Models;

if (args.Length == 1 && args[0] == "--crear-administrador-inicial")
{
    Environment.ExitCode = await CrearAdministradorInicialAsync();
    return;
}

if(args.Length>0)
{
    if(args.Length!=1||args[0] is not("--plan-prototipo" or "--preparar-prototipo"))
    {Console.Error.WriteLine("Modo no valido. Usar --plan-prototipo, --preparar-prototipo o --crear-administrador-inicial, sin argumentos adicionales.");Environment.ExitCode=2;return;}
    if(args[0]=="--plan-prototipo")
    {
        Console.WriteLine("Destino exclusivo: SynchroDB_Pruebas vacia o marcada. No crea ni reinicia bases.");
        foreach(string archivo in Synchro.Api.Database.InstalacionPrototipo.Archivos)Console.WriteLine(archivo);
        return;
    }
    try
    {
        await Synchro.Api.Database.InstalacionPrototipo.AplicarAsync(Environment.GetEnvironmentVariable("SYNCHRO_SQL_DEPOSITO_TEST"),
            Path.Combine(AppContext.BaseDirectory,"Database"),Console.WriteLine);
        Console.WriteLine("Esquema aislado aplicado. No equivale al esquema historico completo ni a pruebas aprobadas.");
    }
    catch(Exception e)when(e is SqlException or InvalidOperationException or ArgumentException or IOException)
    {Console.Error.WriteLine($"Instalacion detenida ({e.GetType().Name}). Revisar conexion aislada, marca, archivos y SQL Server; no se ejecutan lotes posteriores.");Environment.ExitCode=1;}
    return;
}

Console.WriteLine("=== SYNCHRO | Configuración de administrador ===");

var cadena = Environment.GetEnvironmentVariable(
    "SYNCHRO_DB_CONNECTION");

if (string.IsNullOrWhiteSpace(cadena))
{
    Console.WriteLine("Falta configurar la conexión a SynchroDB.");
    return;
}

Console.Write("Nombre de usuario: ");
var nombre = Console.ReadLine()?.Trim();

if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 80)
{
    Console.WriteLine("El nombre de usuario no es válido.");
    return;
}

Console.Write("Contraseña: ");
var password = LeerContrasena();

if (password.Length < 12)
{
    Console.WriteLine("La contraseña debe tener al menos 12 caracteres.");
    return;
}

Console.Write("Confirmar contraseña: ");
var confirmacion = LeerContrasena();

if (password != confirmacion)
{
    Console.WriteLine("Las contraseñas no coinciden.");
    return;
}

var administrador = new Administrador
{
    NombreUsuario = nombre
};

var hasher = new PasswordHasher<Administrador>();

administrador.PasswordHash =
    hasher.HashPassword(administrador, password);

try
{
    using var conexion = new SqlConnection(cadena);
    await conexion.OpenAsync();

    using var comando = new SqlCommand(@"
        INSERT INTO dbo.Administradores
            (NombreUsuario, PasswordHash)
        OUTPUT INSERTED.Id
        VALUES (@nombre, @hash)",
        conexion);

    comando.Parameters.Add(
        "@nombre", SqlDbType.NVarChar, 80).Value =
        administrador.NombreUsuario;

    comando.Parameters.Add(
        "@hash", SqlDbType.NVarChar, 500).Value =
        administrador.PasswordHash;

    var id = Convert.ToInt32(
        await comando.ExecuteScalarAsync());

    Console.WriteLine(
        $"Administrador creado correctamente. ID: {id}");
}
catch (SqlException ex) when (ex.Number is 2601 or 2627)
{
    Console.WriteLine("Ese nombre de usuario ya existe.");
}
catch (SqlException ex)
{
    Console.WriteLine($"Error de base de datos: {ex.Message}");
}

static string LeerContrasena()
{
    var caracteres = new StringBuilder();

    while (true)
    {
        var tecla = Console.ReadKey(intercept: true);

        if (tecla.Key == ConsoleKey.Enter)
            break;

        if (tecla.Key == ConsoleKey.Backspace)
        {
            if (caracteres.Length > 0)
                caracteres.Length--;

            continue;
        }

        if (!char.IsControl(tecla.KeyChar))
            caracteres.Append(tecla.KeyChar);
    }

    Console.WriteLine();
    return caracteres.ToString();
}

static async Task<int> CrearAdministradorInicialAsync()
{
    // Instalación local de la primera cuenta; nunca convierte cuentas de prueba.
    const string destino = @"Server=.\SQLEXPRESS;Database=SynchroDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=15";
    try
    {
        await using var conexion = new SqlConnection(destino);
        await conexion.OpenAsync();

        const string verificar = """
            IF DB_NAME() <> N'SynchroDB'
               OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'') <> N'SQLEXPRESS'
               OR ISNULL(CONVERT(int, SERVERPROPERTY('IsLocalDB')),1) <> 0
                THROW 59020, 'Destino no autorizado.', 1;
            IF EXISTS(SELECT 1 FROM dbo.Administradores WITH(UPDLOCK,HOLDLOCK))
               OR EXISTS(SELECT 1 FROM dbo.AccesosAdministrativosGestionados WITH(UPDLOCK,HOLDLOCK))
               OR EXISTS(SELECT 1 FROM dbo.RolesAdministrativosPrototipo WITH(UPDLOCK,HOLDLOCK))
                THROW 59021, 'La base ya tiene administradores o permisos.', 1;
            """;

        await using (var comprobar = new SqlCommand(verificar, conexion))
            await comprobar.ExecuteNonQueryAsync();

        Console.WriteLine("=== SYNCHRO | Administrador inicial de .\\SQLEXPRESS / SynchroDB ===");
        Console.Write("Nombre de usuario (4 a 32 letras o números): ");
        var nombre = Console.ReadLine()?.Trim();
        if (nombre is null || !System.Text.RegularExpressions.Regex.IsMatch(nombre, @"\A[A-Za-z0-9]{4,32}\z"))
        {
            Console.Error.WriteLine("Nombre de usuario no válido.");
            return 2;
        }

        Console.Write("Contraseña (12 a 128 caracteres): ");
        var password = LeerContrasena();
        if (password.Length < 12 || password.Length > 128 || string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Contraseña no válida.");
            return 2;
        }
        Console.Write("Confirmar contraseña: ");
        if (password != LeerContrasena())
        {
            Console.Error.WriteLine("Las contraseñas no coinciden.");
            return 2;
        }

        var cuenta = new Administrador { NombreUsuario = nombre };
        var hash = new PasswordHasher<Administrador>().HashPassword(cuenta, password);
        await using var tx = (SqlTransaction)await conexion.BeginTransactionAsync(IsolationLevel.Serializable);
        await using var comando = new SqlCommand(verificar + """
            
            SET NOCOUNT ON;
            INSERT dbo.Administradores(NombreUsuario,PasswordHash,Activo,FechaCreacion)
            VALUES(@nombre,@hash,1,SYSUTCDATETIME());
            DECLARE @id int = CONVERT(int,SCOPE_IDENTITY());
            INSERT dbo.AccesosAdministrativosGestionados(AdministradorId,VersionSesion)
            VALUES(@id,1);
            INSERT dbo.RolesAdministrativosPrototipo(AdministradorId,Rol)
            VALUES(@id,N'Administrador');
            INSERT dbo.AuditoriaUsuariosAdministrativos(AdministradorId,ActorId,Accion,FechaUtc)
            VALUES(@id,@id,N'Administrador inicial creado localmente',SYSUTCDATETIME());
            SELECT @id;
            """, conexion, tx);
        comando.Parameters.Add("@nombre", SqlDbType.NVarChar, 80).Value = nombre;
        comando.Parameters.Add("@hash", SqlDbType.NVarChar, 500).Value = hash;
        var id = Convert.ToInt32(await comando.ExecuteScalarAsync());
        if (id <= 0) throw new InvalidOperationException("No se creó la cuenta.");
        await tx.CommitAsync();
        Console.WriteLine($"[OK] Administrador inicial creado. ID: {id}; rol Administrador; versión de sesión 1.");
        return 0;
    }
    catch (Exception e) when (e is SqlException or InvalidOperationException or IOException)
    {
        Console.Error.WriteLine(e is SqlException sql && sql.Number == 59021
            ? "Ya existe una cuenta o permisos. No se creará otro administrador inicial."
            : "Configuración detenida. Verifica la conexión local, permisos y esquema SQL.");
        return 1;
    }
}
