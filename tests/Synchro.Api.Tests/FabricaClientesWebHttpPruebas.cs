using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Synchro.Api.Services.Clientes;

namespace Synchro.Api.Tests;

// Servidor HTTP en memoria para clientes: ejecuta Program y la cookie real,
// pero no activa módulos operativos ni trabajadores de la feria.
internal sealed class FabricaClientesWebHttpPruebas(ClientesWebSimulados cuentas, bool feria = false)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:SynchroDB"] = @"Server=(localdb)\SYNCHRO_Suite_Test;Database=SynchroDB_Pruebas;Integrated Security=True;TrustServerCertificate=True;",
                ["ClientesWeb:ModoFeria"] = feria ? "true" : "false",
                ["WhatsAppManual:Habilitado"] = "false",
                ["WhatsApp:SolicitudesInicioHabilitadas"] = "false",
                ["WhatsApp:SolicitudesInicioWorker:Habilitado"] = "false",
                ["WhatsApp:VerificacionOutbox:Habilitado"] = "false",
                ["WhatsApp:Outbox:Habilitado"] = "false",
                ["EncomiendasOperacion:Habilitado"] = "false",
                ["EncomiendasOperacion:LimpiarBiometria"] = "false",
                ["ReservasPrototipo:AccesoLibrePruebas"] = "false",
                ["Reservas:ProcesarVencimientos"] = "false",
                ["ServiciosTemporales:Habilitado"] = "false",
                ["ServiciosTemporales:ProcesarVencimientos"] = "false",
                ["CargaDispositivos:RegistroHabilitado"] = "false",
                ["Administracion:UsuariosHabilitados"] = "false",
                ["Administracion:ConfiguracionHabilitada"] = "false",
                ["RostroSimulado:Habilitado"] = "false",
                ["RostroSimulado:LimpiarVencidas"] = "false",
                ["RostroV3:RegistroHabilitado"] = "false",
                ["RostroV3:RetiroHabilitado"] = "false"
            }));
        builder.ConfigureTestServices(servicios =>
        {
            servicios.RemoveAll<IRepositorioClientesWeb>();
            servicios.AddSingleton<IRepositorioClientesWeb>(cuentas);
            // Cookies firmadas reales, claves exclusivas del ensayo, no las de feria.
            servicios.RemoveAll<IDataProtectionProvider>();
            servicios.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        });
    }
}
