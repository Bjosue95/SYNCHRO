# SYNCHRO

El punto donde todo se conecta.

## Descripción

Sistema de casilleros inteligentes para el
depósito y retiro de encomiendas y el
resguardo temporal y carga de dispositivos.

## Componentes del sistema

- Página web para clientes.
- Sistema de reservas.
- Panel de administración.
- Aplicación para tablet.
- Backend en C# y ASP.NET Core.
- Base de datos SQL Server.
- Integración con WhatsApp Business.
- Prototipo de reconocimiento facial.

## Estado del proyecto

En desarrollo.

## Desarrollo del backend (issue #1)

- [Avance comprobado y resultados de pruebas](docs/ISSUE-1-AVANCE.md).
- [API de servicios temporales, prepago y configuración](docs/SERVICIOS-TEMPORALES.md).
- OpenAPI generado: `/openapi/v1.json`, solo administrador, HTTPS y entorno de pruebas.

El hardware y los pagos del prototipo son simulados; las integraciones reales y la
validación SQL pendientes están identificadas en el informe. No ejecutar migraciones
de prueba contra la base principal `SynchroDB`.
