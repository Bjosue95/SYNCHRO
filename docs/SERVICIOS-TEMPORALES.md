# Servicios temporales del prototipo

> Corrección del propietario: frío/calor se reservan y pagan solo desde la cuenta web
> de un cliente registrado. La tablet únicamente utiliza o retira reservas web existentes.
> Los ejemplos anteriores de contratación térmica desde terminal quedan sustituidos
> por [esta corrección](CORRECCION-TABLET-Y-RESERVAS-TERMICAS.md).


Módulo aditivo para carga prepagada, resguardo personal y casilleros de frío/calor.
Solo se registra en Development con SynchroDB_Pruebas. No controla hardware real.
Los contratos siempre identifican el hardware como simulado. Las pruebas no demuestran
potencia USB-C PD, funcionamiento de cargadores ni seguridad de conservación de alimentos.

## Preparación y configuración

Aplicar `20261110_ServiciosTemporales_Pruebas.sql` después del inventario base de pruebas.
La migración rechaza cualquier base distinta de SynchroDB_Pruebas y crea tablas idempotentes
para configuración, operaciones, incidencias y auditoría. No borra ni renombra datos.
Reutiliza Casilleros y EspaciosCarga como inventario; no crea un inventario paralelo.
La ocupación se actualiza en esa misma fila, con bloqueos y transacción SQL.

Configurar mediante User Secrets o variables seguras:

- `ServiciosTemporales:Habilitado=true`.
- `ServiciosTemporales:ClaveHmac`: 32 bytes aleatorios en Base64, nunca en Git.
- `ServiciosTemporales:ProcesarVencimientos=true` (predeterminado): revisión cada 30 segundos.

Las guardias se evalúan sobre la configuración final antes de arrancar trabajadores.
La activación en Production o sobre la base principal se rechaza. Sin activación, las
rutas nuevas responden 503 y el comportamiento anterior permanece. Al activarse, los
POST de carga postpago anteriores responden 409: no sirven para eludir el prepago.

Un administrador configura compartimentos y tarifas con:

`PUT /api/admin/servicios/compartimentos/{codigo}`

Ejemplo para un celular (precios ilustrativos, no tarifas comerciales del proyecto):

```json
{
  "codigo": "C01",
  "clase": "CargaCelular",
  "conector": "USB-C",
  "habilitado": true,
  "tarifas": { "30": 10.00, "60": 18.00, "90": 25.00 },
  "tamano": "Mediano"
}
```

La clave de la ruta prevalece sobre codigo del JSON. Se validan decimales y duraciones.
Clases: CargaCelular, CargaLaptop, Frio, Calor, Personal. Los teléfonos admiten USB-C,
Lightning o Inalambrico; las laptops USB-C PD o CargadorPropio. Carga exige las tres tarifas
30/60/90. Frío, calor y resguardo admiten tarifas por duración de 1 a 1440 minutos.
No se inventan ni precargan precios comerciales. El precio se copia a la reserva: cambiar
la tarifa después no altera el contrato ya reservado.

Para Frio/Calor se requieren temperaturaMinima, temperaturaMaxima y temperaturaSimulada;
no llevan conector. Se valida el rango y un valor fuera de él impide nuevos depósitos,
genera una incidencia en la revisión y bloquea el compartimento sin impedir un retiro.
Un casillero existente debe ser Normal para Personal, Frio/Refrigerado para Frio o
Calor/Caliente para Calor. Los nuevos se crean como Normal/Refrigerado/Caliente.
Reconfigurar no cambia de clase/conector un compartimento ya gestionado.

## Contratos web / tablet

`GET /api/servicios/csrf-token`: token y cookie antiforgery del flujo; requiere HTTPS.
Enviar `X-CSRF-TOKEN` y su cookie en cada mutación. Las rutas terminal requieren además
su cookie autenticada; las administrativas requieren Administrador.

| Operación | Ruta |
|---|---|
| Disponibilidad, conectores, temperaturas y tarifas públicas | `GET /api/servicios/compartimentos` |
| Reserva web | `POST /api/web/servicios/{codigo}/reservas` |
| Reserva tablet | `POST /api/terminales/servicios/{codigo}/reservas` |
| Pago web simulado | `POST /api/web/servicios/{codigo}/reservas/{id}/pagar` |
| Pago NFC simulado | `POST /api/terminales/servicios/{codigo}/reservas/{id}/pagar` |
| Depósito / inicio | `POST /api/terminales/servicios/{codigo}/reservas/{id}/iniciar` |
| Consulta privada | `GET /api/servicios/{codigo}/reservas/{id}` |
| Retiro | `POST /api/terminales/servicios/{codigo}/reservas/{id}/retirar` |
| Renovar PIN con credencial de gestión | `POST /api/terminales/servicios/{codigo}/reservas/{id}/pin/renovar` |
| Cancelar antes de ocupar | `POST /api/web/servicios/{codigo}/reservas/{id}/cancelar` o la variante `/api/terminales/servicios/...` |

Reserva: `{"minutos":30}`. Devuelve operación y credencial de gestión una sola vez.
En todas las operaciones posteriores enviar esa credencial en `X-SYNCHRO-SERVICIO`, nunca
por URL. No se guardan teléfonos ni se depende de WhatsApp en el dispositivo que se guarda.
El token de 256 bits se guarda como SHA-256, no en claro.

Pago: `{"idOperacion":"GUID_UNICO_DEL_INTENTO"}`. El monto siempre procede del servidor;
no se toma del JSON. El primer pago entrega el PIN una vez. Repetir el mismo identificador
y canal confirma el resultado sin cobrar nuevamente ni revelar el PIN otra vez. Un intento
distinto sobre un pago confirmado es conflicto. Todos los pagos son simulados, en HNL.

Inicio: `{"equipoCerrado":true}`. Para laptops es una confirmación requerida por el
prototipo, no una lectura real del sensor. El tiempo comienza al depositar/iniciar,
no al reservar. Repetir el inicio no extiende la duración. Se toma la terminal desde
su sesión y se verifica su actividad nuevamente en la transacción.

Retiro: `{"pin":"PIN_RECIBIDO"}`. Requiere credencial de gestión, PIN y la terminal
vinculada. Máximo cinco intentos de PIN, HMAC con salt y propósito por operación.
Renovarlo con la credencial de gestión genera un PIN distinto e invalida el anterior.
El cierre consume el PIN y libera el inventario o lo deja bloqueado si hay una incidencia.
No es una credencial QR y no requiere un teléfono disponible.

## Estados y vencimientos

Reservada -> Pagada -> Activa -> Agotada -> Finalizada.

Reservada/Pagada vencen si no se inicia en diez minutos: se cancelan y liberan inventario;
si hubo pago, se anula el ingreso simulado. Una operación Activa cuyo tiempo termina pasa
a Agotada, conserva el compartimento ocupado y admite retiro sin cobro adicional.
El trabajador y las consultas aplican el mismo motor y la misma transacción. No se
cancelan operaciones ocupadas para asignarlas a otro cliente.

El repositorio SQL bloquea configuración, inventario y operaciones antes de ejecutar el
motor común. Ocupación, precio, PIN, estado e historial de auditoría se confirman juntos;
el índice único impide dos operaciones activas por compartimento. Se mantiene historial
normalizado para reportes y datos operativos JSON versionables, sin PIN/token en claro.
Las pruebas simuladas de repositorio no sustituyen la verificación de estos bloqueos en SQL.

## Administración e incidencias

`POST /api/admin/servicios/compartimentos/{codigo}/incidencias`

Cuerpo: `{"descripcion":"Descripción sin datos personales"}`. Bloquea futuras reservas;
un equipo ya guardado sigue siendo recuperable. Reconfigurar con habilitado=true y
temperatura apta resuelve incidencias del compartimento y deja constancia de auditoría.
No se borran historiales para retirar un componente de servicio.

`GET /api/admin/servicios/reportes?desde=UTC&hasta=UTC`

Períodos de hasta 31 días, hasta no posterior al reloj del servidor; intervalo [desde,hasta).
Incluye reservas creadas, finalizaciones, ingresos netos simulados de pagos del período,
incidencias y minutos de ocupación recortados al intervalo. EnUso es ocupación actual.
No representa ingresos bancarios ni carga/temperatura física verificada.

## OpenAPI y errores

`GET /openapi/v1.json`, exclusivamente administrador y HTTPS en Development + base de pruebas.
Se genera con el paquete oficial Microsoft.AspNetCore.OpenApi 10.0.14 e incluye los endpoints
existentes y nuevos. Las reglas de credenciales/CSRF anteriores complementan el documento.

Errores: 400 datos/formato/CSRF inválidos; 401 sin sesión; 403 rol insuficiente;
404 operación o credencial ajena/no encontrada; 409 estado o disponibilidad incompatible;
429 exceso de intentos; 503 módulo, configuración o SQL no disponibles. No se devuelven
mensajes internos de SQL, claves ni cuerpos del proveedor.

## Verificación

El motor de negocio usado en SQL es el mismo probado con repositorio simulado transaccional.
Hay recorridos HTTP para las cinco clases y permisos, CSRF, credenciales ajenas, prepago,
retiro tras vencimiento, incidencias, OpenAPI y rechazo de activación en producción.
Se añadieron casos externos SQL de prepago/retiro, carrera y reversión por fallo de auditoría;
no tienen Skip y requieren SYNCHRO_SQL_DEPOSITO_TEST. Sus resultados se recogen en ISSUE-1-AVANCE.md.

Falta validar estas migraciones y operaciones sobre SQL Server real en Windows. También
faltan pruebas físicas, homologación eléctrica/térmica, roles granulares/gestión de usuarios
y la instalación inicial completa de la base histórica: esta migración añade el módulo,
no reconstruye todos los procedimientos antiguos ni autoriza modificar SynchroDB.
