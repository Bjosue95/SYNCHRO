SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

-- Solo permitimos instalar este procedimiento
-- en nuestra base de pruebas.
IF DB_NAME() <> N'SynchroDB_Detalle_Pruebas'
    THROW 51400, 'Base de datos no autorizada.', 1;

IF OBJECT_ID(N'dbo.CodigosRetiro', N'U') IS NULL
    THROW 51401, 'Falta la tabla CodigosRetiro.', 1;

IF OBJECT_ID(N'dbo.VerificacionesRetiro', N'U') IS NULL
    THROW 51402, 'Falta la tabla VerificacionesRetiro.', 1;
GO

CREATE OR ALTER PROCEDURE dbo.ValidarPinRetiroSynchro
    @UsoId INT,
    @DestinatarioId INT,
    @PinHashCandidato VARBINARY(32),
    @PinSaltLeido VARBINARY(32)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_NAME() <> N'SynchroDB_Detalle_Pruebas'
        THROW 51403, 'Operacion no autorizada.', 1;

    IF @UsoId IS NULL OR @UsoId <= 0
       OR @DestinatarioId IS NULL OR @DestinatarioId <= 0
        THROW 51404, 'Identificadores invalidos.', 1;

    IF @PinHashCandidato IS NULL
       OR DATALENGTH(@PinHashCandidato) <> 32
       OR @PinSaltLeido IS NULL
       OR DATALENGTH(@PinSaltLeido) <> 32
        THROW 51405, 'Datos criptograficos invalidos.', 1;

    DECLARE @Resultado NVARCHAR(20) = N'NoDisponible';
    DECLARE @CodigoId INT;
    DECLARE @HashGuardado VARBINARY(64);
    DECLARE @SaltGuardado VARBINARY(32);
    DECLARE @Intentos INT;
    DECLARE @FechaCreacion DATETIME2;
    DECLARE @FechaVencimiento DATETIME2;
    DECLARE @FechaUso DATETIME2;
    DECLARE @Ahora DATETIME2;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Comprobar operacion y destinatario autorizado.
        IF EXISTS (
            SELECT 1
            FROM dbo.UsosCasillero u WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.ParticipantesUso p WITH (HOLDLOCK)
                ON p.UsoId = u.Id
               AND p.Rol = N'Destinatario'
            WHERE u.Id = @UsoId
              AND p.PersonaId = @DestinatarioId
              AND u.TipoServicio = N'Encomienda'
              AND u.Estado = N'Ocupado'
              AND u.FechaIngreso IS NOT NULL
              AND u.FechaRetiro IS NULL
        )
        BEGIN
            -- Bloquear el registro durante la validacion.
            SELECT
                @CodigoId = Id,
                @HashGuardado = PinHash,
                @SaltGuardado = PinSalt,
                @Intentos = IntentosFallidos,
                @FechaCreacion = FechaCreacion,
                @FechaVencimiento = FechaVencimiento,
                @FechaUso = FechaUso
            FROM dbo.CodigosRetiro WITH (UPDLOCK, HOLDLOCK)
            WHERE UsoId = @UsoId;

            SET @Ahora = SYSUTCDATETIME();

            IF @CodigoId IS NULL
                SET @Resultado = N'NoDisponible';

            ELSE IF @FechaUso IS NOT NULL
                SET @Resultado = N'Utilizado';

            ELSE IF @FechaCreacion > @Ahora
                 OR @FechaVencimiento <= @Ahora
                SET @Resultado = N'Vencido';

            ELSE IF @Intentos >= 5
                SET @Resultado = N'Bloqueado';

            -- Si el PIN fue renovado despues de obtener
            -- su salt, no validar contra otro registro.
            ELSE IF @SaltGuardado <> @PinSaltLeido
                SET @Resultado = N'NoDisponible';

            ELSE IF @HashGuardado = @PinHashCandidato
            BEGIN
                -- Consumir el PIN una sola vez.
                UPDATE dbo.CodigosRetiro
                SET FechaUso = @Ahora
                WHERE Id = @CodigoId
                  AND FechaUso IS NULL
                  AND IntentosFallidos < 5
                  AND FechaVencimiento > @Ahora;

                IF @@ROWCOUNT <> 1
                    THROW 51406, 'No se pudo consumir el PIN.', 1;

                -- Solo una validacion exitosa puede
                -- generar esta evidencia aprobada.
                INSERT INTO dbo.VerificacionesRetiro
                (
                    UsoId,
                    PersonaId,
                    Metodo,
                    Resultado,
                    FechaVerificacion
                )
                VALUES
                (
                    @UsoId,
                    @DestinatarioId,
                    N'PIN',
                    N'Aprobado',
                    @Ahora
                );

                SET @Resultado = N'Aprobado';
            END
            ELSE
            BEGIN
                -- Registrar un intento incorrecto.
                UPDATE dbo.CodigosRetiro
                SET IntentosFallidos = IntentosFallidos + 1
                WHERE Id = @CodigoId
                  AND IntentosFallidos < 5;

                IF @@ROWCOUNT <> 1
                    THROW 51407, 'No se registro el intento.', 1;

                SET @Resultado = CASE
                    WHEN @Intentos + 1 >= 5 THEN N'Bloqueado'
                    ELSE N'Incorrecto'
                END;
            END;
        END;

        COMMIT TRANSACTION;

        -- Nunca devolver hash, salt ni el PIN original.
        SELECT @Resultado AS Resultado;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;
GO

PRINT 'VALIDACION PIN DE RETIRO INSTALADA EN BASE DE PRUEBAS';
GO
