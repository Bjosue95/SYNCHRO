-- Impacto revisado: ocho tablas nuevas. No modifica ni elimina datos o tablas existentes.
-- Exclusivo de SynchroDB_Pruebas en (localdb)\SYNCHRO_Aislado; el ejecutor verifica la instancia.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'SynchroDB_Pruebas' THROW 59100,'Base de datos no permitida.',1;
IF OBJECT_ID(N'dbo.PreReservasEncomiendasWeb') IS NULL OR OBJECT_ID(N'dbo.ClientesWebPrototipo') IS NULL
 OR OBJECT_ID(N'dbo.UsosCasillero') IS NULL OR OBJECT_ID(N'dbo.Administradores') IS NULL
 THROW 59101,'Faltan prerrequisitos de encomiendas.',1;
IF OBJECT_ID(N'dbo.EncomiendasOperativas') IS NOT NULL
BEGIN
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'SynchroEncomiendasOperativas' AND CONVERT(nvarchar(10),value)=N'1')
  THROW 59102,'Esquema existente sin version reconocida; revisar manualmente.',1;
 RETURN;
END;
BEGIN TRANSACTION;
CREATE TABLE dbo.EncomiendasOperativas(
 ReservaId int NOT NULL PRIMARY KEY REFERENCES dbo.PreReservasEncomiendasWeb(Id),
 DestinatarioId int NOT NULL REFERENCES dbo.Personas(Id),
 Estado nvarchar(30) NOT NULL CONSTRAINT CK_EncomiendaOperativaEstado CHECK(Estado IN(N'Preparacion',N'ListaParaDeposito',N'Depositada',N'Retirada',N'Rechazada',N'Cancelada')),
 UsoId int NULL REFERENCES dbo.UsosCasillero(Id), TerminalId int NULL REFERENCES dbo.Terminales(Id),
 Version bigint NOT NULL DEFAULT 1, CreadaUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 FechaDepositoUtc datetime2(3) NULL, FechaEntregaUtc datetime2(3) NULL,
 PagoEstado nvarchar(30) NOT NULL DEFAULT N'Pendiente' CHECK(PagoEstado IN(N'Pendiente',N'ConfirmadoManual',N'Rechazado')),
 ImporteHnl decimal(18,2) NULL CHECK(ImporteHnl>0 AND ImporteHnl<=100000), MetodoPago nvarchar(30) NULL,
 PagoReferencia nvarchar(160) NULL, JustificacionImporte nvarchar(500) NULL,
 PagoAdminId int NULL REFERENCES dbo.Administradores(Id), PagoConfirmadoUtc datetime2(3) NULL,
 CONSTRAINT CK_EncomiendaPagoManual CHECK(PagoEstado<>N'ConfirmadoManual' OR
  (ImporteHnl IS NOT NULL AND ImporteHnl>0 AND MetodoPago IS NOT NULL AND LEN(MetodoPago)>0
   AND PagoReferencia IS NOT NULL AND LEN(PagoReferencia)>0 AND PagoAdminId IS NOT NULL AND PagoConfirmadoUtc IS NOT NULL))
);
CREATE UNIQUE INDEX UX_EncomiendaUso ON dbo.EncomiendasOperativas(UsoId) WHERE UsoId IS NOT NULL;
CREATE TABLE dbo.ParticipantesEncomiendaOperativa(
 ReservaId int NOT NULL REFERENCES dbo.EncomiendasOperativas(ReservaId),
 Rol nvarchar(12) NOT NULL CHECK(Rol IN(N'Remitente',N'Destinatario')),
 PersonaId int NOT NULL REFERENCES dbo.Personas(Id), Telefono nvarchar(20) NOT NULL,
 Consentimiento nvarchar(20) NOT NULL DEFAULT N'Pendiente' CHECK(Consentimiento IN(N'Pendiente',N'Confirmado',N'Rechazado',N'Revocado')),
 AceptaEncomienda bit NOT NULL DEFAULT 0, CodigoSolicitud uniqueidentifier NOT NULL,
 SolicitadoUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), VenceUtc datetime2(3) NOT NULL,
 MensajeReferencia nvarchar(160) NULL, VerificadoUtc datetime2(3) NULL,
 VerificadoAdminId int NULL REFERENCES dbo.Administradores(Id),
 PRIMARY KEY(ReservaId,Rol), CONSTRAINT CK_EncomiendaConsentimientoVerificado CHECK(Consentimiento<>N'Confirmado' OR
  (MensajeReferencia IS NOT NULL AND VerificadoUtc IS NOT NULL AND VerificadoAdminId IS NOT NULL))
);
CREATE UNIQUE INDEX UX_EncomiendaMensajeConsentimiento ON dbo.ParticipantesEncomiendaOperativa(MensajeReferencia) WHERE MensajeReferencia IS NOT NULL;
CREATE TABLE dbo.PinesEncomiendaOperativa(
 Id uniqueidentifier NOT NULL PRIMARY KEY, ReservaId int NOT NULL REFERENCES dbo.EncomiendasOperativas(ReservaId),
 Finalidad nvarchar(10) NOT NULL CHECK(Finalidad IN(N'Deposito',N'Retiro')),
 PinHash varbinary(32) NOT NULL, PinSalt varbinary(32) NOT NULL,
 Activo bit NOT NULL DEFAULT 1, IntentosFallidos int NOT NULL DEFAULT 0 CHECK(IntentosFallidos BETWEEN 0 AND 5),
 CreadoUtc datetime2(3) NOT NULL, VenceUtc datetime2(3) NOT NULL,
 ConsumidoUtc datetime2(3) NULL, RevocadoUtc datetime2(3) NULL,
 EntregadoUtc datetime2(3) NULL, MensajeReferencia nvarchar(160) NULL,
 AdminEmisionId int NOT NULL REFERENCES dbo.Administradores(Id), AdminEntregaId int NULL REFERENCES dbo.Administradores(Id)
);
CREATE UNIQUE INDEX UX_EncomiendaPinActivo ON dbo.PinesEncomiendaOperativa(ReservaId,Finalidad) WHERE Activo=1;
CREATE TABLE dbo.ReferenciasEncomiendaV3(
 PersonaId int NOT NULL PRIMARY KEY REFERENCES dbo.ClientesWebPrototipo(PersonaId),
 ReferenciaId uniqueidentifier NOT NULL UNIQUE, DescriptorProtegido varbinary(max) NOT NULL,
 Modelo nvarchar(60) NOT NULL, VersionConsentimiento nvarchar(80) NOT NULL,
 ConsentidoUtc datetime2(3) NOT NULL, RevalidarUtc datetime2(3) NOT NULL
);
CREATE TABLE dbo.EvidenciasEncomiendaOperativa(
 Id uniqueidentifier NOT NULL PRIMARY KEY, ReservaId int NOT NULL REFERENCES dbo.EncomiendasOperativas(ReservaId),
 TerminalId int NOT NULL REFERENCES dbo.Terminales(Id), PersonaId int NOT NULL REFERENCES dbo.Personas(Id),
 Tipo nvarchar(30) NOT NULL CHECK(Tipo IN(N'VerificacionRemitente',N'CapturaDestinatario')),
 VersionConsentimiento nvarchar(80) NOT NULL, EmitidaUtc datetime2(3) NOT NULL, VenceUtc datetime2(3) NOT NULL,
 ConsumidaUtc datetime2(3) NULL, ReferenciaId uniqueidentifier NULL,
 ComparacionRemitenteAprobada bit NOT NULL DEFAULT 0,
 PinValidadoId uniqueidentifier NULL REFERENCES dbo.PinesEncomiendaOperativa(Id),
 CONSTRAINT CK_EvidenciaDestinatarioNoReconocimiento CHECK(Tipo<>N'CapturaDestinatario' OR ComparacionRemitenteAprobada=0)
);
CREATE TABLE dbo.FotografiasEncomiendaOperativa(
 EvidenciaId uniqueidentifier NOT NULL PRIMARY KEY REFERENCES dbo.EvidenciasEncomiendaOperativa(Id),
 FotografiaProtegida varbinary(max) NOT NULL, EliminarUtc datetime2(3) NOT NULL,
 EntregaUtc datetime2(3) NULL, IncidenciaReferencia nvarchar(160) NULL,
 MotivoExtension nvarchar(500) NULL, ExtensionHastaUtc datetime2(3) NULL,
 ExtensionAdminId int NULL REFERENCES dbo.Administradores(Id)
);
CREATE INDEX IX_EncomiendaFotoLimpieza ON dbo.FotografiasEncomiendaOperativa(EliminarUtc,ExtensionHastaUtc);
CREATE TABLE dbo.SolicitudesDepositoEncomiendaOperativa(
 Id uniqueidentifier NOT NULL PRIMARY KEY, TerminalId int NOT NULL REFERENCES dbo.Terminales(Id),
 ReservaId int NULL REFERENCES dbo.EncomiendasOperativas(ReservaId),
 EvidenciaId uniqueidentifier NULL REFERENCES dbo.EvidenciasEncomiendaOperativa(Id),
 PinId uniqueidentifier NULL REFERENCES dbo.PinesEncomiendaOperativa(Id),
 CodigoCasillero nvarchar(10) NOT NULL REFERENCES dbo.Casilleros(Codigo),
 Estado nvarchar(20) NOT NULL DEFAULT N'Pendiente' CHECK(Estado IN(N'Pendiente',N'Autorizada',N'Depositada',N'Cancelada')),
 CreadaUtc datetime2(3) NOT NULL, VenceUtc datetime2(3) NOT NULL
);
CREATE TABLE dbo.AuditoriaEncomiendaOperativa(
 Id bigint IDENTITY NOT NULL PRIMARY KEY, ReservaId int NULL REFERENCES dbo.EncomiendasOperativas(ReservaId),
 PersonaId int NULL REFERENCES dbo.Personas(Id), ActorTipo nvarchar(20) NOT NULL CHECK(ActorTipo IN(N'Cliente',N'Administrador',N'Terminal',N'Sistema')),
 ActorId int NOT NULL, Accion nvarchar(60) NOT NULL, Estado nvarchar(30) NOT NULL,
 FechaUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), Detalle nvarchar(500) NOT NULL,
 PinId uniqueidentifier NULL, EvidenciaId uniqueidentifier NULL
);
CREATE INDEX IX_EncomiendaAuditoria ON dbo.AuditoriaEncomiendaOperativa(ReservaId,Id);
EXEC sys.sp_addextendedproperty @name=N'SynchroEncomiendasOperativas',@value=N'1';
COMMIT TRANSACTION;
