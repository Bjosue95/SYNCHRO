SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 59201, 'Base incorrecta.', 1;

IF COALESCE(CONVERT(INT, SERVERPROPERTY('IsLocalDB')), 0) <> 1
    THROW 59202, 'Solo LocalDB.', 1;

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE class = 0
      AND name = N'SynchroFixtureDeposito'
      AND CONVERT(NVARCHAR(10), value) = N'1'
)
    THROW 59203, 'Base de pruebas no autorizada.', 1;

IF OBJECT_ID(N'dbo.AuditoriaCobrosRetiroPersonal', N'U') IS NULL
    THROW 59204, 'Tabla de auditoria inexistente.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @NombreCheck SYSNAME;
    DECLARE @DefCheck NVARCHAR(MAX);
    DECLARE @Cantidad INT;

    SELECT @Cantidad = COUNT(*)
    FROM sys.check_constraints
    WHERE parent_object_id =
          OBJECT_ID(N'dbo.AuditoriaCobrosRetiroPersonal')
      AND definition LIKE N'%MetodoPago%'
      AND definition LIKE N'%EfectivoSimulado%'
      AND definition LIKE N'%TarjetaSimulada%';

    IF @Cantidad <> 1
        THROW 59205, 'Restriccion de pago no identificada.', 1;

    SELECT
        @NombreCheck = name,
        @DefCheck = definition
    FROM sys.check_constraints
    WHERE parent_object_id =
          OBJECT_ID(N'dbo.AuditoriaCobrosRetiroPersonal')
      AND definition LIKE N'%MetodoPago%'
      AND definition LIKE N'%EfectivoSimulado%'
      AND definition LIKE N'%TarjetaSimulada%';

    IF @DefCheck NOT LIKE N'%NfcSimulado%'
    BEGIN
        DECLARE @Eliminar NVARCHAR(MAX);

        SET @Eliminar =
            N'ALTER TABLE dbo.AuditoriaCobrosRetiroPersonal ' +
            N'DROP CONSTRAINT ' + QUOTENAME(@NombreCheck);

        EXEC sys.sp_executesql @Eliminar;

        ALTER TABLE dbo.AuditoriaCobrosRetiroPersonal
        WITH CHECK ADD CONSTRAINT CK_AuditoriaRetiro_Metodos
        CHECK (
            MetodoPago IN (
                N'EfectivoSimulado',
                N'TarjetaSimulada',
                N'NfcSimulado'
            )
        );
    END;

    EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE
    dbo.ConfirmarCobroRetiroPersonalAuditableSynchro
    @CobroId UNIQUEIDENTIFIER,
    @Metodo NVARCHAR(24),
    @OperadorId INT,
    @PagoComprobado BIT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_NAME() <> N''SynchroDB_Pruebas''
        THROW 57900, ''Base no autorizada.'', 1;

    IF NOT EXISTS (
        SELECT 1 FROM sys.extended_properties
        WHERE class=0
          AND name=N''SynchroFixtureDeposito''
          AND CONVERT(nvarchar(10),value)=N''1''
    )
        THROW 57901, ''Falta marcador de pruebas.'', 1;

    IF @CobroId IS NULL OR
       @OperadorId IS NULL OR @OperadorId <= 0 OR
       @PagoComprobado IS NULL OR @PagoComprobado <> 1 OR
       @Metodo IS NULL OR
       @Metodo NOT IN (
           N''EfectivoSimulado'',
           N''TarjetaSimulada'', N''NfcSimulado''
       )
        THROW 57911, ''Confirmacion invalida.'', 1;

    DECLARE @Actualizados TABLE
    (
        CobroId UNIQUEIDENTIFIER,
        UsoId INT,
        ImporteHnl DECIMAL(12,2),
        MetodoPago NVARCHAR(24),
        ConfirmadoUtc DATETIME2(3)
    );

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE c
        SET Estado=N''ConfirmadoSimulado'',
            MetodoPago=@Metodo,
            ConfirmadoUtc=SYSUTCDATETIME()
        OUTPUT
            INSERTED.CobroId,
            INSERTED.UsoId,
            INSERTED.ImporteHnl,
            INSERTED.MetodoPago,
            INSERTED.ConfirmadoUtc
        INTO @Actualizados
        FROM dbo.CobrosRetiroPersonal c
            WITH (UPDLOCK,HOLDLOCK)
        JOIN dbo.UsosCasillero u
            ON u.Id=c.UsoId
        JOIN dbo.Casilleros cas
            ON cas.Codigo=u.CodigoCasillero
        WHERE c.CobroId=@CobroId
          AND c.Estado=N''Pendiente''
          AND c.ConfirmadoUtc IS NULL
          AND c.FinalizadoUtc IS NULL
          AND c.ImporteHnl>0
          AND c.MinutosUtilizados>0
          AND u.TipoServicio=N''ResguardoPersonal''
          AND u.OrigenReserva=N''Terminal''
          AND u.Estado=N''Ocupado''
          AND u.FechaIngreso IS NOT NULL
          AND u.FechaRetiro IS NULL
          AND cas.Estado=N''Ocupado''
          AND EXISTS (
              SELECT 1 FROM dbo.CodigosRetiroPersonal p
              WHERE p.UsoId=u.Id
                AND p.FechaUso IS NULL
                AND p.IntentosFallidos<5
                AND (
                    p.FechaVencimiento IS NULL OR
                    p.FechaVencimiento>SYSUTCDATETIME()
                )
          )
          AND EXISTS (
              SELECT 1
              FROM dbo.AutorizacionesWhatsAppManuales a
              WHERE a.UsoId=u.Id
                AND a.Estado=N''Confirmado''
          );

        IF (SELECT COUNT(*) FROM @Actualizados) <> 1
            THROW 57910, ''Cobro no disponible.'', 1;

        INSERT dbo.AuditoriaCobrosRetiroPersonal
        (
            CobroId,
            UsoId,
            CodigoCasillero,
            OperadorId,
            ImporteHnl,
            MetodoPago,
            ConfirmadoUtc
        )
        SELECT
            x.CobroId,
            x.UsoId,
            u.CodigoCasillero,
            @OperadorId,
            x.ImporteHnl,
            x.MetodoPago,
            x.ConfirmadoUtc
        FROM @Actualizados x
        JOIN dbo.UsosCasillero u
            ON u.Id=x.UsoId;

        IF @@ROWCOUNT<>1
            THROW 57910, ''Auditoria no registrada.'', 1;

        COMMIT TRANSACTION;

        SELECT
            x.CobroId,
            x.ImporteHnl,
            N''ConfirmadoSimulado'' AS Estado,
            u.CodigoCasillero
        FROM @Actualizados x
        JOIN dbo.UsosCasillero u
            ON u.Id=x.UsoId;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT>0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
';

    EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE dbo.FinalizarRetiroPersonalPagadoSynchro
    @UsoId INT,
    @CobroId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_NAME() <> N''SynchroDB_Pruebas''
        THROW 57700, ''Base incorrecta.'', 1;

    IF @UsoId IS NULL OR @UsoId <= 0
       OR @CobroId IS NULL
       OR @CobroId = ''00000000-0000-0000-0000-000000000000''
        THROW 57703, ''Operacion invalida.'', 1;

    DECLARE
        @Codigo NVARCHAR(10),
        @EstadoCasillero NVARCHAR(20),
        @EstadoUso NVARCHAR(20),
        @Tipo NVARCHAR(30),
        @Origen NVARCHAR(20),
        @Ingreso DATETIME2(7),
        @Retiro DATETIME2(7),
        @FechaUsoPin DATETIME2(7),
        @VencePin DATETIME2(7),
        @IntentosPin INT,
        @EstadoPago NVARCHAR(24),
        @Importe DECIMAL(12,2),
        @MetodoPago NVARCHAR(24),
        @ConfirmadoUtc DATETIME2(3),
        @Ahora DATETIME2(7) = SYSUTCDATETIME();

    SELECT @Codigo = CodigoCasillero
    FROM dbo.UsosCasillero
    WHERE Id = @UsoId;

    IF @Codigo IS NULL
        THROW 57704, ''Uso no encontrado.'', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Mantener el orden de bloqueo del retiro existente.
        SELECT @EstadoCasillero = Estado
        FROM dbo.Casilleros WITH (UPDLOCK, HOLDLOCK)
        WHERE Codigo = @Codigo;

        SELECT
            @EstadoUso = Estado,
            @Tipo = TipoServicio,
            @Origen = OrigenReserva,
            @Ingreso = FechaIngreso,
            @Retiro = FechaRetiro
        FROM dbo.UsosCasillero WITH (UPDLOCK, HOLDLOCK)
        WHERE Id = @UsoId
          AND CodigoCasillero = @Codigo;

        SELECT
            @FechaUsoPin = FechaUso,
            @VencePin = FechaVencimiento,
            @IntentosPin = IntentosFallidos
        FROM dbo.CodigosRetiroPersonal WITH (UPDLOCK, HOLDLOCK)
        WHERE UsoId = @UsoId;

        SELECT
            @EstadoPago = Estado,
            @Importe = ImporteHnl,
            @MetodoPago = MetodoPago,
            @ConfirmadoUtc = ConfirmadoUtc
        FROM dbo.CobrosRetiroPersonal WITH (UPDLOCK, HOLDLOCK)
        WHERE UsoId = @UsoId
          AND CobroId = @CobroId;

        -- El casillero debe continuar ocupado.
        IF @EstadoCasillero IS NULL
           OR @EstadoCasillero <> N''Ocupado''
           OR @EstadoUso IS NULL
           OR @EstadoUso <> N''Ocupado''
           OR @Tipo IS NULL
           OR @Tipo <> N''ResguardoPersonal''
           OR @Origen IS NULL
           OR @Origen <> N''Terminal''
           OR @Ingreso IS NULL
           OR @Retiro IS NOT NULL
            THROW 57705, ''Retiro no disponible.'', 1;

        IF NOT EXISTS (
            SELECT 1
            FROM dbo.AutorizacionesWhatsAppManuales
            WHERE UsoId = @UsoId
              AND Estado = N''Confirmado''
        )
            THROW 57706, ''Autorizacion no disponible.'', 1;

        -- No finalizar con PIN consumido, bloqueado o vencido.
        IF @IntentosPin IS NULL
           OR @IntentosPin >= 5
           OR @FechaUsoPin IS NOT NULL
           OR (@VencePin IS NOT NULL AND @VencePin <= @Ahora)
            THROW 57707, ''PIN no disponible.'', 1;

        -- Exigir un cobro simulado confirmado y valido.
        IF @EstadoPago IS NULL
           OR @EstadoPago <> N''ConfirmadoSimulado''
           OR @ConfirmadoUtc IS NULL
           OR @ConfirmadoUtc > @Ahora
           OR @Importe IS NULL
           OR @Importe <= 0
           OR @MetodoPago IS NULL
           OR @MetodoPago NOT IN (
                N''EfectivoSimulado'',
                N''TarjetaSimulada'', N''NfcSimulado''
           )
            THROW 57708, ''Pago no confirmado.'', 1;

        IF EXISTS (
            SELECT 1 FROM dbo.UsosCasillero
            WHERE CodigoCasillero = @Codigo
              AND Id <> @UsoId
              AND Estado IN (N''Reservado'', N''Ocupado'')
              AND FechaRetiro IS NULL
        )
            THROW 57709, ''Existe otra operacion activa.'', 1;

        UPDATE dbo.UsosCasillero
        SET Estado = N''Finalizado'',
            FechaRetiro = @Ahora
        WHERE Id = @UsoId
          AND Estado = N''Ocupado''
          AND FechaRetiro IS NULL;

        IF @@ROWCOUNT <> 1
            THROW 57710, ''No se pudo finalizar el uso.'', 1;

        UPDATE dbo.Casilleros
        SET Estado = N''Disponible''
        WHERE Codigo = @Codigo
          AND Estado = N''Ocupado'';

        IF @@ROWCOUNT <> 1
            THROW 57711, ''No se pudo liberar el casillero.'', 1;

        UPDATE dbo.CodigosRetiroPersonal
        SET FechaUso = @Ahora
        WHERE UsoId = @UsoId
          AND FechaUso IS NULL;

        IF @@ROWCOUNT <> 1
            THROW 57712, ''No se pudo consumir el PIN.'', 1;

        UPDATE dbo.CobrosRetiroPersonal
        SET Estado = N''Aplicado'',
            FinalizadoUtc = @Ahora
        WHERE UsoId = @UsoId
          AND CobroId = @CobroId
          AND Estado = N''ConfirmadoSimulado'';

        IF @@ROWCOUNT <> 1
            THROW 57713, ''No se pudo aplicar el cobro.'', 1;

        -- SYNCHRO-LIMPIEZA-ROSTRO-RETIRO-PAGADO-V1
        -- Eliminar la evidencia facial simulada en la misma
        -- transaccion que consume el PIN y libera el casillero.
        DELETE FROM dbo.RostrosRetiroPersonalManual
        WHERE UsoId = @UsoId;

        IF @@ROWCOUNT <> 1
            THROW 57714, ''No se pudo consumir la evidencia facial.'', 1;
        COMMIT TRANSACTION;

        SELECT
            CAST(1 AS BIT) AS RetiroConfirmado,
            @Codigo AS CodigoCasillero,
            N''Disponible'' AS EstadoCasillero,
            CAST(1 AS BIT) AS PagoSimulado,
            @Importe AS ImporteHnl,
            @Ahora AS FechaRetiro, @MetodoPago AS MetodoPago;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;';

    IF (
        SELECT COUNT(*)
        FROM sys.procedures p
        JOIN sys.sql_modules m ON m.object_id = p.object_id
        WHERE SCHEMA_NAME(p.schema_id) = N'dbo'
          AND p.name IN (
              N'ConfirmarCobroRetiroPersonalAuditableSynchro',
              N'FinalizarRetiroPersonalPagadoSynchro'
          )
          AND m.definition LIKE N'%NfcSimulado%'
    ) <> 2
        THROW 59206, 'Procedimientos NFC no actualizados.', 1;

    IF OBJECT_DEFINITION(
        OBJECT_ID(N'dbo.FinalizarRetiroPersonalPagadoSynchro')
    ) NOT LIKE N'%@MetodoPago AS MetodoPago%'
        THROW 59207, 'Falta metodo en respuesta final.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;