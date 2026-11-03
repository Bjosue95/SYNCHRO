# SYNCHRO - Estado del desarrollo
Fecha: 2 de noviembre de 2026

## ETAPA ACTUAL
Desarrollo del backend y API con C# .NET 10.
Base de datos: SQL Server LocalDB, SynchroDB.

La interfaz web y de tablet se desarrollara posteriormente.
No modificar el backend existente sin revisar sus funciones.

## FUNCIONALIDADES PROBADAS
- Consulta y reserva de casilleros.
- Deposito y retiro basicos de encomiendas.
- Consulta y reserva de espacios de carga.
- Inicio y finalizacion de sesiones de carga.
- Calculo de tarifas y generacion de cobros.
- Autenticacion administrativa.
- Proteccion HTTPS, cookies y CSRF.
- Confirmacion de pagos NFC simulados.
- Confirmacion de pagos web simulados.
- Prevencion de confirmaciones duplicadas.
- Registro de intentos de pago rechazados.
- Historial de intentos aprobados y rechazados.

## RESULTADOS DE PRUEBA
Pago 1: Pagado, NFC, Tablet, simulado.
Pago 2: Pagado, TarjetaWeb, Web, simulado.
Pago 3: Pagado, NFC, Tablet, simulado.
Pago 3 tuvo dos intentos rechazados y uno aprobado.

## DECISIONES DEL NEGOCIO
- SYNCHRO no aceptara efectivo.
- Todos los pagos del prototipo seran simulados.
- NFC simulado en la tablet.
- Pago digital simulado en la web.
- Los pagos simulados no representan dinero recibido.

## CAMBIO IMPORTANTE: CARGA DE DISPOSITIVOS
Los celulares y laptops guardados en casilleros
utilizaran un modelo de tiempo prepagado.

Flujo propuesto:
1. Seleccionar espacio de carga.
2. Elegir duracion: 30, 60 o 90 minutos.
3. Pagar antes de guardar el dispositivo.
4. Autorizar el uso del casillero.
5. Guardar e iniciar carga.
6. Retirar al finalizar.

El cliente debe poder recuperar su dispositivo
aunque termine el tiempo contratado.

No depender de WhatsApp en el celular guardado
para obtener el PIN de retiro.

IMPORTANTE:
El backend todavia calcula el cobro al finalizar.
Debemos adaptar esa logica al modelo prepagado.

## PENDIENTE
- Adaptar la carga al pago anticipado.
- Completar la API de encomiendas.
- Completar la API de resguardo personal.
- Completar verificaciones y consentimiento WhatsApp.
- Completar administracion y reportes.
- Realizar pruebas integrales.
- Desarrollar interfaces UX/UI.

## PROXIMO PASO
Retomar la API de encomiendas, revisando las tablas
Casilleros, UsosCasillero, Personas y ParticipantesUso.

Antes de terminar el backend, implementar tambien
el nuevo flujo prepagado de carga.

## NOTA
No se ha integrado ninguna pasarela bancaria real.
No utilizar endpoints de demostracion en produccion.
