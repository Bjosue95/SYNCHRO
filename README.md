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


## Aplicaciones web independientes

| Aplicación | Carpeta | Desarrollo local |
|---|---|---|
| Terminal/tablet | `frontend/tablet-app` | HTTPS :5173 |
| Administración | `frontend/admin-app` | HTTPS :5174 |
| Usuarios y pre-reservas | `frontend/usuarios-app` | HTTPS :5175 |

El portal de usuarios dispone de registro, acceso, consulta de sesión y
pre-reserva web de encomiendas vinculada al backend de clientes. Una pre-reserva
no asigna automáticamente casillero ni habilita el depósito. Las reservas de
pertenencias y módulos térmicos requieren contratos web adicionales.

Guía de instalación y comandos:
[frontend/usuarios-app/README.md](frontend/usuarios-app/README.md).

## Estado del proyecto

En desarrollo.

## Desarrollo del backend (issue #1)

- [Interfaz de tablet y capturas](docs/INTERFAZ-TABLET.md).
- [Avance comprobado y resultados de pruebas](docs/ISSUE-1-AVANCE.md).
- [Encomiendas de clientes y capturas de rostro simuladas](docs/ENCOMIENDAS-CLIENTES-Y-ROSTRO-SIMULADO.md).
- [Instalación de la base aislada](docs/INSTALACION-PROTOTIPO-AISLADO.md).
- [API de servicios temporales, prepago y configuración](docs/SERVICIOS-TEMPORALES.md).
- OpenAPI generado: `/openapi/v1.json`, solo administrador, HTTPS y entorno de pruebas.

Las encomiendas se reservan solo desde cuentas web registradas; la terminal deposita y
retira reservas existentes. El rostro, hardware y pagos del prototipo son simulados; las integraciones reales y la
validación SQL pendientes están identificadas en el informe. No ejecutar migraciones
de prueba contra la base principal `SynchroDB`.
