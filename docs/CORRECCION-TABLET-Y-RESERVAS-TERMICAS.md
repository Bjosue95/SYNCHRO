# Correcciones del propietario: menú, iconos y reservas térmicas

El menú conserva exactamente cuatro accesos: Guardar, Retirar, Tengo una reserva web
y Cargar dispositivo, con distribución 2×2 en tablet. Guardar vuelve a su pantalla original.
Cargar usa `04_carga_inalambrica.svg` y `04_carga_inalambrica-oscuro.svg`; se reutilizan
los iconos complementarios existentes para las acciones en vez de sustituirlos por símbolos.

Frío y calor no se contratan en la terminal del casillero. El cliente registrado crea y
paga la reserva en su web. En la tablet, Tengo una reserva web permite consultar la
reserva térmica existente y empezar su uso una vez comprobado el pago. No hay selección
de nuevas tarifas, alta de reserva o pago térmico en la tablet. Las antiguas URL de
contratación térmica redirigen a reservas existentes. El retiro usa la reserva y su PIN.

## Controles del backend

- POST `/api/terminales/servicios/{codigo}/reservas` y su `/pagar` rechazan Frio/Calor (403).
  La clase se obtiene del compartimento guardado, no del JSON ni de un prefijo de código.
- POST `/api/web/servicios/{codigo}/reservas` exige cookie Clientes válida para servicios
  térmicos. La identidad se autentica en el servidor antes de comprobar CSRF. Se ofrece
  GET `/api/web/servicios/csrf-token` para obtener un token ligado a esa sesión.
- Reserva y pago web térmicos conservan ClientePersonaId y OrigenReserva=Web en DatosJson,
  dentro de la transacción existente. SQL comprueba que la cuenta siga activa al gestionar
  la reserva. No se aceptan identidades/orígenes del cuerpo. No se necesita nuevo DDL.
- Pago/cancelación térmicos exigen el cliente propietario además de su credencial; otro
  cliente con la misma credencial recibe 404. Iniciar en terminal exige reserva web con
  cliente guardado y estado pagado. No se abre solo por presentar un ID.
- Los registros térmicos históricos sin cliente/origen se conservan; no se inventan cuentas
  para ellos ni se permite iniciarlos como reservas web nuevas. El retiro de operaciones
  ya activas conserva sus controles existentes de PIN/credencial para recuperar objetos.
- Carga y Personal mantienen sus modalidades anteriores. No se cambia main ni demos/.

## Comprobación

Interfaz: compilación aprobada, 2 pruebas de fixtures y 18 de navegador aprobadas.
Las pruebas verifican cuatro accesos exactos, icono original, rutas retiradas, uso de
reserva térmica existente y ausencia de contratación/pago térmico en la tablet.
La demostración tampoco acepta crear reservas térmicas desde la terminal.

Backend: suite completa de 300 casos, 252 aprobados, 44 fallidos, 4 omisiones anteriores.
Los fallos son 25 LocalDB no soportado, 5 secrets.json ausente, 2 HTTP500 derivados de
LocalDB y 12 casos externos sin conexión SQL aislada. No se añaden omisiones. El caso
SQL nuevo cubre cuenta inactiva, origen/propietario persistido y uso de la reserva tras
recrear la instancia del servicio; no se afirma que haya aprobado en Cloud.

Las pruebas HTTP usan cookies de Cliente y repositorios aislados de prueba. Los resultados
no demuestran SQL, WhatsApp ni hardware físico. No se declara terminado todo el backend.
