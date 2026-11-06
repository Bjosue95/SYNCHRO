
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Esta migracion es EXCLUSIVA de la base de pruebas.
IF DB_NAME() <> N'SynchroDB_Detalle_Pruebas'
    THROW 51100, 'Base de datos no autorizada.', 1;

IF OBJECT_ID(N'dbo.CodigosRetiro', N'U') IS NULL
    THROW 51101, 'No existe la tabla CodigosRetiro.', 1;

IF COLUMNPROPERTY(
    OBJECT_ID(N'dbo.CodigosRetiro'),
    N'Id',
    'IsIdentity'
) <> 1
    THROW 51102, 'Se requiere Id autoincremental.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.CodigosRetiro')
      AND name = N'UX_CodigosRetiro_UsoId'
      AND is_unique = 1
)
    THROW 51103, 'Falta el indice unico por encomienda.', 1;
GO

CREATE OR ALTER PROCEDURE dbo.EmitirPinRetiroSynchro
    @UsoId INT,
    @DestinatarioId INT,
    @PinHash VARBINARY(64),
    @PinSalt VARBINARY(32),
    @VigenciaMinutos INT = 1440
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Proteccion del entorno de pruebas.
    IF DB_NAME() <> N'SynchroDB_Detalle_Pruebas'
        THROW 51104, 'Operacion no autorizada.', 1;

    IF @UsoId <= 0 OR @UsoId IS NULL
       OR @DestinatarioId <= 0 OR @DestinatarioId IS NULL
        THROW 51105, 'Identificadores invalidos.', 1;

    IF @PinHash IS NULL OR DATALENGTH(@PinHash) <> 32
       OR @PinSalt IS NULL OR DATALENGTH(@PinSalt) <> 32
        THROW 51106, 'Datos criptograficos invalidos.', 1;

    -- Vigencia permitida: entre 5 minutos y 24 horas.
    IF @VigenciaMinutos IS NULL
       OR @VigenciaMinutos NOT BETWEEN 5 AND 1440
        THROW 51107, 'Vigencia no permitida.', 1;

    DECLARE @Ahora DATETIME2 = SYSUTCDATETIME();
    DECLARE @Estado NVARCHAR(20);
    DECLARE @TipoServicio NVARCHAR(20);
    DECLARE @FechaIngreso DATETIME2;
    DECLARE @FechaRetiro DATETIME2;

    DECLARE @CodigoId INT;
    DECLARE @VencimientoAnterior DATETIME2;
    DECLARE @FechaUsoAnterior DATETIME2;

    DECLARE @NuevoVencimiento DATETIME2 =
        DATEADD(MINUTE, @VigenciaMinutos, @Ahora);

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Bloquear la operacion durante la emision.
        SELECT
            @Estado = Estado,
            @TipoServicio = TipoServicio,
            @FechaIngreso = FechaIngreso,
            @FechaRetiro = FechaRetiro
        FROM dbo.UsosCasillero WITH (UPDLOCK, HOLDLOCK)
        WHERE Id = @UsoId;

        IF @Estado IS NULL
            THROW 51108, 'Encomienda inexistente.', 1;

        -- Solo encomiendas depositadas y no retiradas.
        IF @TipoServicio <> N'Encomienda'
           OR @Estado <> N'Ocupado'
           OR @FechaIngreso IS NULL
           OR @FechaRetiro IS NOT NULL
            THROW 51109, 'Encomienda no permite emitir PIN.', 1;

        -- Verificar destinatario correspondiente.
        IF NOT EXISTS (
            SELECT 1
            FROM dbo.ParticipantesUso WITH (HOLDLOCK)
            WHERE UsoId = @UsoId
              AND PersonaId = @DestinatarioId
              AND Rol = N'Destinatario'
        )
            THROW 51110, 'Destinatario no autorizado.', 1;

        -- Buscar un PIN anterior de esta encomienda.
        SELECT
            @CodigoId = Id,
            @VencimientoAnterior = FechaVencimiento,
            @FechaUsoAnterior = FechaUso
        FROM dbo.CodigosRetiro WITH (UPDLOCK, HOLDLOCK)
        WHERE UsoId = @UsoId;

        IF @CodigoId IS NOT NULL
        BEGIN
            -- Impedir reutilizar una operacion ya consumida.
            IF @FechaUsoAnterior IS NOT NULL
                THROW 51111, 'El PIN anterior ya fue utilizado.', 1;

            -- No permitir regeneracion mientras este vigente.
            IF @VencimientoAnterior > @Ahora
                THROW 51112, 'Ya existe un PIN vigente.', 1;

-- Renovar un PIN vencido sin reiniciar
-- sus intentos fallidos acumulados.
UPDATE dbo.CodigosRetiro
SET PinHash = @PinHash,
    PinSalt = @PinSalt,
    FechaCreacion = @Ahora,
    FechaVencimiento = @NuevoVencimiento,
    FechaUso = NULL
WHERE Id = @CodigoId
  AND IntentosFallidos < 5;

IF @@ROWCOUNT <> 1
    THROW 51113,
        'PIN bloqueado: no se permite renovar automaticamente.',
        1;

        END
        ELSE
        BEGIN
            -- Registrar el primer PIN de la encomienda.
            INSERT INTO dbo.CodigosRetiro
            (
                UsoId,
                PinHash,
                PinSalt,
                FechaCreacion,
                FechaVencimiento,
                IntentosFallidos,
                FechaUso
            )
            VALUES
            (
                @UsoId,
                @PinHash,
                @PinSalt,
                @Ahora,
                @NuevoVencimiento,
                0,
                NULL
            );
        END;

        COMMIT TRANSACTION;

        -- Nunca devolver el hash ni el salt.
        SELECT
            @UsoId AS UsoId,
            N'Emitido' AS Estado,
            @NuevoVencimiento AS FechaVencimiento;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;
GO

PRINT 'PROCEDIMIENTO DE EMISION PIN CREADO CORRECTAMENTE';
GO
