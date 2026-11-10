
-- SYNCHRO
-- Evidencias faciales del remitente
-- Exclusivo para SynchroDB_Pruebas.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 55301,
        'SEGURIDAD: base de datos incorrecta.', 1;

IF OBJECT_ID(
    N'dbo.PreReservasEncomiendasWeb', N'U'
) IS NULL
    THROW 55302,
        'No existe la tabla de pre-reservas.', 1;

IF OBJECT_ID(
    N'dbo.Terminales', N'U'
) IS NULL
    THROW 55303,
        'No existe la tabla de terminales.', 1;

IF OBJECT_ID(
    N'dbo.EvidenciasFacialesRemitente', N'U'
) IS NULL
BEGIN
    CREATE TABLE dbo.EvidenciasFacialesRemitente
    (
        EvidenciaId UNIQUEIDENTIFIER NOT NULL,
        ReservaId INT NOT NULL,
        RemitenteId INT NOT NULL,
        TerminalId INT NOT NULL,

        FechaEmisionUtc DATETIME2(3) NOT NULL,
        FechaExpiracionUtc DATETIME2(3) NOT NULL,

        RostroCoincide BIT NOT NULL,
        PruebaDeVidaSuperada BIT NOT NULL,

        Consumida BIT NOT NULL
            CONSTRAINT DF_EvidenciaFacial_Consumida
            DEFAULT (0),

        FechaConsumoUtc DATETIME2(3) NULL,

        CONSTRAINT PK_EvidenciasFacialesRemitente
            PRIMARY KEY (EvidenciaId),

        CONSTRAINT FK_EvidenciaFacial_Reserva
            FOREIGN KEY (ReservaId)
            REFERENCES dbo.PreReservasEncomiendasWeb(Id),

        CONSTRAINT FK_EvidenciaFacial_Terminal
            FOREIGN KEY (TerminalId)
            REFERENCES dbo.Terminales(Id),

        CONSTRAINT CK_EvidenciaFacial_Identificadores
            CHECK (
                ReservaId > 0
                AND RemitenteId > 0
                AND TerminalId > 0
            ),

        CONSTRAINT CK_EvidenciaFacial_Vigencia
            CHECK (
                DATEDIFF_BIG(
                    MILLISECOND,
                    FechaEmisionUtc,
                    FechaExpiracionUtc
                ) BETWEEN 1 AND 120000
            ),

        CONSTRAINT CK_EvidenciaFacial_Consumo
            CHECK (
                (Consumida = 0 AND FechaConsumoUtc IS NULL)
                OR
                (Consumida = 1 AND FechaConsumoUtc IS NOT NULL)
            )
    );

    CREATE INDEX IX_EvidenciaFacial_Validacion
        ON dbo.EvidenciasFacialesRemitente
        (
            ReservaId,
            RemitenteId,
            TerminalId,
            FechaExpiracionUtc
        )
        WHERE Consumida = 0;
END;

SELECT
    DB_NAME() AS BaseDeDatos,
    CASE
        WHEN OBJECT_ID(
            N'dbo.EvidenciasFacialesRemitente',
            N'U'
        ) IS NOT NULL
        THEN N'Preparada'
        ELSE N'No preparada'
    END AS EstadoTabla,
    (
        SELECT COUNT(*)
        FROM dbo.EvidenciasFacialesRemitente
    ) AS EvidenciasRegistradas;
