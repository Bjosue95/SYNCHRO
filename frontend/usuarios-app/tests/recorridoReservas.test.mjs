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
