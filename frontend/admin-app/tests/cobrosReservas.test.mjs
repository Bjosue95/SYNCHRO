import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const vista = readFileSync(new URL("../src/views/CobrosView.vue", import.meta.url), "utf8");

test("cobros pendientes mantiene los cobros anteriores y añade reservas", () => {
  assert.match(vista, /whatsapp-manual\/cobros\/retiro\/pendientes/);
  assert.match(vista, /api\/admin\/carga\/cobros\/pendientes/);
  assert.match(vista, /api\/admin\/encomiendas\/cobros\/pendientes/);
  assert.match(vista, /Reservas web y encomiendas/);
  assert.match(vista, /item\.origen/);
  assert.match(vista, /item\.pagador/);
});

test("confirmación de reservas requiere CSRF, revisión del monto y autorización explícita", () => {
  assert.match(vista, /api\/admin\/csrf-token/);
  assert.match(vista, /api\/admin\/encomiendas\/cobros\//);
  assert.match(vista, /comprobadosReservas\.value\[id\]!==true/);
  assert.match(vista, /item\.importeHnl!=null && monto!==Number\(item\.importeHnl\)/);
  assert.match(vista, /justificacionesReservas\.value\[id\]/);
  assert.match(vista, /pago de prueba/);
  assert.doesNotMatch(vista, /numeroTarjeta|codigoSeguridad|cvv/i);
});

test("el administrador puede entregar PIN de depósito diferido sin marcar pago confirmado", () => {
  const vista=readFileSync(new URL("../src/views/EncomiendasView.vue", import.meta.url), "utf8");
  assert.match(vista, /const pagoDiferido = computed/);
  assert.match(vista, /const permitePin = computed\(\(\) => pagado\.value \|\| pagoDiferido\.value\)/);
  assert.match(vista, /!permitePin/);
  assert.match(vista, /v-if="enPreparacion && !pagado && !pagoDemoReserva"/);
  assert.match(vista, /El PIN de depósito se entrega antes del pago/);
});

test("la bandeja WhatsApp presenta solicitudes PIN de deposito web y pertenencias personales",()=>{
  const wa=readFileSync(new URL("../src/views/WhatsAppView.vue",import.meta.url),"utf8");
  const backend=readFileSync(new URL("../../../backend/Synchro.Api/Services/Encomiendas/BandejaWhatsAppPin.cs",import.meta.url),"utf8");
  const detalle=readFileSync(new URL("../../../backend/Synchro.Api/Services/Encomiendas/EncomiendasOperativasSql.cs",import.meta.url),"utf8");
  assert.match(wa,/x\.claseReserva === 'Encomienda' \|\| \(tipo === 'remitente' && x\.claseReserva === 'Personal'\)/);
  assert.match(wa,/x\.rol === 'Titular'/);
  assert.match(wa,/solicitudPinDeposito===true/);
  assert.match(wa,/PIN de depósito solicitados desde la web/);
  assert.match(wa,/atenderPinSolicitado\(item\.encomiendaId\)/);
  assert.match(wa,/categoria\.value='remitente'/);
  assert.match(wa,/PanelEnvioPin/);
  const panel=readFileSync(new URL("../src/components/PanelEnvioPin.vue",import.meta.url),"utf8");
  assert.match(panel,/Ya envié el PIN por WhatsApp/);
  assert.match(backend,/a\.Accion=N'PinDepositoSolicitado'/);
  assert.match(backend,/solicitudPinDeposito=r\.GetBoolean\(22\)/);
  assert.match(backend,/SolicitudPinDeposito,PagoDiferidoDestinatario\) AS/);
  assert.match(detalle, /!r\.IsDBNull\(6\)\?"Entregado"/);
});
