import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const leer = nombre =>
  readFileSync(new URL('../src/views/' + nombre + '.vue', import.meta.url), 'utf8');

test('la reserva distingue la autorizacion del remitente y la del destinatario', () => {
  const reserva = leer('PreReservaView');
  assert.match(reserva, /normalizarTelefono\(telefono\.value\)/);
  assert.match(reserva, /La autorización de retiro del destinatario se gestiona después de confirmar el depósito/);
  assert.match(reserva, /La solicitud quedará pendiente de confirmación administrativa/);
  assert.match(reserva, /Number\.isSafeInteger\(Number\(d\?\.id\)\)/);
  assert.doesNotMatch(reserva, /No se comparan rostros ni necesitas registrar uno en la web/);
});

test('los contadores reservables cuentan codigos unicos y no todas las filas SQL', () => {
  const reserva = leer('PreReservaView');
  assert.match(reserva, /new Set\(lista\.map\(/);
  assert.match(reserva, /:disponibles="servicio===\x27Personal\x27 && t\.id!==\x27Normal\x27 \? undefined : cantidadPorTipo\(t\.id\)"/);
  assert.match(reserva, /:disponibles="cantidadPorTamano\(t\.valor\)"/);
  assert.doesNotMatch(reserva, /:disponibles="opciones\.filter\(o=>o\.tipo===t\.id\)\.length"/);
});

test('al cambiar entre encomienda y pertenencias se consulta nuevamente SQL', () => {
  const reserva = leer('PreReservaView');
  assert.match(reserva, /watch\(servicio,\(nuevo\)=>\{/);
  assert.match(reserva, /void cargar\(true\)/);
  assert.match(reserva, /const cargaInicial=!discreta/);
  assert.match(reserva, /else actualizando\.value=true/);
  assert.match(reserva, /actual!==versionCatalogo/);
});

test('pertenencias personales solo habilita Normal y encomiendas permite termicos', () => {
  const reserva = leer('PreReservaView');
  assert.match(reserva, /const tipoPermitido=computed\(\(\)=>servicio\.value==='Encomienda'\|\|tipo\.value==='Normal'\)/);
  assert.match(reserva, /if\(nuevo==='Personal' && tipo\.value!=='Normal'\)tipo\.value='Normal'/);
  assert.match(reserva, /:disabled="servicio==='Personal' && t\.id!=='Normal'"/);
  assert.match(reserva, /Exclusivo para encomiendas/);
  assert.match(reserva, /const disponible=computed\(\(\)=>tipoPermitido\.value/);
});

test('las tarjetas tienen dimensiones fijas y muestran los iconos reales de Tablet', () => {
  const reserva = leer('PreReservaView');
  const tarjeta = readFileSync(new URL('../src/components/ui/SeleccionReservaCard.vue', import.meta.url), 'utf8');
  const icono = readFileSync(new URL('../src/components/ui/IconoTablet.vue', import.meta.url), 'utf8');
  assert.match(reserva, /icono-tablet="guardar"/);
  assert.match(reserva, /icono-tablet="encomienda"/);
  assert.match(reserva, /:icono-tablet="t\.id"/);
  assert.match(reserva, /:icono-tablet="t\.valor"/);
  assert.match(tarjeta, /height: 260px;/);
  assert.match(tarjeta, /grid-template-rows: 56px 54px 50px 20px 20px;/);
  assert.match(tarjeta, /seleccionada::after/);
  assert.match(icono, /32_guardar_pertenencias_personales/);
  assert.match(icono, /09_encomienda_paquete/);
  assert.match(icono, /casillero-tamano-S/);
});

test('detalle de reserva usa opciones de ubicación y duración, teléfono y navegación visual', () => {
  const vista = leer('PreReservaView');
  const iconos = readFileSync(new URL('../src/components/ui/IconoTablet.vue', import.meta.url), 'utf8');
  assert.match(vista, /v-for="\[id,nombre\] in ubicaciones"/);
  assert.match(vista, /v-for="m in duraciones"/);
  assert.match(vista, /:aria-pressed="minutos===m"/);
  assert.match(vista, /:aria-pressed="ubicacion===id"/);
  assert.match(vista, /type="tel"/);
  assert.match(vista, /nombreUbicacion\(nombre\)/);
  assert.match(vista, /Dirección pendiente de confirmación/);
  assert.match(vista, /@submit\.prevent="revisar"/);
  assert.match(vista, /\(paso===4 && !disponible\)/);
  assert.doesNotMatch(vista, /@click="volverATamanos"/);
  assert.match(vista, /'Revisa tu reserva' : 'Selecciona cómo pagar'/);
  assert.doesNotMatch(vista, /<select v-model="minutos"/);
  assert.doesNotMatch(vista, /<select v-model="ubicacion"/);
  for(const nombre of ['ubicacion','tiempo','whatsapp','volver','continuar']) {
    assert.match(iconos, new RegExp(nombre + ':'));
  }
});

test('iconos de Tablet respetan tamaño en reserva y pago', () => {
  const icono = readFileSync(new URL('../src/components/ui/IconoTablet.vue', import.meta.url), 'utf8');
  const vista = leer('PreReservaView');
  assert.ok(icono.includes(':style="{ width: tamano +'));
  assert.ok(icono.includes("height: tamano + 'px'"));
  assert.doesNotMatch(icono, /^\s*width:\s*100%;/m);
  assert.doesNotMatch(icono, /^\s*height:\s*100%;/m);
  assert.doesNotMatch(vista, /<IconoTablet nombre="volver" :tamano="21"/);
  assert.match(vista, /<IconoTablet nombre="pago" :tamano="32"/);
  assert.match(vista, /<IconoTablet nombre="continuar" :tamano="21"/);
  assert.ok(vista.includes('.reserva-final__acciones .boton {'));
});

test('el botón revisar reserva no crece hasta 280px de alto en móviles', () => {
  const vista = leer('PreReservaView');
  const reglasMovil = vista.slice(vista.indexOf('@media (max-width: 470px)'));
  assert.match(reglasMovil, /\.reserva-final__acciones \{ flex-direction: column-reverse; \}/);
  assert.match(reglasMovil, /\.reserva-final__acciones \.boton \{[^}]*flex: 0 0 auto;/);
  assert.match(reglasMovil, /\.reserva-final__acciones \.boton \{[^}]*min-height: 54px;/);
  assert.match(reglasMovil, /\.reserva-final__acciones \.boton \{[^}]*height: auto;/);
});

test('la revisión distingue el pagador y nunca transmite datos de tarjeta', () => {
  const vista=leer('PreReservaView');
  const api=vista.slice(vista.indexOf('encomiendasApi.reservar({'),
    vista.indexOf('});',vista.indexOf('encomiendasApi.reservar({')));
  assert.match(vista, /pagador==='Remitente'/);
  assert.match(vista, /pagador==='Destinatario'/);
  assert.match(vista, /4242 4242 4242 4242/);
  assert.match(vista, /12\/30/);
  assert.match(vista, /Usa 123/);
  assert.match(vista, /limpiarTarjetaPrueba\(\)/);
  assert.match(api, /pagador:/);
  assert.match(api, /modalidadPago:/);
  assert.doesNotMatch(api, /numeroTarjetaPrueba|venceTarjetaPrueba|codigoTarjetaPrueba/);
  assert.doesNotMatch(vista, /localStorage|sessionStorage/);
  assert.doesNotMatch(vista, /Volver a tamaños/);
  assert.match(vista, /El pago presencial se realizará mediante el proceso habitual de la tablet/);
  assert.match(vista, /\? 'Confirmar pago' : 'Confirmar reserva'/);
  assert.match(vista, /modalidadPago:pagador\.value==='Remitente'\?'WebTarjeta'/);
});

test('el historial utiliza solo datos reales y descarta respuestas antiguas', () => {
  const panel = leer('PanelView');
  assert.match(panel, /encomiendasApi\.listar\(/);
  assert.match(panel, /items\.value=datos\.encomiendas/);
  assert.match(panel, /actual!==numeroConsulta/);
  assert.match(panel, /fecha inicial debe ser anterior o igual a la fecha final/);
  assert.match(panel, /v-if="totalPaginas>1"/);
});

test('el detalle no ofrece la autorizacion del destinatario antes del deposito', () => {
  const detalle = leer('EncomiendaDetalleView');
  assert.match(detalle, /const depositoConfirmado = computed/);
  assert.match(detalle, /rol === 'remitente' \|\| depositoConfirmado/);
  assert.match(detalle, /rol === 'destinatario' && depositoConfirmado/);
  assert.match(detalle, /detalle\.condiciones\.remitenteAutorizado/);
  assert.match(detalle, /\["Entregado","Consumido"\]\.includes\(p\.estado\)/);
  assert.match(detalle, /watch\(\(\) => route\.params\.id/);
  assert.match(detalle, /Tu solicitud de pago fue enviada a revisión/);
  assert.match(detalle, /No se ha efectuado ningún cargo bancario/);
});

test('el pago web se ofrece para pertenencias y encomiendas sin enviar datos bancarios', () => {
  const vista=leer('PreReservaView');
  assert.match(vista, /<div v-if="paso===7" class="reserva-pago__seccion">\s*<div class="reserva-final__intro">\s*<h3>¿Cómo deseas realizar el pago\?/);
  assert.match(vista, /pagador:pagador\.value==='Remitente'\?'Remitente'/);
  assert.match(vista, /modalidadPago:pagador\.value==='Remitente'\?'WebTarjeta'/);
  assert.match(vista, /@click="usarDatosPrueba"/);
  assert.match(vista, /v-if="pagoError"/);
  assert.doesNotMatch(vista, /servicio==='Encomienda'&&pagador==='Remitente'&&!tarjetaPruebaValida/);
});

test('el pago muestra instrucciones y permite confirmar para ver errores', () => {
  const vista=leer('PreReservaView');
  assert.match(vista, /function usarDatosPrueba\(\)/);
  assert.match(vista, /@click="usarDatosPrueba"/);
  assert.match(vista, /<SynButton v-else tipo="button" :cargando="enviando" :disabled="enviando" @click="crear"/);
  assert.match(vista, /pagoError\.value='Los valores no coinciden/);
});

test('el resumen y los cobros usan tarifas del catálogo sin inventar precios', () => {
  const vista=leer('PreReservaView');
  assert.match(vista, /const importeReserva=computed/);
  assert.match(vista, /opcion\.tarifas\?\.\[minutos\.value\]/);
  assert.match(vista, /Total de la reserva/);
  assert.match(vista, /importeTexto/);
  assert.match(vista, /Importe por definir/);
});

test('reserva en pasos adaptables conserva selecciones y espera aprobacion del pago', () => {
  const vista=leer('PreReservaView');
  assert.match(vista, /const pasosReserva=\['Servicio','Tipo','Tamaño','Estación','Duración','Destinatario','Resumen','Pago'\]/);
  assert.match(vista, /v-if="paso===0"/);
  assert.match(vista, /v-if="paso===1"/);
  assert.match(vista, /v-if="paso===2 && porTipo.length"/);
  assert.match(vista, /v-if="paso===6 \|\| paso===7"/);
  assert.match(vista, /v-if="paso===8 && reservaEnEspera!==null"/);
  assert.match(vista, /intervaloPago=setInterval/);
  assert.match(vista, /d\.pago\?\.estado==='ConfirmadoManual'/);
  assert.match(vista, /await router\.replace\('\/reservas\/'\+id\)/);
});
test('PIN destinatario permanece condicionado al deposito confirmado', () => {
  const detalle=leer('EncomiendaDetalleView');
  assert.match(detalle, /Depósito confirmado\. Ya puedes compartir la invitación de WhatsApp/);
  assert.match(detalle, /permanecen deshabilitados hasta que la tablet confirme el depósito/);
  assert.match(detalle, /pinRetiroEntregado/);
});

test('tarifa visible ocupa todo el ancho del resumen en pantallas pequeñas', () => {
  const vista=leer('PreReservaView');
  assert.match(vista, /\.reserva-revision__importe \{grid-column:1\/-1;/);
  assert.match(vista, /\.reserva-revision__datos \.reserva-revision__importe dd \{font-size:clamp/);
});

test('una sola seccion por paso y tarjetas sin piramide movil', () => {
  const vista=leer('PreReservaView');
  assert.match(vista, /v-if="paso===1" class="env-card"/);
  assert.match(vista, /v-if="paso===2 && porTipo\.length"/);
  assert.match(vista, /v-if="paso===3" class="reserva-bloque"/);
  assert.match(vista, /v-if="paso===4" class="reserva-bloque"/);
  assert.match(vista, /saltoSiguiente=\(actual:number\)=>actual===4&&servicio\.value==='Personal'\?6:actual\+1/);
  assert.match(vista, /\.reserva-vista \.reserva-opciones\.reserva-tres \{grid-template-columns:repeat\(3,minmax\(0,1fr\)\);\}/);
  assert.doesNotMatch(vista, /:last-child:nth-child\(odd\)/);
  assert.match(vista, /overflow-y:auto;overscroll-behavior:contain/);
  assert.match(vista, /<footer v-if="!cargando && paso<=ultimoPaso"/);
});
