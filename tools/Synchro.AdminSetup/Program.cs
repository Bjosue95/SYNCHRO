
using System.Data;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Synchro.Api.Models;

if(args.Length>0)
{
    if(args.Length!=1||args[0] is not("--plan-prototipo" or "--preparar-prototipo"))
    {Console.Error.WriteLine("Modo no valido. Usar --plan-prototipo o --preparar-prototipo, sin argumentos adicionales.");Environment.ExitCode=2;return;}
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
