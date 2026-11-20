IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 59001, 'Esta migracion solo permite la base aislada.', 1;

IF OBJECT_ID(N'dbo.RostrosRetiroPersonalV3', N'U') IS NULL
    THROW 59002, 'La tabla V3 no existe.', 1;

IF OBJECT_ID(N'dbo.TR_Synchro_LimpiarRostroV3', N'TR') IS NULL
BEGIN
    EXEC(N'
        CREATE TRIGGER dbo.TR_Synchro_LimpiarRostroV3
        ON dbo.UsosCasillero
        AFTER UPDATE
        AS
        BEGIN
            SET NOCOUNT ON;

            DELETE r
            FROM dbo.RostrosRetiroPersonalV3 AS r
            INNER JOIN inserted AS i
                ON i.Id = r.UsoId
            WHERE i.FechaRetiro IS NOT NULL
               OR i.Estado IN (
                   N''Finalizado'', N''Finalizada'',
                   N''Cancelado'', N''Cancelada'',
                   N''Vencido'', N''Vencida'',
                   N''Anulado'', N''Anulada''
               );
        END;
    ');
END;