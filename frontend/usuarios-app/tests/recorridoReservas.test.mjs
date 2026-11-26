import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const leer = nombre =>
  readFileSync(new URL('../src/views/' + nombre + '.vue', import.meta.url), 'utf8');

test('la reserva distingue la autorizacion del remitente y la del destinatario', () => {
  const reserva = leer('PreReservaView');
  assert.match(reserva, /normalizarTelefono\(telefono\.value\)/);
  assert.match(reserva, /El administrador comprobará tu autorización de WhatsApp y el pago/);
  assert.match(reserva, /Después de confirmar el depósito/);
  assert.match(reserva, /Number\.isSafeInteger\(Number\(d\?\.id\)\)/);
  assert.doesNotMatch(reserva, /No se comparan rostros ni necesitas registrar uno en la web/);
});

test('los contadores reservables cuentan codigos unicos y no todas las filas SQL', () => {
  const reserva = leer('PreReservaView');
  assert.match(reserva, /new Set\(lista\.map\(/);
  assert.match(reserva, /:disponibles="cantidadPorTipo\(t\.id\)"/);
  assert.match(reserva, /:disponibles="cantidadPorTamano\(t\.valor\)"/);
  assert.doesNotMatch(reserva, /:disponibles="opciones\.filter\(o=>o\.tipo===t\.id\)\.length"/);
});

test('al cambiar entre encomienda y pertenencias se consulta nuevamente SQL', () => {
  const reserva = leer('PreReservaView');
  assert.match(reserva, /watch\(servicio,\(\)=>\{revisando\.value=false;void cargar\(true\);\}\)/);
  assert.match(reserva, /const cargaInicial=!discreta/);
  assert.match(reserva, /else actualizando\.value=true/);
  assert.match(reserva, /actual!==versionCatalogo/);
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
});
