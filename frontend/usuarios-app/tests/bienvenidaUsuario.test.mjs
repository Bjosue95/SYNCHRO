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

test('las reservas se gestionan en Mis reservas y no en Bienvenida',()=>{
  const bienvenida=leer('src/views/BienvenidaView.vue');
  const panel=leer('src/views/PanelView.vue');
  const detalle=leer('src/views/EncomiendaDetalleView.vue');
  const api=leer('src/services/encomiendasApi.js');
  assert.doesNotMatch(bienvenida,/reservaPendiente|cancelarReserva|siguienteAccion|encomiendasApi\.listar/);
  assert.match(bienvenida,/to="\/mis-reservas"/);
  assert.match(panel,/encomiendasApi\.listar/);
  assert.match(panel,/encomiendasApi\.detalle\(e\.id\)/);
  assert.match(panel,/Reserva #\{\{ e\.id \}\}/);
  assert.match(panel,/estadoEtapa\(detalleDe\(e\)\)/);
  assert.match(panel,/return '\/reservas\/'\+d\.id\+'\/solicitar-pin'/);
  assert.match(detalle,/class="env-card reserva-ficha"/);
  assert.match(api,/pin-deposito\/solicitar/);
});
test('solicitar PIN solo registra evento en API con cuenta propia y dos autorizaciones',()=>{
  const endpoint=leer('../../backend/Synchro.Api/Endpoints/EncomiendasOperativasEndpoints.cs');
  const servicio=leer('../../backend/Synchro.Api/Services/Encomiendas/EncomiendasOperativasSolicitudPin.cs');
  const seguimiento=leer('../../backend/Synchro.Api/Services/Encomiendas/EncomiendasOperativasHistorial.cs');
  const admin=leer('src/../../admin-app/src/views/EncomiendasView.vue');
  assert.match(endpoint,/reservas\.MapPost\("\/\{id:int\}\/pin-deposito\/solicitar"/);
  assert.match(endpoint,/s\.SolicitarPinDeposito\(id,Identidad\(h\),h\.RequestAborted\)/);
  assert.match(servicio,/Leer\(c,tx,id,persona,ct\)/);
  assert.match(servicio,/o\.Pago=="ConfirmadoManual"/);
  assert.match(servicio,/cuenta\.Activa/);
  assert.match(servicio,/Consentimientos\(c,tx,id,ct\)/);
  assert.match(servicio,/PinDepositoSolicitado/);
  assert.match(servicio,/!existente/);
  assert.doesNotMatch(servicio,/EmitirPin\(/);
  assert.match(seguimiento,/N'PinDepositoSolicitado'/);
  assert.match(admin,/PIN de depósito solicitado por el titular/);
});

test('Mis reservas muestra etapa real y protege la cancelacion antes del deposito',()=>{
 const panel=leer('src/views/PanelView.vue');
 const detalle=leer('src/views/EncomiendaDetalleView.vue');
 assert.match(panel,/if\(depositada\(d\)\)\{/);
 assert.match(panel,/if\(esPersonal\(d\)\)return 'Pertenencias resguardadas'/);
 assert.match(panel,/if\(pinEntregado\(d,'Retiro'\)\)return 'Esperando retiro'/);
 assert.match(panel,/if\(pinEntregado\(d,'Deposito'\)\)return 'Esperando depósito'/);
 assert.match(panel,/return 'Autorizar destinatario'/);
 assert.match(panel,/!depositada\(e\)&&!e\?\.cancelacion/);
 assert.match(panel,/await encomiendasApi\.cancelar\(id,\{/);
 assert.match(panel,/confirmada:true,version:actual\.version/);
 assert.match(panel,/v-model="confirmarCancelacion"/);
 assert.match(panel,/Cancelar reserva/);
 assert.match(panel,/setInterval\(\(\)=>\{/);
 assert.match(detalle,/id="destinatario-autorizacion"/);
});

test('Mis reservas e Historial son vistas privadas separadas y se filtran antes de paginar en SQL',()=>{
  const rutas=leer('src/router/index.js');
  const menu=leer('src/components/layout/AppShell.vue');
  const panel=leer('src/views/PanelView.vue');
  const backend=leer('../../backend/Synchro.Api/Services/Encomiendas/EncomiendasOperativasHistorial.cs');
  assert.match(rutas,/path: "\/historial", name: "historial"/);
  assert.match(rutas,/meta: \{ privada: true, titulo: "Historial" \}/);
  assert.match(menu,/ruta: "\/historial", nombre: "Historial"/);
  assert.match(panel,/const esHistorial=computed\(\(\)=>route.name==='historial'\)/);
  assert.match(panel,/categoria:categoria\.value/);
  assert.match(panel,/esHistorial\?'Ver detalle':accionTexto\(detalleDe\(e\)\)/);
  assert.match(panel,/!esHistorial && detalles\[Number\(e.id\)\] && cancelable\(detalleDe\(e\)\)/);
  assert.match(panel,/watch\(\(\)=>route.name/);
  assert.match(backend,/string\? Categoria=null/);
  assert.match(backend,/@categoria=N'Activas'/);
  assert.match(backend,/@categoria=N'Historial'/);
  assert.match(backend,/Estado IN\(N'Cancelada',N'Vencida',N'Rechazada'\)/);
  assert.match(backend,/Estado=N'Retirada' AND EntregaUtc IS NOT NULL/);
  assert.match(backend,/@salto ROWS FETCH NEXT @tamano ROWS ONLY/);
});
