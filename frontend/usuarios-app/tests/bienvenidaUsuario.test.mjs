import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const leer=(ruta)=>readFileSync(new URL('../'+ruta,import.meta.url),'utf8');

test('al ingresar se abre la bienvenida, excepto cuando existe un destino protegido',()=>{
  const acceso=leer('src/views/AccesoView.vue');
  const rutas=leer('src/router/index.js');
  assert.match(acceso,/\? destino : "\/bienvenida"/);
  assert.match(acceso,/route\.query\.volver/);
  assert.match(acceso,/await router\.replace\(destinoSeguro\(\)\)/);
  assert.match(rutas,/path: "\/bienvenida", name: "bienvenida"/);
  assert.match(rutas,/meta: \{ privada: true, titulo: "Mi espacio" \}/);
  assert.match(rutas,/BienvenidaView\.vue/);
});

test('bienvenida explica servicios y permite reservar o consultar sin inventar operaciones',()=>{
  const vista=leer('src/views/BienvenidaView.vue');
  const shell=leer('src/components/layout/AppShell.vue');
  assert.match(vista,/Bienvenido/);
  assert.match(vista,/to="\/reservar"/);
  assert.match(vista,/to="\/mis-reservas"/);
  assert.match(vista,/query:\{servicio:'Personal'\}/);
  assert.match(vista,/query:\{servicio:'Encomienda'\}/);
  assert.match(vista,/Guardar pertenencias/);
  assert.match(vista,/Enviar una encomienda/);
  assert.match(vista,/Consultar tus reservas/);
  assert.match(shell,/ruta: "\/bienvenida", nombre: "Inicio"/);
  assert.match(shell,/to="\/bienvenida" class="enlace-marca"/);
  assert.doesNotMatch(vista,/Math\.random|reservas simuladas|total de reservas:/);
});

test('la bienvenida consulta reservas reales y ofrece siguiente paso de PIN solo tras pago confirmado',()=>{
  const vista=leer('src/views/BienvenidaView.vue');
  const detalle=leer('src/views/EncomiendaDetalleView.vue');
  const api=leer('src/services/encomiendasApi.js');
  const espera=leer('src/views/PagoPendienteView.vue');
  assert.match(vista,/encomiendasApi\.listar\(\{pagina:1,tamanoPagina:20\}\)/);
  assert.match(vista,/e\.pagoEstado==='ConfirmadoManual'/);
  assert.match(vista,/encomiendasApi\.detalle\(reserva\.id\)/);
  assert.match(vista,/reservaPendiente\.value=elegida/);
  assert.match(vista,/!whatsappConfirmado\.value/);
  assert.match(vista,/Solicitud de PIN registrada/);
  assert.match(vista,/hash:'#solicitar-pin-deposito'/);
  assert.match(vista,/Solicitar PIN de depósito/);
  assert.match(detalle,/async function solicitarPinDeposito\(\)/);
  assert.match(detalle,/await encomiendasApi\.solicitarPinDeposito\(detalle\.value\.id\)/);
  assert.match(detalle,/const puedeSolicitarPin=computed/);
  assert.match(detalle,/solicitudPinRegistrada/);
  assert.match(detalle,/pinDepositoEmitido/);
  assert.match(api,/pin-deposito\/solicitar/);
  assert.match(api,/metodo: "POST"/);
  assert.match(espera,/await router\.replace\('\/bienvenida'\)/);
});
