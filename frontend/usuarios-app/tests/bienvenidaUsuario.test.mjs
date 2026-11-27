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
  assert.match(vista,/encomiendasApi\.listar\(\{pagina:1,tamanoPagina:30\}\)/);
  assert.match(vista,/const etapaSiguiente=computed/);
  assert.match(vista,/encomiendasApi\.detalle\(reserva\.id\)/);
  assert.match(vista,/reservaPendiente\.value=elegida/);
  assert.match(vista,/!whatsappConfirmado\.value/);
  assert.match(vista,/Preparando PIN de depósito/);
  assert.match(vista,/return '\/reservas\/'\+id\+'\/solicitar-pin'/);
  assert.match(vista,/Solicitar PIN de depósito/);
  // La solicitud del PIN vive en su propia pantalla; el detalle queda como resumen.
  assert.match(detalle,/class="env-card reserva-ficha"/);
  assert.doesNotMatch(detalle,/async function solicitarPinDeposito/);
  assert.match(api,/pin-deposito\/solicitar/);
  assert.match(api,/metodo: "POST"/);
  assert.match(espera,/await router\.replace\('\/reservas\/'\+id\+'\/solicitar-pin'\)/);
  const solicitud=leer('src/views/SolicitarPinView.vue');
  assert.match(solicitud,/await router\.replace\('\/bienvenida'\)/);
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

test('bienvenida presenta el paso correcto con PIN entregado, deposito y autorizacion destinatario',()=>{
  const vista=leer('src/views/BienvenidaView.vue');
  const detalle=leer('src/views/EncomiendaDetalleView.vue');
  assert.match(vista,/if\(depositoConfirmado\.value\)\{/);
  assert.match(vista,/if\(esPersonal\.value\)return 'personal-resguardado'/);
  assert.match(vista,/if\(pinRetiroEntregado\.value\)return 'esperar-retiro'/);
  assert.match(vista,/if\(destinatarioAutorizado\.value\)return 'pin-retiro'/);
  assert.match(vista,/return 'autorizar-destinatario'/);
  assert.match(vista,/if\(pinDepositoEntregado\.value\)return 'esperar-deposito'/);
  assert.match(vista,/Esperando depósito en estación/);
  assert.match(vista,/Tus pertenencias están resguardadas/);
  assert.match(vista,/Autorizar al destinatario/);
  assert.match(vista,/Esperando retiro del destinatario/);
  assert.match(vista,/Operación completada/);
  assert.match(vista,/enlaceWhatsApp/);
  assert.match(vista,/Enviar invitación por WhatsApp/);
  assert.match(vista,/enlaceInvitacionValido/);
  assert.match(vista,/#destinatario-autorizacion/);
  assert.match(detalle,/id="destinatario-autorizacion"/);
  assert.match(detalle,/v-if="depositoConfirmado && !detalle.cancelacion"/);
  assert.match(detalle,/Enviar invitación por WhatsApp/);
});
test('cancelacion desde bienvenida solo antes de deposito y con confirmacion real',()=>{
  const vista=leer('src/views/BienvenidaView.vue');
  assert.match(vista,/const puedeCancelar=computed/);
  assert.match(vista,/!depositoConfirmado\.value&&!retiroConfirmado\.value/);
  assert.match(vista,/v-if="puedeCancelar"/);
  assert.match(vista,/v-model="motivoCancelacion"/);
  assert.match(vista,/v-model="confirmarCancelacion"/);
  assert.match(vista,/await encomiendasApi\.cancelar\(d\.id,\{/);
  assert.match(vista,/confirmada:true,version:d\.version/);
  assert.match(vista,/Ya no desea utilizar el casillero/);
  assert.match(vista,/Número de destinatario incorrecto/);
  assert.match(vista,/no se reembolsa automáticamente/);
  assert.doesNotMatch(vista,/Math\.random\(/);
});

test('la tarjeta de siguiente paso mantiene acciones de altura normal en móvil',()=>{
  const vista=leer('src/views/BienvenidaView.vue');
  assert.match(vista,/\.inicio-cliente__siguiente-principal>\.inicio-cliente__siguiente-acciones\{grid-column:3;grid-row:1/);
  assert.match(vista,/@media\(max-width:760px\)/);
  assert.match(vista,/\.inicio-cliente__siguiente-principal>\.inicio-cliente__siguiente-acciones\{grid-column:1\/-1;grid-row:2/);
  assert.match(vista,/\.inicio-cliente__siguiente-acciones \.inicio-cliente__accion\{width:100%;min-height:50px;height:auto;max-height:none;flex:0 0 auto/);
  assert.match(vista,/\.inicio-cliente__cancelacion\{/);
});
