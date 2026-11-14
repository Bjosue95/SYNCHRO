
using System.Data;
using System.Security.Cryptography;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Synchro.Api.Services;
using Synchro.Api.Services.WhatsApp;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public sealed class EjecutorSolicitudesInicioWhatsAppTests
{
    [SqlDepositoFact]
    public async Task EjecutaUnaSolicitudSinDuplicarVerificacion()
    {
        string cadena = ConexionSqlAislada.Exigir(
            Environment.GetEnvironmentVariable(
                "SYNCHRO_SQL_DEPOSITO_TEST"),
            "SynchroDB_Pruebas");

        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SynchroDB"] = cadena,
                    ["VerificacionTelefono:ClaveHmac"] =
                        Convert.ToBase64String(
                            RandomNumberGenerator.GetBytes(32)),
                    ["WhatsApp:PlantillaCodigoInicioSesion"] =
                        "codigo_inicio_sesion"
                })
            .Build();

        var protector = new ProtectorVerificacionTelefono(
            new EphemeralDataProtectionProvider());

        var reclamador =
            new ReclamadorSolicitudesInicioWhatsAppSql(
                configuracion);

        var procesador =
            new ProcesadorSolicitudesInicioWhatsAppSql(
                configuracion,
                new IdentidadDestinatarioWhatsAppSql(
                    configuracion),
                new EmisorVerificacionTelefonoSql(
                    configuracion, protector));

        var cierre =
            new CierreFalloSolicitudesInicioWhatsAppSql(
                configuracion);

        var ejecutor = new EjecutorSolicitudesInicioWhatsApp(
            reclamador,
            procesador,
            cierre,
            NullLogger<EjecutorSolicitudesInicioWhatsApp>
                .Instance);

        string remitente = "+999" +
            RandomNumberGenerator.GetInt32(
                1_000_000_000, 2_000_000_000);

        string destinatario = "+999" +
            RandomNumberGenerator.GetInt32(
                1_000_000_000, 2_000_000_000);

        string usuario = "test_ejecutor_" +
            Guid.NewGuid().ToString("N");

        string mensajeId = "wamid.ejecutor." +
            Guid.NewGuid().ToString("N");
        string mensajeRevocadoId = mensajeId + ".revocado";

        int remitenteId = 0;
        int reservaId = 0;
        long eventoId = 0;

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();

        try
        {
            // Preparar una solicitud pendiente ficticia.
            await using (var preparar = new SqlCommand("""
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 57170, 'Base no autorizada.', 1;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.extended_properties
                    WHERE class = 0
                      AND name = N'SynchroFixtureDeposito'
                )
                    THROW 57171, 'Falta marca de pruebas.', 1;

                BEGIN TRY
                    BEGIN TRANSACTION;

                    INSERT dbo.Personas
                        (TelefonoWhatsapp, TieneCuenta)
                    VALUES (@Remitente, 1);

                    DECLARE @RemitenteId INT =
                        CONVERT(INT, SCOPE_IDENTITY());

                    INSERT dbo.ClientesWebPrototipo
                        (PersonaId, NombreUsuario,
                         Telefono, PasswordHash)
                    VALUES (
                        @RemitenteId, @Usuario,
                        @Remitente,
                        N'HASH_FICTICIO_NO_AUTENTICABLE'
                    );

                    INSERT dbo.PreReservasEncomiendasWeb
                        (TokenGestionHash,
                         TelefonoRemitente,
                         TelefonoDestinatario,
                         TamanoSolicitado,
                         FechaExpiracion,
                         ClientePersonaId)
                    VALUES (
                        CRYPT_GEN_RANDOM(32),
                        @Remitente, @Destinatario,
                        N'Mediano',
                        DATEADD(HOUR, 1, SYSUTCDATETIME()),
                        @RemitenteId
                    );

                    DECLARE @ReservaId INT =
                        CONVERT(INT, SCOPE_IDENTITY());

                    INSERT dbo.ConsentimientosPreReservaWhatsApp
                        (ReservaId, TelefonoWhatsApp,
                         CodigoHash, Estado,
                         FechaCreacionUtc,
                         FechaExpiracionUtc,
                         FechaConfirmacionUtc,
                         MensajeIdConfirmacion)
                    VALUES (
                        @ReservaId, @Destinatario,
                        CRYPT_GEN_RANDOM(32),
                        N'Confirmado',
                        DATEADD(MINUTE, -2, SYSUTCDATETIME()),
                        DATEADD(MINUTE, 30, SYSUTCDATETIME()),
                        DATEADD(MINUTE, -1, SYSUTCDATETIME()),
                        N'CONS-' + @Usuario
                    );

                    INSERT dbo.WhatsAppWebhookEventos
                        (ClaveEvento, TipoEvento,
                         MensajeId, WhatsappId)
                    VALUES (
                        CRYPT_GEN_RANDOM(32),
                        N'Mensaje',
                        @MensajeId,
                        SUBSTRING(@Destinatario, 2, 20)
                    );

                    DECLARE @EventoId BIGINT =
                        CONVERT(BIGINT, SCOPE_IDENTITY());

                    INSERT dbo.SolicitudesInicioSesionWhatsApp
                        (EventoId, ReservaId, ExpiraUtc)
                    VALUES (
                        @EventoId,
                        @ReservaId,
                        DATEADD(MINUTE, 5, SYSUTCDATETIME())
                    );

                    COMMIT TRANSACTION;

                    SELECT @RemitenteId, @ReservaId, @EventoId;
                END TRY
                BEGIN CATCH
                    IF XACT_STATE() <> 0
                        ROLLBACK TRANSACTION;
                    THROW;
                END CATCH;
                """, conexion))
            {
                preparar.Parameters.Add(
                    "@Remitente", SqlDbType.NVarChar, 20
                ).Value = remitente;

                preparar.Parameters.Add(
                    "@Destinatario", SqlDbType.NVarChar, 20
                ).Value = destinatario;

                preparar.Parameters.Add(
                    "@Usuario", SqlDbType.NVarChar, 80
                ).Value = usuario;

                preparar.Parameters.Add(
                    "@MensajeId", SqlDbType.NVarChar, 255
                ).Value = mensajeId;

                await using var lector =
                    await preparar.ExecuteReaderAsync();

                Assert.True(await lector.ReadAsync());

                remitenteId = lector.GetInt32(0);
                reservaId = lector.GetInt32(1);
                eventoId = lector.GetInt64(2);
            }

            // Ejecutar el flujo completo.
            var primerResultado =
                await ejecutor.EjecutarUnaAsync();

            Assert.Equal(
                ResultadoEjecucionInicioWhatsApp.Preparada,
                primerResultado);

            // La solicitud ya preparada no debe
            // reclamarse nuevamente.
            var segundoResultado =
                await ejecutor.EjecutarUnaAsync();

            Assert.Equal(
                ResultadoEjecucionInicioWhatsApp.SinSolicitudes,
                segundoResultado);

            // Comprobar el estado final en SQL.
            await using var comprobar = new SqlCommand("""
                SELECT
                    s.Estado,
                    s.Intentos,
                    s.Arrendamiento,
                    s.VerificacionId,
                    v.Telefono,
                    v.Estado,
                    o.Id,
                    o.ContenidoProtegido,
                    o.Estado
                FROM dbo.SolicitudesInicioSesionWhatsApp s
                INNER JOIN dbo.VerificacionesTelefono v
                    ON v.Id = s.VerificacionId
                INNER JOIN dbo.OutboxVerificacionTelefono o
                    ON o.VerificacionId = v.Id
                WHERE s.EventoId = @EventoId
                  AND s.ReservaId = @ReservaId;
                """, conexion);

            comprobar.Parameters.Add(
                "@EventoId", SqlDbType.BigInt
            ).Value = eventoId;

            comprobar.Parameters.Add(
                "@ReservaId", SqlDbType.Int
            ).Value = reservaId;

            await using var resultado =
                await comprobar.ExecuteReaderAsync();

            Assert.True(await resultado.ReadAsync());

            Assert.Equal("Preparada", resultado.GetString(0));
            Assert.Equal(1, resultado.GetInt32(1));
            Assert.True(resultado.IsDBNull(2));

            int verificacionId = resultado.GetInt32(3);

            Assert.True(verificacionId > 0);
            Assert.Equal(
                destinatario, resultado.GetString(4));

            Assert.Equal("Pendiente", resultado.GetString(5));

            Guid idMensaje = resultado.GetGuid(6);
            byte[] cifrado = (byte[])resultado[7];

            try
            {
                Assert.Equal(
                    "Pendiente", resultado.GetString(8));

                var mensaje = protector.Abrir(
                    idMensaje,
                    verificacionId,
                    cifrado);

                Assert.Equal(
                    destinatario, mensaje.Telefono);

                Assert.Equal(
                    "codigo_inicio_sesion",
                    mensaje.Plantilla);

                Assert.Equal(2, mensaje.Variables.Length);
                Assert.Equal(verificacionId.ToString(), mensaje.Variables[1]);

                Assert.Equal(
                    6, mensaje.Variables[0].Length);

                Assert.All(
                    mensaje.Variables[0],
                    caracter => Assert.InRange(
                        caracter, '0', '9'));

                Assert.False(await resultado.ReadAsync());
            }
            finally
            {
                CryptographicOperations.ZeroMemory(cifrado);
            }

            // Cerrar el lector antes de ejecutar
            // nuevas operaciones en la misma conexion.
            await resultado.DisposeAsync();

            var almacen = new SqlOutboxVerificacionTelefono(configuracion);
            var tomados = await Task.WhenAll(almacen.TomarAsync(default), almacen.TomarAsync(default));
            var tomado = Assert.Single(tomados, x => x is not null)!;
            Assert.Equal(verificacionId, tomado.VerificacionId);
            Assert.True(await almacen.AutorizarAsync(tomado, default));
            await using (var revocar = new SqlCommand("UPDATE dbo.ConsentimientosPreReservaWhatsApp SET Estado=N'Revocado' WHERE ReservaId=@r",conexion))
            {
                revocar.Parameters.AddWithValue("@r",reservaId);
                await revocar.ExecuteNonQueryAsync();
                Assert.False(await almacen.AutorizarAsync(tomado,default));
                revocar.CommandText="UPDATE dbo.ConsentimientosPreReservaWhatsApp SET Estado=N'Confirmado' WHERE ReservaId=@r";
                await revocar.ExecuteNonQueryAsync();
            }
            Assert.False(await almacen.AutorizarAsync(tomado with { Arrendamiento=Guid.NewGuid() }, default));
            await Assert.ThrowsAsync<SqlException>(() => almacen.CerrarAsync(
                tomado with { Arrendamiento=Guid.NewGuid() }, new(EstadoEnvioWhatsApp.Aceptado,"wamid.ajeno"), default));
            await almacen.CerrarAsync(tomado, new(EstadoEnvioWhatsApp.Reintento,
                ReintentarUtc: DateTimeOffset.UtcNow.AddSeconds(-1)), default);
            var cliente = new ClienteVerificacionFalso();
            var consumidor = new ProcesadorVerificacionTelefono(almacen, protector, cliente, TimeProvider.System);
            Assert.True(await consumidor.ProcesarUnoAsync());
            Assert.False(await consumidor.ProcesarUnoAsync());
            Assert.Equal(1, cliente.Envios);
            Assert.Equal(verificacionId.ToString(), cliente.Variables![1]);
            await using (var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync())
            {
                // Eventos sinteticos dentro de una transaccion que se revierte.
                // Leido no se degrada por un fallo o entrega posteriores.
                foreach (var recibido in new[]{"read","failed","delivered"})
                {
                    await using var evento=new SqlCommand("""
                        INSERT dbo.WhatsAppWebhookEventos(ClaveEvento,TipoEvento,MensajeId,WhatsappId,EstadoMensaje)
                        VALUES(CRYPT_GEN_RANDOM(32),N'Estado',@meta,@telefono,@estado);
                        """,conexion,transaccion);
                    evento.Parameters.AddWithValue("@meta",cliente.Resultado.IdMensaje!);
                    evento.Parameters.AddWithValue("@telefono",destinatario.TrimStart('+'));
                    evento.Parameters.AddWithValue("@estado",recibido);
                    await evento.ExecuteNonQueryAsync();
                    await SqlOutboxVerificacionTelefono.ConciliarPendientesAsync(conexion,transaccion,default);
                    await using var consulta=new SqlCommand("SELECT Estado FROM dbo.OutboxVerificacionTelefono WHERE VerificacionId=@v",conexion,transaccion);
                    consulta.Parameters.AddWithValue("@v",verificacionId);
                    Assert.Equal("Leido",await consulta.ExecuteScalarAsync());
                }
                await transaccion.RollbackAsync();
            }
            await using (var estado = new SqlCommand("SELECT Estado FROM dbo.OutboxVerificacionTelefono WHERE VerificacionId=@v", conexion))
            {
                estado.Parameters.AddWithValue("@v",verificacionId);
                Assert.Equal("Aceptado", await estado.ExecuteScalarAsync());
            }

            // El codigo y la referencia recibidos por el cliente falso permiten
            // emitir una cookie real, y la verificacion no puede reutilizarse.
            using (var fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            {
                b.UseEnvironment("Development");
                b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>
                {
                    ["ConnectionStrings:SynchroDB"]=cadena,
                    ["VerificacionTelefono:ClaveHmac"]=configuracion["VerificacionTelefono:ClaveHmac"],
                    ["WhatsApp:Outbox:Habilitado"]="false",
                    ["WhatsApp:VerificacionOutbox:Habilitado"]="false",
                    ["WhatsApp:SolicitudesInicioWorker:Habilitado"]="false"
                }));
            }))
            {
                using var navegador=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
                await PreReservasWebTabletTests.CsrfAsync(navegador,"/api/destinatarios/csrf-token");
                using var login=await navegador.PostAsJsonAsync("/api/destinatarios/login",
                    new{verificacionId,codigo=cliente.Variables![0]});
                Assert.Equal(HttpStatusCode.OK,login.StatusCode);
                using var sesion=await navegador.GetAsync("/api/destinatarios/mi-sesion");
                Assert.Equal(HttpStatusCode.OK,sesion.StatusCode);
                using var otro=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
                await PreReservasWebTabletTests.CsrfAsync(otro,"/api/destinatarios/csrf-token");
                using var repetido=await otro.PostAsJsonAsync("/api/destinatarios/login",
                    new{verificacionId,codigo=cliente.Variables![0]});
                Assert.Equal(HttpStatusCode.Unauthorized,repetido.StatusCode);
            }

            // Recuperacion segura: un lease abandonado nunca se reenvia;
            // tampoco los codigos vencidos o ya usados.
            foreach (var escenario in new[]{"Indeterminado","Caducado","Cancelado"})
            {
                await using var ajustar=new SqlCommand("""
                    UPDATE dbo.OutboxVerificacionTelefono SET
                        Estado=CASE WHEN @estado=N'Indeterminado' THEN N'Enviando' ELSE N'Pendiente' END,
                        Arrendamiento=CASE WHEN @estado=N'Indeterminado' THEN NEWID() ELSE NULL END,
                        ArrendamientoHastaUtc=CASE WHEN @estado=N'Indeterminado' THEN DATEADD(second,-1,SYSUTCDATETIME()) ELSE NULL END,
                        ExpiraUtc=CASE WHEN @estado=N'Caducado' THEN DATEADD(second,-1,SYSUTCDATETIME()) ELSE DATEADD(minute,5,SYSUTCDATETIME()) END,
                        ContenidoProtegido=CRYPT_GEN_RANDOM(32)
                    WHERE VerificacionId=@v;
                    """,conexion);
                ajustar.Parameters.AddWithValue("@v",verificacionId);
                ajustar.Parameters.AddWithValue("@estado",escenario);
                await ajustar.ExecuteNonQueryAsync();
                Assert.Null(await almacen.TomarAsync(default));
                await using var consultar=new SqlCommand("SELECT Estado,ContenidoProtegido FROM dbo.OutboxVerificacionTelefono WHERE VerificacionId=@v",conexion);
                consultar.Parameters.AddWithValue("@v",verificacionId);
                await using var fila=await consultar.ExecuteReaderAsync();
                Assert.True(await fila.ReadAsync());
                Assert.Equal(escenario,fila.GetString(0));
                Assert.True(fila.IsDBNull(1));
            }

            // SEGUNDO ESCENARIO:

            // SEGUNDO ESCENARIO:
            // Una nueva solicitud llega cuando el
            // destinatario ya revoco su consentimiento.
            long eventoRevocadoId;

            await using (var crear = new SqlCommand("""
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 57180, 'Base no autorizada.', 1;

                IF NOT EXISTS (
                    SELECT 1 FROM sys.extended_properties
                    WHERE class = 0
                      AND name = N'SynchroFixtureDeposito'
                )
                    THROW 57181, 'Falta marca de pruebas.', 1;

                BEGIN TRY
                    BEGIN TRANSACTION;

                    UPDATE dbo.ConsentimientosPreReservaWhatsApp
                    SET Estado = N'Revocado'
                    WHERE ReservaId = @ReservaId
                      AND TelefonoWhatsApp = @Destinatario
                      AND Estado = N'Confirmado';

                    IF @@ROWCOUNT <> 1
                        THROW 57182,
                            'No se revoco el consentimiento.',
                            1;

                    INSERT dbo.WhatsAppWebhookEventos
                        (ClaveEvento, TipoEvento,
                         MensajeId, WhatsappId)
                    VALUES (
                        CRYPT_GEN_RANDOM(32),
                        N'Mensaje',
                        @MensajeRevocadoId,
                        SUBSTRING(@Destinatario, 2, 20)
                    );

                    DECLARE @NuevoEventoId BIGINT =
                        CONVERT(BIGINT, SCOPE_IDENTITY());

                    INSERT dbo.SolicitudesInicioSesionWhatsApp
                        (EventoId, ReservaId, ExpiraUtc)
                    VALUES (
                        @NuevoEventoId,
                        @ReservaId,
                        DATEADD(MINUTE, 5, SYSUTCDATETIME())
                    );

                    COMMIT TRANSACTION;

                    SELECT @NuevoEventoId;
                END TRY
                BEGIN CATCH
                    IF XACT_STATE() <> 0
                        ROLLBACK TRANSACTION;
                    THROW;
                END CATCH;
                """, conexion))
            {
                crear.Parameters.Add(
                    "@ReservaId", SqlDbType.Int
                ).Value = reservaId;

                crear.Parameters.Add(
                    "@Destinatario", SqlDbType.NVarChar, 20
                ).Value = destinatario;

                crear.Parameters.Add(
                    "@MensajeRevocadoId", SqlDbType.NVarChar, 255
                ).Value = mensajeRevocadoId;

                eventoRevocadoId = Convert.ToInt64(
                    await crear.ExecuteScalarAsync());
            }

            // El ejecutor no debe preparar otro codigo.
            Assert.Equal(
                ResultadoEjecucionInicioWhatsApp.Rechazada,
                await ejecutor.EjecutarUnaAsync());

            // Confirmar que se libero el arrendamiento,
            // sin crear una nueva verificacion.
            await using (var verificar = new SqlCommand("""
                SELECT
                    s.Estado,
                    s.Intentos,
                    s.Arrendamiento,
                    s.VerificacionId,
                    (
                        SELECT COUNT(*)
                        FROM dbo.VerificacionesTelefono
                        WHERE Telefono = @Destinatario
                          AND Finalidad =
                              N'InicioSesionDestinatario'
                    ) AS TotalVerificaciones,
                    (
                        SELECT COUNT(*)
                        FROM dbo.OutboxVerificacionTelefono o
                        INNER JOIN dbo.VerificacionesTelefono v
                            ON v.Id = o.VerificacionId
                        WHERE v.Telefono = @Destinatario
                          AND v.Finalidad =
                              N'InicioSesionDestinatario'
                    ) AS TotalMensajes
                FROM dbo.SolicitudesInicioSesionWhatsApp s
                WHERE s.EventoId = @EventoId
                  AND s.ReservaId = @ReservaId;
                """, conexion))
            {
                verificar.Parameters.Add(
                    "@EventoId", SqlDbType.BigInt
                ).Value = eventoRevocadoId;

                verificar.Parameters.Add(
                    "@ReservaId", SqlDbType.Int
                ).Value = reservaId;

                verificar.Parameters.Add(
                    "@Destinatario", SqlDbType.NVarChar, 20
                ).Value = destinatario;

                await using var lector =
                    await verificar.ExecuteReaderAsync();

                Assert.True(await lector.ReadAsync());

                Assert.Equal(
                    "Rechazada", lector.GetString(0));

                Assert.Equal(1, lector.GetInt32(1));

                Assert.True(lector.IsDBNull(2));
                Assert.True(lector.IsDBNull(3));

                // Sigue existiendo solamente el codigo
                // y el mensaje del escenario anterior.
                Assert.Equal(1, lector.GetInt32(4));
                Assert.Equal(1, lector.GetInt32(5));

                Assert.False(await lector.ReadAsync());
            }

            // No debe reclamarse otra vez.
            Assert.Equal(
                ResultadoEjecucionInicioWhatsApp.SinSolicitudes,
                await ejecutor.EjecutarUnaAsync());

        }
        finally
        {
            // Limpiar solamente los registros ficticios.
            if (remitenteId > 0 &&
                reservaId > 0 &&
                eventoId > 0)
            {
                await using var limpiar = new SqlCommand("""
                    SET XACT_ABORT ON;

                    IF DB_NAME() <> N'SynchroDB_Pruebas'
                        THROW 57172, 'Base no autorizada.', 1;

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.extended_properties
                        WHERE class = 0
                          AND name = N'SynchroFixtureDeposito'
                    )
                        THROW 57173, 'Falta marca de pruebas.', 1;

                    BEGIN TRY
                        BEGIN TRANSACTION;

                                                DELETE s
                        FROM dbo.SolicitudesInicioSesionWhatsApp s
                        INNER JOIN dbo.WhatsAppWebhookEventos e
                            ON e.Id = s.EventoId
                        WHERE s.ReservaId = @ReservaId
                          AND e.MensajeId IN (
                              @MensajeId,
                              @MensajeRevocadoId
                          );

                        DELETE o
                        FROM dbo.OutboxVerificacionTelefono o
                        INNER JOIN dbo.VerificacionesTelefono v
                            ON v.Id = o.VerificacionId
                        WHERE v.Telefono = @Destinatario
                          AND v.Finalidad =
                              N'InicioSesionDestinatario';

                        DELETE dbo.VerificacionesTelefono
                        WHERE Telefono = @Destinatario
                          AND Finalidad =
                              N'InicioSesionDestinatario';

                                              DELETE dbo.WhatsAppWebhookEventos
                        WHERE MensajeId IN (
                            @MensajeId,
                            @MensajeRevocadoId
                        )
                          AND WhatsappId =
                              SUBSTRING(@Destinatario, 2, 20);

                        DELETE dbo.ConsentimientosPreReservaWhatsApp
                        WHERE ReservaId = @ReservaId
                          AND TelefonoWhatsApp = @Destinatario;

                        DELETE dbo.PreReservasEncomiendasWeb
                        WHERE Id = @ReservaId
                          AND ClientePersonaId = @RemitenteId;

                        DELETE dbo.ClientesWebPrototipo
                        WHERE PersonaId = @RemitenteId
                          AND NombreUsuario = @Usuario;

                        DELETE dbo.Personas
                        WHERE TelefonoWhatsapp = @Destinatario
                          AND TieneCuenta = 0;

                        DELETE dbo.Personas
                        WHERE Id = @RemitenteId
                          AND TelefonoWhatsapp = @Remitente;

                        COMMIT TRANSACTION;
                    END TRY
                    BEGIN CATCH
                        IF XACT_STATE() <> 0
                            ROLLBACK TRANSACTION;
                        THROW;
                    END CATCH;
                    """, conexion);

                limpiar.Parameters.Add(
                    "@EventoId", SqlDbType.BigInt
                ).Value = eventoId;

                limpiar.Parameters.Add(
                    "@ReservaId", SqlDbType.Int
                ).Value = reservaId;

                limpiar.Parameters.Add(
                    "@RemitenteId", SqlDbType.Int
                ).Value = remitenteId;

                limpiar.Parameters.Add(
                    "@Remitente", SqlDbType.NVarChar, 20
                ).Value = remitente;

                limpiar.Parameters.Add(
                    "@Destinatario", SqlDbType.NVarChar, 20
                ).Value = destinatario;

                limpiar.Parameters.Add(
                    "@Usuario", SqlDbType.NVarChar, 80
                ).Value = usuario;

                limpiar.Parameters.Add(
                    "@MensajeId", SqlDbType.NVarChar, 255
                ).Value = mensajeId;

                limpiar.Parameters.Add(
                    "@MensajeRevocadoId", SqlDbType.NVarChar, 255
                ).Value = mensajeRevocadoId;

                await limpiar.ExecuteNonQueryAsync();
            }
        }
    }
}
