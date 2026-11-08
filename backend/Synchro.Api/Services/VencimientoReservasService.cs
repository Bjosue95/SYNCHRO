
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

namespace Synchro.Api.Services;

public sealed class VencimientoReservasService : BackgroundService
{
    private readonly IConfiguration _configuracion;
    private readonly ILogger<VencimientoReservasService> _logger;

    public VencimientoReservasService(
        IConfiguration configuracion,
        ILogger<VencimientoReservasService> logger)
    {
        _configuracion = configuracion;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var temporizador = new PeriodicTimer(
            TimeSpan.FromMinutes(1));

        try
        {
            do
            {
                try
                {
                    await ProcesarReservasVencidas(stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error al revisar reservas vencidas de SYNCHRO.");
                }
            }
            while (
                await temporizador.WaitForNextTickAsync(stoppingToken)
            );
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // El backend se esta cerrando normalmente.
        }
    }

    private async Task ProcesarReservasVencidas(
        CancellationToken cancellationToken)
    {
        var cadena = _configuracion.GetConnectionString("SynchroDB");

        if (string.IsNullOrWhiteSpace(cadena))
        {
            throw new InvalidOperationException(
                "No se encontro la conexion a SynchroDB.");
        }

        await using var conexion = new SqlConnection(cadena);

        await conexion.OpenAsync(cancellationToken);

        var reservasVencidas = new List<int>();

        // Consultar solamente reservas vencidas de la terminal.
        await using (var consulta = new SqlCommand(
            @"
            SELECT Id
            FROM dbo.UsosCasillero
            WHERE OrigenReserva = N'Terminal'
              AND Estado = N'Reservado'
              AND FechaIngreso IS NULL
              AND FechaVencimiento IS NOT NULL
              AND FechaVencimiento <= SYSUTCDATETIME();
            ",
            conexion))
        {
            await using var lector =
                await consulta.ExecuteReaderAsync(cancellationToken);

            while (await lector.ReadAsync(cancellationToken))
            {
                reservasVencidas.Add(lector.GetInt32(0));
            }
        }

        // Reutilizar la cancelacion segura que ya existe.
        foreach (var idUso in reservasVencidas)
        {
            await using var comando = new SqlCommand(
                "dbo.CancelarReservaCasilleroSynchro",
                conexion);

            comando.CommandType = CommandType.StoredProcedure;

            comando.Parameters.Add(
                "@UsoId",
                SqlDbType.Int
            ).Value = idUso;

            try
            {
                await comando.ExecuteNonQueryAsync(cancellationToken);

                _logger.LogInformation(
                    "Reserva vencida cancelada. IdUso: {IdUso}",
                    idUso);
            }
            catch (SqlException ex)
                when (ex.Number is 50031 or 50033 or 50034)
            {
                // La reserva pudo cambiar de estado
                // mientras se realizaba la comprobacion.
                _logger.LogInformation(
                    "La reserva {IdUso} ya no puede cancelarse: {Numero}",
                    idUso,
                    ex.Number);
            }
        }
    }
}
