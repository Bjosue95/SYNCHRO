# Encomiendas: requisitos corregidos del prototipo

Corrección del propietario e issue #1 del 10/11/2026. Sustituye las instrucciones anteriores
sobre visitantes, altas presenciales y biometría profesional. Rama de trabajo:
`feat/issue-1-encomiendas-end-to-end`; destino de revisión: `refactor/arquitectura-backend`.

## Reglas implementadas

- Solo una cuenta de cliente activa y autenticada puede crear una reserva de encomienda en la web.
  La identidad y el teléfono del remitente salen del servidor. El JSON solo contiene teléfono
  destinatario y tamaño. Los IDs y teléfonos de remitente enviados por el navegador no se usan.
- No existen altas de encomiendas en la terminal. `/api/terminales/encomiendas/visitas`,
  `/reservas` y `/pre-reservas` responden 404 al POST. Los servicios de carga, Personal,
  frío/calor mantienen sus reservas presenciales independientes.
- Las reservas históricas sin cuenta vinculada se conservan, pero no habilitan depósito;
  tampoco lo hacen las creadas desde terminal o las de una cuenta inactiva. Se comprueba
  nuevamente dentro de la transacción SQL, además del control HTTP.
- El consentimiento firmado por WhatsApp sigue siendo obligatorio antes del depósito.
  El retiro exige sesión del destinatario, captura simulada y PIN temporal. El PIN se
  encola cifrado en la misma transacción del depósito y no se entrega a la terminal.

## Recorrido web y terminal

Todas las operaciones usan HTTPS, cookies Secure/HttpOnly y `Cache-Control: no-store`.
Las escrituras exigen la cookie antifalsificación y el encabezado `X-CSRF-TOKEN`.

1. `GET /api/web/clientes/acceso/csrf-token`; `POST /api/web/clientes/acceso/registro`
   con `{"nombreUsuario":"cliente_demo","contrasena":"<12 a 128 caracteres>","telefono":"<E164>"}`.
   También existe `POST /login` con nombre y contraseña. Se usa PasswordHasher del framework,
   se crea Persona + cuenta en una transacción y se emite cookie `SYNCHRO.ClienteWeb`.
2. Con la cookie, obtener `GET /api/web/clientes/csrf-token`. Consultar
   `GET /api/web/clientes/mi-sesion`; salir mediante `POST /api/web/clientes/logout`.
   La cuenta activa y su versión de sesión se vuelven a comprobar en cada solicitud.
3. `POST /api/web/encomiendas/pre-reservas` con
   `{"telefonoDestinatario":"<E164>","tamanoSolicitado":"Mediano"}`.
   Tamaños: Pequeno/Mediano/Grande. Guardar la credencial devuelta una sola vez;
   enviarla en `X-SYNCHRO-RESERVA`, nunca en la URL ni en logs.
4. Iniciar el consentimiento mediante el endpoint existente
   `/api/web/encomiendas/pre-reservas/{id}/consentimiento/iniciar`. Solo el cliente propietario puede iniciarlo.
   La confirmación procede del webhook firmado, no de un booleano del navegador.
5. El destinatario usa la verificación telefónica y sesión `Destinatarios` existentes.
   `POST /api/web/destinatarios/encomiendas/{id}/vincular` toma la persona y teléfono
   verificado de esa sesión y exige coincidencia con el consentimiento de la reserva.
   No crea un registro presencial ni permite cambiar participantes ya vinculados.
6. Cada participante registra su propia referencia de demostración según el apartado siguiente.
7. La terminal vinculada obtiene CSRF de `GET /api/terminales/encomiendas/csrf-token`.
   Captura remitente: `POST /api/terminales/encomiendas/reservas/{id}/evidencias-remitente`.
   Depósito: `POST /api/terminales/encomiendas/reservas/{id}/depositar` con
   `{"evidenciaId":"<GUID>","codigoCasillero":"S01"}`. Ambas requieren la credencial de reserva.
8. Con cookie de terminal y del destinatario: capturar mediante
   `POST /api/terminales/encomiendas/usos/{id}/evidencias-destinatario`, después
   `POST /api/terminales/encomiendas/usos/{id}/retirar` con `{"evidenciaId":"<GUID>","pin":"<6 dígitos>"}`.
   No se pide de nuevo el teléfono guardado en la reserva.

## Capturas y comparación simulada

No hay requisito de proveedor externo, biometría profesional ni prueba de vida física.
El adaptador predeterminado en Development + SynchroDB_Pruebas es
`VerificadorFacialSimulado`. Fuera del entorno de demostración se utiliza el adaptador
no disponible; nunca se presenta la simulación como autenticación facial segura.

Cada persona autenticada puede registrar/eliminar solamente su propia referencia:

| Sesión | POST / DELETE |
|---|---|
| Cliente remitente | `/api/web/clientes/rostro-simulado/referencia` |
| Destinatario verificado | `/api/web/destinatarios/rostro-simulado/referencia` |

POST exige además `X-SYNCHRO-CONSENTIMIENTO: ACEPTO` y CSRF correspondiente a esa sesión
(para destinatario: `/api/destinatarios/csrf-token`). Enviar bytes PNG como
`application/octet-stream`, máximo 1 MiB, 32..1024 píxeles por dimensión, RGB/RGBA/gris,
8 bits, sin entrelazado. En web puede usarse un fotograma de cámara dibujado en canvas
más `toBlob(..., 'image/png')`. El backend rechaza datos corruptos, imágenes uniformes,
formatos incompatibles y cargas demasiado grandes.

El comparador reduce luminancia a 32x32, normaliza y compara distancia visual. Cada
captura se contrasta con la referencia de SU persona: remitente al depositar,
destinatario al retirar. No detecta caras, no garantiza identidad física y puede aceptar
imágenes parecidas o rechazar cambios de iluminación/pose. Las respuestas lo marcan
`simulado=true`. Los campos SQL heredados `PruebaDeVidaSuperada` significan en esta
modalidad únicamente captura procesada en la simulación.

Se descartan/sobrescriben imágenes y buffers al terminar la solicitud. Solo se persiste
el patrón reducido cifrado con Data Protection y ligado a PersonaId/modelo/vencimiento.
No se guardan archivos de cámara. DELETE elimina la referencia; el worker limpia las
vencidas cada cinco minutos y una referencia vencida se rechaza inmediatamente.
Conservar las claves Data Protection entre reinicios mediante la configuración del host.

Configuración opcional: `RostroSimulado:Habilitado` (true en demostración),
`RostroSimulado:VigenciaHoras` (24; permitido 1..168),
`RostroSimulado:UmbralDistancia` (0.25; permitido 0..1),
`RostroSimulado:LimpiarVencidas` (true). No habilitar fuera de Development/Pruebas.

## Administración de usuarios

`Administracion:UsuariosHabilitados=true` habilita `/api/admin/usuarios`, exclusivamente
Development + SynchroDB_Pruebas. Un administrador puede listar/crear usuarios, actualizar
estado/roles (`PUT /{id}`) y cambiar contraseña (`POST /{id}/password`). Requiere HTTPS,
CSRF y rate limiting. No se devuelven hashes ni contraseñas. AdminSetup conserva el alta
inicial. Los cambios de roles/estado/contraseña revocan la versión de sesión anterior.

Roles permitidos: Administrador (usuarios/configuración/operación/reportes), Operador
(incidencias operativas) y Auditor (reportes). Los endpoints anteriores de gestión global
siguen reservados a Administrador. Se impide desactivar o quitar el rol al último
administrador activo. Las cuentas previas sin marcador conservan su rol histórico;
una cuenta gestionada sin roles NO se convierte por defecto en administrador.

## Migraciones y verificación pendiente

Aplicar solo en SynchroDB_Pruebas con parada al primer error. ClientesWeb va después de
OrigenPreReserva y antes de DepositoEncomiendaAtomico. RostroSimulado requiere Personas.
UsuariosRoles requiere/crea Administradores. Las migraciones no convierten visitantes
en clientes ni borran historial. Nunca aplicar el esquema de fixture a una base histórica.

Los tests HTTP usan cookies y políticas reales con repositorios de prueba sustituidos;
los tests visuales ejecutan el comparador real de demostración con PNG sintéticos.
Los tests SQL de transacciones/rollback/concurrencia requieren
`SYNCHRO_SQL_DEPOSITO_TEST` y SQL Server de la laptop. No se ejecutó hardware ni entrega
real de WhatsApp. Ver resultados consolidados en ISSUE-1-AVANCE.md.
