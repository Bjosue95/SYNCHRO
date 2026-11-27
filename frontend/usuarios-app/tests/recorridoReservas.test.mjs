import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const leer = nombre =>
  readFileSync(new URL('../src/views/' + nombre + '.vue', import.meta.url), 'utf8');

test('la reserva distingue la autorizacion del remitente y la del destinatario', () => {
  const reserva = leer('PreReservaView');
  assert.match(reserva, /normalizarTelefono\(telefono\.value\)/);
  assert.match(reserva, /La autorización de retiro del destinatario se gestiona después de confirmar el depósito/);
  assert.match(reserva, /Al continuar, podrás consultar el estado de tu solicitud/);
  assert.match(reserva, /await router\.replace\('\/reservas\/'\+d\.id\+'\/pago-pendiente'\)/);
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
  assert.match(vista, /<h2>Revisa tu reserva<\/h2>/);
  assert.match(vista, /<h3>¿Cómo deseas realizar el pago\?<\/h3>/);
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
  assert.match(vista, /venceTarjetaPrueba\.value\.match/);
  assert.match(vista, /test\(codigoTarjetaPrueba\.value\)/);
  assert.match(vista, /limpiarTarjetaPrueba\(\)/);
  assert.match(api, /pagador:/);
  assert.match(api, /modalidadPago:/);
  assert.doesNotMatch(api, /numeroTarjetaPrueba|venceTarjetaPrueba|codigoTarjetaPrueba/);
  assert.doesNotMatch(vista, /localStorage|sessionStorage/);
  assert.doesNotMatch(vista, /Volver a tamaños/);
  assert.match(vista, /El pago presencial se realizará mediante el proceso habitual de la tablet/);
  assert.match(vista, /Confirmar solicitud de pago/);
  assert.match(vista, /modalidadPago:pagador\.value==='Remitente'\?'WebTarjeta'/);
});

test('el historial utiliza solo datos reales y descarta respuestas antiguas', () => {
  const panel = leer('PanelView');
  assert.match(panel, /encomiendasApi\.listar\(/);
  assert.match(panel, /items\.value=datos\.encomiendas/);
  assert.match(panel, /actual!==numeroConsulta/);
  assert.match(panel, /v-if="esHistorial" class="historial-busqueda"/);
  assert.match(panel, /numeroAplicado\.value/);
  assert.match(panel, /v-if="totalPaginas>1"/);
});

test('el detalle solo muestra el resumen hasta confirmar deposito',()=>{
 const detalle=leer('EncomiendaDetalleView');
 assert.match(detalle,/const depositoConfirmado=computed/);
 assert.match(detalle,/v-if="depositoConfirmado && !detalle.cancelacion"/);
 assert.match(detalle,/v-else-if="detalle.destinatario\?\.aceptaEncomienda/);
 assert.match(detalle,/enlaceValido\(detalle.compartir\?\.enlaceWhatsApp\)/);
 assert.match(detalle,/watch\(\(\)=>route.params.id/);
 assert.doesNotMatch(detalle,/Tu PIN de depósito|Progreso de tu operación|Historial de movimientos/);
});

test('el pago web se ofrece para pertenencias y encomiendas sin enviar datos bancarios', () => {
  const vista=leer('PreReservaView');
  assert.match(vista, /<section v-if="paso===7" class="env-card reserva-pago__eleccion">/);
  assert.match(vista, /pagador:pagador\.value==='Remitente'\?'Remitente'/);
  assert.match(vista, /modalidadPago:pagador\.value==='Remitente'\?'WebTarjeta'/);
  assert.match(vista, /@click="pagador=\'Remitente\'"/);
  assert.match(vista, /v-if="pagador==='Remitente'" class="reserva-pagador__check"/);
  assert.doesNotMatch(vista, /@click="pagador=\'Remitente\';paso=8"/);
  assert.match(vista, /v-if="pagoError"/);
  assert.doesNotMatch(vista, /servicio==='Encomienda'&&pagador==='Remitente'&&!tarjetaPruebaValida/);
});

test('el pago muestra instrucciones y permite confirmar para ver errores', () => {
  const vista=leer('PreReservaView');
  assert.doesNotMatch(vista, /function usarDatosPrueba\(\)/);
  assert.match(vista, /@click="pagador=\'Remitente\'"/);
  assert.match(vista, /v-if="pagador==='Remitente'" class="reserva-pagador__check"/);
  assert.doesNotMatch(vista, /@click="pagador=\'Remitente\';paso=8"/);
  assert.match(vista, /<SynButton v-else tipo="button" :cargando="enviando" :disabled="enviando" @click="crear"/);
  assert.match(vista, /pagoError\.value='Revisa el formato/);
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
  assert.match(vista, /const pasosReserva=\['Servicio','Tipo','Tamaño','Estación','Duración','Destinatario','Resumen','Forma de pago','Tarjeta'\]/);
  assert.match(vista, /v-if="paso===0"/);
  assert.match(vista, /v-if="paso===1"/);
  assert.match(vista, /v-if="paso===2 && porTipo.length"/);
  assert.match(vista, /v-if="paso===6" class="env-card reserva-revision/);
  assert.match(vista, /await router\.replace\('\/reservas\/'\+d\.id\+'\/pago-pendiente'\)/);
  assert.doesNotMatch(vista, /intervaloPago=setInterval/);
  const espera=readFileSync(new URL('../src/views/PagoPendienteView.vue',import.meta.url),'utf8');
  assert.match(espera, /setInterval\(\(\)=>\{if\(!document\.hidden\)void consultar\(\);\},5000\)/);
  assert.match(espera, /d\.pago\?\.estado==='ConfirmadoManual'/);
  assert.match(espera, /await router\.replace\('\/reservas\/'\+id\+'\/solicitar-pin'\)/);
});
test('el destinatario se autoriza despues del deposito real',()=>{
 const detalle=leer('EncomiendaDetalleView');
 assert.match(detalle,/v-if="depositoConfirmado && !detalle.cancelacion"/);
 assert.match(detalle,/Enviar invitación por WhatsApp/);
 assert.match(detalle,/pinRetiroEntregado/);
 assert.match(detalle,/compartir el mensaje no confirma/i);
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
  assert.match(vista, /grid-template-columns:repeat\(2,minmax\(0,1fr\)\)/);
  assert.match(vista, /:last-child:nth-child\(odd\):not\(:first-child\)/);
  assert.match(vista, /:only-child \{\s*grid-column:1\/-1;\s*justify-self:center;/);
  assert.match(vista, /overflow-y:auto;overscroll-behavior:contain/);
  assert.match(vista, /<footer v-if="!cargando && paso<=ultimoPaso"/);
});

test('indicador de progreso compacto y tarjetas adaptadas a la cantidad', () => {
  const vista=leer('PreReservaView');
  assert.match(vista, /reserva-pasos__contador/);
  assert.match(vista, /:style="\{width:\(pasoNumero\/pasosVisibles\.length\*100\)\+'%'\}"/);
  assert.match(vista, /\.reserva-vista \.reserva-opciones > :only-child \{\s*grid-column:1\/-1;\s*justify-self:center;/);
  assert.match(vista, /\.reserva-vista \.reserva-opciones > :last-child:nth-child\(odd\):not\(:first-child\)/);
  assert.match(vista, /\.reserva-pasos__barra \{\s*display:block;\s*height:3px;/);
});

test('la cuarta celda se mantiene libre y una tarjeta sola conserva ancho proporcional', () => {
  const vista=leer('PreReservaView');
  assert.match(vista, /:last-child:nth-child\(odd\):not\(:first-child\) \{\s*grid-column:auto;/);
  assert.match(vista, /width:min\(100%,clamp\(190px,45vw,330px\)\)/);
  assert.doesNotMatch(vista, /:last-child:nth-child\(odd\):not\(:first-child\) \{\s*grid-column:1\/-1;/);
});

test('dos tarjetas usan una sola fila sin estirarse hasta el fondo', () => {
  const vista=leer('PreReservaView');
  assert.match(vista, /grid-auto-rows:auto;/);
  assert.match(vista, /flex:0 0 auto;\s*align-content:start;/);
  assert.match(vista, /\.reserva-vista > \.env-card:has\(\.reserva-opciones\) \{\s*flex:0 0 auto;/);
  assert.match(vista, /height:clamp\(185px,28dvh,220px\)/);
  assert.doesNotMatch(vista, /grid-auto-rows:minmax\(0,1fr\)/);
});

test('escritorio ajusta tres opciones al alto disponible y no repite titulo de detalles', () => {
  const vista=leer('PreReservaView');
  assert.doesNotMatch(vista, /DETALLES DE LA RESERVA/);
  assert.match(vista, /\.reserva-vista \.reserva-opciones:has\(> :nth-child\(3\)\) \{/);
  assert.match(vista, /grid-template-rows:repeat\(2,minmax\(0,1fr\)\)/);
  assert.match(vista, /\.reserva-vista > \.env-card:has\(\.reserva-opciones > :nth-child\(3\)\) \{/);
  assert.match(vista, /min-height:0;\s*overflow:hidden;/);
  assert.match(vista, /\.reserva-vista \.reserva-opciones:has\(> :nth-child\(3\)\) :deep\(\.seleccion-reserva\) \{/);
});

test('navegacion inferior integrada y con margenes reales en pantalla', () => {
  const vista=leer('PreReservaView');
  assert.match(vista, /\.env-page--asistente > \.reserva-paso__navegacion \{/);
  assert.match(vista, /margin-inline:clamp\(16px,2\.4vw,30px\)/);
  assert.match(vista, /background:var\(--bg\)/);
  assert.match(vista, /width:calc\(100% - 2 \* clamp\(16px,2\.4vw,30px\)\)/);
  assert.doesNotMatch(vista, /\.reserva-paso__navegacion\{flex-direction:column-reverse\}/);
  assert.doesNotMatch(vista, /\.reserva-paso__navegacion \{display:flex;gap:12px;justify-content:space-between;\}/);
});

test('dos tarjetas conservan altura de las de tipo y botones regresan al mismo lado', () => {
  const vista=leer('PreReservaView');
  assert.match(vista, /height:clamp\(185px,28dvh,225px\)/);
  assert.match(vista, /height:clamp\(185px,28dvh,220px\)/);
  assert.match(vista, /\.env-page--asistente > \.reserva-paso__navegacion > :first-child \{margin-left:auto;\}/);
  assert.match(vista, /\.env-page--asistente > \.reserva-paso__navegacion > :last-child \{margin-left:0;\}/);
  assert.match(vista, /justify-content:flex-end;/);
});

test('las tarjetas y acciones usan la paleta de tablet con SVG visibles en claro y oscuro', () => {
  const vista=leer('PreReservaView');
  const tarjeta=readFileSync(new URL('../src/components/ui/SeleccionReservaCard.vue',import.meta.url),'utf8');
  const icono=readFileSync(new URL('../src/components/ui/IconoTablet.vue',import.meta.url),'utf8');
  assert.match(tarjeta,/background:#103956;/);
  assert.match(tarjeta,/border-color:#315573;/);
  assert.match(tarjeta,/background:#174b65;/);
  assert.match(tarjeta,/color:#5ec6e8;/);
  assert.match(vista,/background:#f4feff;/);
  assert.match(vista,/background:#123d5b;/);
  assert.match(vista,/background:#204766;/);
  assert.match(vista,/filter:none;/);
  assert.doesNotMatch(vista,/\.reserva-final__continuar :deep\(\.icono-tablet\) \{ filter: var\(--action-icon-filter\); \}/);
  assert.match(icono,/tema\.value === 'oscuro' \? '-oscuro' : ''/);
});

test('ubicaciones futuras deshabilitadas sin contaminar inventario SQL', () => {
  const vista=leer('PreReservaView');
  assert.match(vista,/UNAH · Campus Comayagua/);
  assert.match(vista,/Parque Central de Comayagua/);
  assert.match(vista,/Próximamente · No disponible/);
  assert.match(vista,/v-for="estacion in estacionesProximas"/);
  assert.match(vista,/reserva-opcion-ubicacion--proxima/);
  assert.match(vista,/class="reserva-opcion-ubicacion reserva-opcion-ubicacion--proxima"[\s\S]*?disabled>/);
  assert.match(vista,/const ubicaciones=computed\(\(\)=>\[\.\.\.new Map/);
  assert.match(vista,/ubicacionId:ubicacion\.value/);
  assert.doesNotMatch(vista,/@click="ubicacion=estacion\.id"/);
});
test('selección de estación comienza arriba sin espacio vacío', () => {
  const vista=leer('PreReservaView');
  assert.match(vista,/\.reserva-vista > \.reserva-final \{\s*flex:0 0 auto;\s*justify-content:flex-start;/);
  assert.match(vista,/\.reserva-vista > \.reserva-final \.reserva-final__intro \{align-content:start;\}/);
});

test('duraciones fijas del backend llegan hasta cuatro horas sin incrementadores', () => {
  const vista=leer('PreReservaView');
  const config=JSON.parse(readFileSync(new URL('../../../backend/Synchro.Api/appsettings.json',import.meta.url),'utf8'));
  const backend=readFileSync(new URL('../../../backend/Synchro.Api/Services/Encomiendas/ReservasPrototipo.cs',import.meta.url),'utf8');
  assert.deepEqual(config.ReservasPrototipo.DuracionesMinutos,[60,120,180,240]);
  for(const tarifa of Object.values(config.ReservasPrototipo.TarifasNormales)){
    for(const minutos of [60,120,180,240])assert.ok(tarifa[minutos]>0);
  }
  assert.match(backend,/m is >=60 and <=240/);
  assert.match(backend,/t\.Key>=60 && t\.Key<=240/);
  assert.match(backend,/if\(d\.Minutos<60 \|\| d\.Minutos>240/);
  assert.match(vista,/v-for="m in duraciones"/);
  assert.doesNotMatch(vista,/@click="aumentarDuracion"/);
});

test('revisar reserva comparte el estilo del boton continuar sin desbordarse', () => {
  const vista=leer('PreReservaView');
  assert.match(vista, /class="boton boton-primario reserva-paso__continuar"/);
  assert.match(vista, /class="reserva-paso__continuar-texto"/);
  assert.match(vista, /min-width:clamp\(160px,18vw,210px\)/);
  assert.match(vista, /white-space:nowrap;/);
  assert.match(vista, /@media\(max-width:390px\)/);
});

test('resumen sin subtitulo redundante y ajustado a una pantalla movil', () => {
  const vista=leer('PreReservaView');
  assert.doesNotMatch(vista,/REVISIÓN DE LA RESERVA/);
  assert.match(vista, /class="env-card reserva-revision reserva-revision--resumen"/);
  assert.match(vista,/class="reserva-revision__ubicacion"/);
  assert.match(vista,/\.reserva-revision--resumen \.reserva-revision__ubicacion \{grid-column:1\/-1;\}/);
  assert.match(vista, /v-if="paso===7" class="env-card reserva-pago__eleccion"/);
  assert.doesNotMatch(vista,/Importe de la reserva: <strong>/);
  assert.match(vista,/\.reserva-vista > \.reserva-revision--resumen \{\s*flex:0 0 auto;/);
});

test('el pago web usa pantalla independiente con titular y sin autocompletado', () => {
  const vista=leer('PreReservaView');
  const api=vista.slice(vista.indexOf('encomiendasApi.reservar({'),vista.indexOf('});',vista.indexOf('encomiendasApi.reservar({')));
  assert.match(vista,/v-if="paso===6" class="env-card reserva-revision/);
  assert.match(vista,/v-if="paso===7" class="env-card reserva-pago__eleccion"/);
  assert.match(vista,/v-if="paso===8 && pagador==='Remitente'"/);
  assert.match(vista,/@click="pagador='Remitente'"/);
  assert.match(vista,/@click="pagador='Destinatario'"/);
  assert.match(vista, /class="reserva-pagador__check"/);
  assert.match(vista, /v-if="pagador==='Remitente'" class="reserva-pagador__check"/);
  assert.match(vista, /v-if="pagador==='Destinatario'" class="reserva-pagador__check"/);
  assert.match(vista, /paso===7 \? \(pagador==='Remitente' \? 'Continuar a tarjeta' : 'Confirmar reserva'\)/);
  assert.doesNotMatch(vista, /@click="pagador='Remitente';paso=8"/);
  assert.match(vista,/const titularTarjeta=ref\(''\)/);
  assert.match(vista,/v-model="titularTarjeta"/);
  assert.match(vista,/titularTarjeta\.trim\(\) \|\| 'CLIENTE SYNCHRO'/);
  assert.match(vista,/await router\.replace\('\/reservas\/'\+d\.id\+'\/pago-pendiente'\)/);
  assert.doesNotMatch(vista,/Usar datos de prueba/);
  assert.doesNotMatch(api,/titularTarjeta|numeroTarjetaPrueba|venceTarjetaPrueba|codigoTarjetaPrueba/);
});

test('la solicitud aprobada abre una ruta independiente y privada de espera', () => {
  const reserva=leer('PreReservaView');
  const vista=leer('PagoPendienteView');
  const router=readFileSync(new URL('../src/router/index.js',import.meta.url),'utf8');
  assert.match(router,/path: "\/reservas\/:id\(\\\\d\+\)\/pago-pendiente"/);
  assert.match(router,/name: "pago-pendiente"/);
  assert.match(router,/meta: \{ privada: true, titulo: "Confirmación del pago" \}/);
  assert.match(reserva,/await router\.replace\('\/reservas\/'\+d\.id\+'\/pago-pendiente'\)/);
  assert.match(vista,/encomiendasApi\.detalle\(id\)/);
  assert.match(vista,/d\.condiciones\?\.pagoConfirmado/);
  assert.match(vista,/if\(d\.pago\?\.estado==='ConfirmadoManual'\|\|d\.condiciones\?\.pagoConfirmado\)/);
  assert.match(vista,/clearInterval\(reloj\)/);
  assert.match(vista,/onBeforeUnmount/);
  assert.doesNotMatch(vista,/localStorage|sessionStorage|numeroTarjetaPrueba|codigoTarjetaPrueba|titularTarjeta/);
});

test('el pago conserva un aviso breve de demostracion sin repetirlo en cada campo', () => {
  const vista=leer('PreReservaView');
  const espera=leer('PagoPendienteView');
  assert.match(vista,/Pago de demostración\. Puedes usar datos inventados/);
  assert.doesNotMatch(vista,/Escribe un nombre ficticio/);
  assert.doesNotMatch(vista,/Revisa los datos de demostración para continuar/);
  assert.doesNotMatch(vista,/confirmación administrativa\.<\/span>/);
  assert.match(espera,/role="status" aria-live="polite"/);
  assert.doesNotMatch(espera,/<h1|Total solicitado|Pago de demostración/);
});

test('la tarjeta de demostración valida formatos, no valores bancarios específicos', () => {
  const vista=leer('PreReservaView');
  assert.match(vista,/const tarjetaPruebaValida=computed\(\(\)=>\{/);
  assert.match(vista,/\^\\d\{16\}\$/);
  assert.match(vista,/mes>=1&&mes<=12/);
  assert.match(vista,/\^\\d\{3\}\$/);
  assert.doesNotMatch(vista,/==='4242424242424242'/);
  assert.doesNotMatch(vista,/==='12\/30'/);
  assert.doesNotMatch(vista,/==='123'/);
});

test('la espera de pago usa animación automatica sin botones ni textos redundantes', () => {
  const vista=leer('PagoPendienteView');
  assert.match(vista,/class="espera-pago__animacion"/);
  assert.match(vista,/@keyframes espera-giro/);
  assert.match(vista,/@keyframes espera-flotar/);
  assert.match(vista,/@media\(prefers-reduced-motion:reduce\)/);
  assert.match(vista,/setInterval\(\(\)=>\{if\(!document\.hidden\)void consultar\(\);\},5000\)/);
  assert.match(vista,/await router\.replace\('\/reservas\/'\+id\+'\/solicitar-pin'\)/);
  assert.doesNotMatch(vista,/Actualizar estado|Ver mi reserva|@click="consultar"/);
  assert.doesNotMatch(vista,/RouterLink/);
});

test('el resumen tiene unicamente numero importe servicio y estacion antes de deposito',()=>{
 const detalle=leer('EncomiendaDetalleView');
 const resumen=detalle.indexOf('class="env-card reserva-ficha"');
 const operacion=detalle.indexOf('v-if="depositoConfirmado && !detalle.cancelacion"');
 assert.ok(resumen>=0&&operacion>resumen);
 assert.match(detalle,/Reserva #\{\{ detalle.id \}\}/);
 assert.match(detalle,/>Servicio<\/span>/);
 assert.match(detalle,/>Importe<\/span>/);
 assert.match(detalle,/>Estación<\/span>/);
 assert.match(detalle,/Pertenencias están resguardadas|pertenencias están resguardadas/);
 assert.match(detalle,/Autorizar al destinatario/);
 assert.doesNotMatch(detalle,/reserva-seguimiento__pasos|reserva-acordeon|reserva-pin__accion/);
});

test('pantalla de tarjeta en escritorio centra el contenido y no recorta titular', () => {
  const vista=leer('PreReservaView');
  assert.match(vista, /@media \(min-width:851px\)/);
  assert.match(vista, /\.reserva-vista:has\(> \.reserva-prueba\)/);
  assert.match(vista, /width:min\(100%,1080px\)/);
  assert.match(vista, /grid-template-columns:minmax\(0,370px\) minmax\(0,1fr\)/);
  assert.match(vista, /max-height:none;/);
  assert.match(vista, /aspect-ratio:1\.7;/);
  assert.match(vista, /overflow-wrap:anywhere;/);
  assert.match(vista, /@media \(min-width:851px\) and \(max-height:740px\)/);
});

test('la animación del pago abre pantalla independiente de solicitud y no genera PIN automáticamente',()=>{
  const espera=leer('PagoPendienteView');
  const solicitud=leer('SolicitarPinView');
  assert.match(espera,/d\.pago\?\.estado==='ConfirmadoManual'/);
  assert.match(espera,/await router\.replace\('\/reservas\/'\+id\+'\/solicitar-pin'\)/);
  assert.doesNotMatch(espera,/encomiendasApi\.solicitarPinDeposito\(/);
  assert.match(solicitud,/async function solicitar\(\)/);
  assert.match(solicitud,/await encomiendasApi\.solicitarPinDeposito\(reservaId\.value\)/);
  assert.match(solicitud,/@click="solicitar"/);
  assert.match(solicitud,/if\(entregado\)\{/);
  assert.match(solicitud,/await router\.replace\('\/bienvenida'\)/);
  assert.match(solicitud,/['"]Entregado['"]/);
  assert.match(solicitud,/clearInterval\(reloj\)/);
  assert.match(espera,/:global\(\.app-shell:has\(\.espera-pago\) \.topbar\)\{display:none;\}/);
});

test('si la sesión caduca durante la animación se vuelve al acceso conservando la reserva',()=>{
  const espera=leer('PagoPendienteView');
  assert.match(espera,/error instanceof ErrorApi&&error\.estado===401/);
  assert.match(espera,/clearInterval\(reloj\)/);
  assert.match(espera,/estadoSesion\.cliente=null/);
  assert.match(espera,/await router\.replace\(\{name:'acceso',query:\{volver:route\.fullPath\}\}\)/);
  assert.match(espera,/if\(await gestionarSesion\(e\)\)return/);
  assert.match(espera,/redirigiendoAcceso/);
  assert.doesNotMatch(espera,/await router\.replace\('\/bienvenida'\)[\s\S]*?error\.estado===401/);
});

test('la espera no aprueba el PIN ni regresa a Bienvenida al confirmar solamente el pago',()=>{
 const espera=leer('PagoPendienteView');
 const solicitud=leer('SolicitarPinView');
 assert.match(espera,/mostrarAyuda\.value=false/);
 assert.match(espera,/mostrarAyuda\.value=true/);
 assert.doesNotMatch(espera,/await router\.replace\('\/bienvenida'\)/);
 assert.match(solicitud,/pinEntregado/);
 assert.match(solicitud,/if\(entregado\)\{/);
 assert.match(solicitud,/await router\.replace\('\/bienvenida'\)/);
});

test('solicitud PIN se abre solo tras pago confirmado y espera entrega administrativa',()=>{
  const rutas=readFileSync(new URL('../src/router/index.js',import.meta.url),'utf8');
  const pago=leer('PagoPendienteView');
  const pin=leer('SolicitarPinView');
  assert.match(rutas,/name: "solicitar-pin"/);
  assert.match(rutas,/component: \(\) => import\("\.\.\/views\/SolicitarPinView\.vue"\)/);
  assert.match(pago,/await router\.replace\('\/reservas\/'\+id\+'\/solicitar-pin'\)/);
  assert.doesNotMatch(pago,/encomiendasApi\.solicitarPinDeposito/);
  assert.match(pin,/const puedeSolicitar=computed/);
  assert.match(pin,/@click="solicitar"/);
  assert.match(pin,/await encomiendasApi\.solicitarPinDeposito\(reservaId\.value\)/);
  assert.match(pin,/['"]Entregado['"]/);
  assert.match(pin,/await router\.replace\('\/bienvenida'\)/);
  assert.match(pin,/if\(await manejarSesion\(e\)\)return/);
});
