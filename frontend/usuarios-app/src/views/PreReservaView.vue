<script setup lang="ts">
import { computed,onMounted,onBeforeUnmount,ref,watch } from 'vue';
import { useRouter,useRoute } from 'vue-router';
import { encomiendasApi } from '../services/encomiendasApi.js';
import { normalizarTelefono,TAMANOS_ENCOMIENDA } from '../services/validaciones.js';
import SeleccionReservaCard from '../components/ui/SeleccionReservaCard.vue';
import SynButton from '../components/ui/SynButton.vue';
import IconoTablet from '../components/ui/IconoTablet.vue';
import AppIcon from '../components/ui/AppIcon.vue';
const router=useRouter(),route=useRoute();
const servicio=ref(route.query.servicio==='Encomienda'?'Encomienda':'Personal'),tipo=ref('Normal'),tamano=ref(''),ubicacion=ref(''),minutos=ref(0),telefono=ref('');
const opciones=ref<any[]>([]),cargando=ref(true),actualizando=ref(false),enviando=ref(false),error=ref(''),ubicacionPendiente=ref(false),revisando=ref(false);
const paso=ref(0);
const reservaEnEspera=ref<number|null>(null);
const estadoEnEspera=ref<any>(null);
const consultandoPago=ref(false);
let intervaloPago: ReturnType<typeof setInterval>|undefined;
let consultaPagoActiva=false;
const pasosReserva=['Servicio','Tipo','Tamaño','Estación','Duración','Destinatario','Resumen','Pago'];
const ultimoPaso=7;
const pasosVisibles=computed(()=>servicio.value==='Personal'
  ?pasosReserva.filter((_,indice)=>indice!==5):pasosReserva);
const pasoNumero=computed(()=>paso.value>=6&&servicio.value==='Personal'?paso.value:paso.value+1);
const pasoActual=computed(()=>pasosReserva[paso.value]||'Confirmación');
const saltoSiguiente=(actual:number)=>actual===4&&servicio.value==='Personal'?6:actual+1;
const saltoAnterior=(actual:number)=>actual===6&&servicio.value==='Personal'?4:actual-1;
const textoEstadoPago=computed(()=>{
  if(!estadoEnEspera.value)return 'Comprobando el estado del pago con SYNCHRO…';
  if(['Vencida','Cancelada','Rechazada'].includes(estadoEnEspera.value.estado))return 'La reserva ya no está activa. Consulta su detalle antes de intentar otra operación.';
  if(!estadoEnEspera.value.condiciones?.remitenteAutorizado)return 'Pago solicitado. Falta comprobar el consentimiento de WhatsApp del titular.';
  return 'Pago solicitado. Esperando la aprobación del administrador.';
});
function continuar(){
  error.value='';
  if(paso.value===1&&!tipo.value){error.value='Selecciona un tipo de casillero.';return;}
  if(paso.value===2&&(!tamano.value||!porTamano.value.length)){error.value='Selecciona un tamaño disponible.';return;}
  if(paso.value===3&&!ubicacion.value){error.value='Selecciona una estación.';return;}
  if(paso.value===4&&!disponible.value){error.value='Selecciona una duración disponible.';return;}
  if(paso.value===5&&!normalizarTelefono(telefono.value)){error.value='Introduce el WhatsApp del destinatario con código de país.';return;}
  if(paso.value===5||paso.value===4&&servicio.value==='Personal'){
    revisar();return;
  }
  paso.value=Math.min(ultimoPaso,saltoSiguiente(paso.value));
}
function regresar(){
  if(enviando.value||reservaEnEspera.value!==null)return;
  error.value='';pagoError.value='';
  if(paso.value===7)limpiarTarjetaPrueba();
  if(paso.value===6)revisando.value=false;
  paso.value=Math.max(0,saltoAnterior(paso.value));
}
async function consultarPago(){
  if(reservaEnEspera.value===null||consultaPagoActiva||document.hidden)return;
  consultaPagoActiva=true;consultandoPago.value=true;
  try{
    const d=await encomiendasApi.detalle(reservaEnEspera.value);
    if(reservaEnEspera.value===null||d?.id!==reservaEnEspera.value)return;
    estadoEnEspera.value=d;
    if(d.pago?.estado==='ConfirmadoManual'){
      const id=reservaEnEspera.value;
      clearInterval(intervaloPago);
      intervaloPago=undefined;
      reservaEnEspera.value=null;
      await router.replace('/reservas/'+id);
    }
  }catch(e){error.value=(e as Error).message||'No pudimos actualizar el estado del pago.';}
  finally{consultaPagoActiva=false;consultandoPago.value=false;}
}
function detenerConsultaPago(){
  clearInterval(intervaloPago);intervaloPago=undefined;
}
const tipos=[{id:'Normal',nombre:'Normal'},{id:'Refrigerado',nombre:'Refrigerado'},{id:'Caliente',nombre:'Caliente'}];
// Campos solamente de memoria local. No se envían al backend ni se almacenan.
const pagador=ref<'Remitente'|'Destinatario'>('Remitente');
const numeroTarjetaPrueba=ref(''),venceTarjetaPrueba=ref(''),codigoTarjetaPrueba=ref('');
const tarjetaPruebaValida=computed(()=>numeroTarjetaPrueba.value.replace(/\D/g,'')==='4242424242424242'
  &&venceTarjetaPrueba.value.replace(/\s/g,'')==='12/30'&&codigoTarjetaPrueba.value.trim()==='123');
const pagoError=ref('');
function usarDatosPrueba(){
  numeroTarjetaPrueba.value='4242 4242 4242 4242';
  venceTarjetaPrueba.value='12/30';
  codigoTarjetaPrueba.value='123';
  pagoError.value='';
}
function cambiarNumeroTarjeta(valor:string){
  const digitos=valor.replace(/\D/g,'').slice(0,16);
  numeroTarjetaPrueba.value=digitos.replace(/(.{4})/g,'$1 ').trim();
  pagoError.value='';
}
function cambiarVencimiento(valor:string){
  const digitos=valor.replace(/\D/g,'').slice(0,4);
  venceTarjetaPrueba.value=digitos.length>2 ? digitos.slice(0,2)+'/'+digitos.slice(2) : digitos;
  pagoError.value='';
}
function limpiarTarjetaPrueba(){
  numeroTarjetaPrueba.value='';
  venceTarjetaPrueba.value='';
  codigoTarjetaPrueba.value='';
  pagoError.value='';
}
watch(pagador,limpiarTarjetaPrueba);
onBeforeUnmount(()=>{limpiarTarjetaPrueba();detenerConsultaPago();});
// Una opción corresponde a un casillero; una duración nunca suma otro casillero.
const contarCasilleros=(lista:any[])=>new Set(lista.map(o=>typeof o.codigo==='string'?o.codigo.trim():'').filter(Boolean)).size;
const cantidadPorTipo=(t:string)=>contarCasilleros(opciones.value.filter(o=>o.tipo===t));
const cantidadPorTamano=(t:string)=>contarCasilleros(porTipo.value.filter(o=>o.tamano===t));
const porTipo=computed(()=>opciones.value.filter(o=>o.tipo===tipo.value));
const porTamano=computed(()=>porTipo.value.filter(o=>o.tamano===tamano.value));
const ubicaciones=computed(()=>[...new Map<string,string>(porTamano.value.map(o=>[o.ubicacionId,o.ubicacion])).entries()]);
const elegibles=computed(()=>porTamano.value.filter(o=>o.ubicacionId===ubicacion.value));
const duraciones=computed<number[]>(()=>[...new Set<number>(elegibles.value.flatMap(o=>o.duraciones))].sort((a,b)=>a-b));
const tipoPermitido=computed(()=>servicio.value==='Encomienda'||tipo.value==='Normal');
const disponible=computed(()=>tipoPermitido.value && elegibles.value.some(o=>o.duraciones.includes(minutos.value)));
const importeReserva=computed<number|null>(()=>{
  const opcion=elegibles.value.find(o=>o.duraciones.includes(minutos.value));
  if(!opcion)return null;
  const valor=opcion.tarifas?.[minutos.value];
  const importe=Number(valor);
  return valor!=null&&Number.isFinite(importe)&&importe>0?importe:null;
});
const importeTexto=computed(()=>importeReserva.value===null?'Importe por definir':new Intl.NumberFormat('es-HN',{style:'currency',currency:'HNL',minimumFractionDigits:2,maximumFractionDigits:2}).format(importeReserva.value));
const nombreUbicacion=(nombre:string)=>ubicacionPendiente.value?'Estación SYNCHRO':nombre;
const ubicacionNombre=computed(()=>{
  const nombre=ubicaciones.value.find(([id])=>id===ubicacion.value)?.[1];
  if(!nombre)return 'Por seleccionar';
  return ubicacionPendiente.value?'Estación SYNCHRO · Dirección pendiente':nombre;
});
const nombreDuracion=(valor:number)=>valor%60===0
  ? `${valor/60} ${valor===60?'hora':'horas'}`
  : `${valor} min`;
watch(() => route.query.servicio, valor => {
  const nuevo = valor === 'Encomienda' ? 'Encomienda' : 'Personal';
  if (nuevo !== servicio.value) { servicio.value = nuevo; revisando.value = false; }
});
watch(tipo,()=>{tamano.value='';ubicacion.value='';minutos.value=0;revisando.value=false;});
watch(tamano,()=>{ubicacion.value='';minutos.value=0;revisando.value=false;});
watch(ubicaciones,lista=>{if(lista.length===1)ubicacion.value=lista[0][0];});
watch(duraciones,lista=>{if(!lista.includes(minutos.value))minutos.value=lista[0]||0;});
let versionCatalogo=0;
async function cargar(discreta=false){
  const actual=++versionCatalogo;
  // El cambio de servicio consulta SQL sin desmontar las tarjetas.
  const cargaInicial=!discreta || (cargando.value && opciones.value.length===0);
  if(cargaInicial)cargando.value=true;
  else actualizando.value=true;
  error.value='';
  try {
    const datos=await encomiendasApi.disponibilidad();
    if(actual!==versionCatalogo)return;
    if(!Array.isArray(datos?.opciones))throw new Error('La disponibilidad no pudo verificarse.');
    opciones.value=datos.opciones;
    ubicacionPendiente.value=Boolean(datos.ubicacionPendiente);
    if(!disponible.value){revisando.value=false;if(paso.value>4&&reservaEnEspera.value===null)paso.value=4;}
  } catch(e) {
    if(actual!==versionCatalogo)return;
    opciones.value=[];
    ubicacionPendiente.value=false;
    revisando.value=false;
    if(reservaEnEspera.value===null)paso.value=Math.min(paso.value,4);
    error.value=(e as Error).message || 'No pudimos consultar la disponibilidad.';
  } finally {
    if(actual===versionCatalogo){
      cargando.value=false;
      actualizando.value=false;
    }
  }
}
// Encomienda y pertenencias comparten el inventario, pero lo refrescamos en
// segundo plano para evitar bloquear la seleccion.
watch(servicio,(nuevo)=>{
  // Frío y calor se reservan exclusivamente para encomiendas.
  // Al volver a pertenencias personales se restaura Normal sin ocultar las tarjetas.
  if(nuevo==='Personal' && tipo.value!=='Normal')tipo.value='Normal';
  pagador.value='Remitente';
  limpiarTarjetaPrueba();
  revisando.value=false;
  if(reservaEnEspera.value===null&&paso.value>0)paso.value=0;
  void cargar(true);
});
function revisar(){
  error.value='';
  if(!disponible.value){
    error.value='Selecciona tipo, tamaño, ubicación y duración disponibles.';
    return;
  }
  if(servicio.value==='Encomienda'&&!normalizarTelefono(telefono.value)){
    error.value='Escribe el WhatsApp del destinatario con código de país.';
    return;
  }
  revisando.value=true;
  paso.value=6;
}
async function crear(){
  if(enviando.value)return;
  if(!disponible.value || (servicio.value==='Encomienda'&&!normalizarTelefono(telefono.value))){
    revisando.value=false;
    error.value='Verifica la selección antes de crear tu reserva.';
    return;
  }
  if(pagador.value==='Remitente'&&!tarjetaPruebaValida.value){
    pagoError.value='Los valores no coinciden con los datos habilitados. Utiliza «Usar datos de prueba» y vuelve a confirmar.';
    error.value='Verifica los datos de pago antes de continuar.';
    return;
  }
  enviando.value=true;
  pagoError.value='';
  error.value='';
  try {
    const d=await encomiendasApi.reservar({
      servicio:servicio.value,
      tipoCasillero:tipo.value,
      tamanoSolicitado:tamano.value,
      ubicacionId:ubicacion.value,
      minutos:minutos.value,
      telefonoDestinatario:servicio.value==='Encomienda'?normalizarTelefono(telefono.value):null,
      pagador:pagador.value==='Remitente'?'Remitente':servicio.value==='Encomienda'?'Destinatario':null,
      modalidadPago:pagador.value==='Remitente'?'WebTarjeta':servicio.value==='Encomienda'?'Tablet':null
    });
    if(!Number.isSafeInteger(Number(d?.id))||Number(d.id)<=0)
      throw new Error('La reserva fue recibida, pero el servidor no devolvió una referencia válida. Consulta Mis reservas antes de crear otra.');
    limpiarTarjetaPrueba();
    if(pagador.value==='Remitente'){
      reservaEnEspera.value=Number(d.id);
      estadoEnEspera.value=null;
      paso.value=8;
      detenerConsultaPago();
      void consultarPago();
      intervaloPago=setInterval(()=>{void consultarPago();},5000);
    }else{
      await router.replace('/reservas/'+d.id);
    }
  } catch(e) {
    error.value=(e as Error).message || 'No se pudo completar la reserva.';
    // En caso de error de conexión no se genera otra reserva automáticamente.
    revisando.value=false;
  } finally { enviando.value=false; }
}
onMounted(cargar);
</script>
<template>
  <section class="env-page env-page--asistente">
    <header class="env-heading"><div><h1>Reservar casillero</h1><p>Elige qué vas a guardar y encuentra un casillero disponible.</p></div></header>
    <p v-if="error" class="alerta" role="alert">{{ error }}</p><p v-if="cargando" role="status">Consultando disponibilidad…</p>
    <span v-if="actualizando" class="reserva-sr-only" role="status">Actualizando disponibilidad</span>
    <nav v-if="paso<=ultimoPaso" class="reserva-pasos" aria-label="Progreso de la reserva">
      <div class="reserva-pasos__texto">
        <span class="reserva-pasos__contador">{{ pasoNumero }} / {{ pasosVisibles.length }}</span>
        <strong>{{ pasoActual }}</strong>
      </div>
      <div class="reserva-pasos__barra" role="progressbar" :aria-valuenow="pasoNumero" :aria-valuemin="1" :aria-valuemax="pasosVisibles.length" :aria-valuetext="pasoActual">
        <span :style="{width:(pasoNumero/pasosVisibles.length*100)+'%'}"></span>
      </div>
    </nav>
    <div v-if="!cargando" class="reserva-vista" :key="paso">
      <template v-if="paso<=5">
        <section v-if="paso===0" class="env-card"><h2>¿Qué vas a guardar?</h2><div class="reserva-opciones">
          <SeleccionReservaCard titulo="Pertenencias personales" descripcion="Guardarlas y retirarlas con tus PIN" icono-tablet="guardar" :seleccionado="servicio==='Personal'" @seleccionar="servicio='Personal'" />
          <SeleccionReservaCard titulo="Una encomienda" descripcion="El destinatario retira con su PIN" icono-tablet="encomienda" :seleccionado="servicio==='Encomienda'" @seleccionar="servicio='Encomienda'" />
        </div></section>
        <section v-if="paso===1" class="env-card"><h2>Tipo de casillero</h2><div class="reserva-opciones reserva-tres">
          <SeleccionReservaCard v-for="t in tipos" :key="t.id" :titulo="t.nombre"
            :icono-tablet="t.id" :seleccionado="tipo===t.id"
            :descripcion="servicio==='Personal' && t.id!=='Normal' ? 'Exclusivo para encomiendas' : undefined"
            :disponibles="servicio==='Personal' && t.id!=='Normal' ? undefined : cantidadPorTipo(t.id)"
            :disabled="servicio==='Personal' && t.id!=='Normal'"
            @seleccionar="tipo=t.id" />
        </div><p v-if="!opciones.length">No hay casilleros compatibles disponibles. <button class="boton boton-secundario" @click="cargar(true)">Actualizar disponibilidad</button></p></section>
        <section v-if="paso===2 && porTipo.length" id="reserva-seleccion-tamano" class="env-card"><h2>Tamaño</h2><div class="reserva-opciones reserva-tres">
          <SeleccionReservaCard v-for="t in TAMANOS_ENCOMIENDA" :key="t.valor" :titulo="t.nombre" :descripcion="t.detalle" :icono-tablet="t.valor" :seleccionado="tamano===t.valor" :disponibles="cantidadPorTamano(t.valor)" @seleccionar="tamano=t.valor" />
        </div></section>
        <form v-if="paso>=3 && paso<=5 && tamano" class="env-card formulario reserva-final" @submit.prevent="revisar">
          <div class="reserva-final__intro">
            <h2>{{ paso===3 ? "Selecciona la estación" : paso===4 ? "¿Por cuánto tiempo?" : "Destinatario de la encomienda" }}</h2>
            <p>Selecciona una opción para continuar al siguiente paso.</p>
          </div>

          <div class="reserva-final__grid">
            <section v-if="paso===3" class="reserva-bloque" aria-labelledby="reserva-titulo-ubicacion">
              <div class="reserva-bloque__cabecera">
                <span class="reserva-bloque__icono"><IconoTablet nombre="ubicacion" :tamano="30" /></span>
                <div>
                  <h3 id="reserva-titulo-ubicacion">Ubicación</h3>
                  <p>Selecciona la estación donde utilizarás el casillero.</p>
                </div>
              </div>
              <div class="reserva-ubicaciones" role="group" aria-label="Ubicación de la reserva">
                <button v-for="[id,nombre] in ubicaciones" :key="id" type="button"
                  class="reserva-opcion-ubicacion"
                  :class="{ 'es-seleccionada': ubicacion===id }"
                  :aria-pressed="ubicacion===id"
                  @click="ubicacion=id">
                  <span class="reserva-opcion-ubicacion__contenido">
                    <strong>{{ nombreUbicacion(nombre) }}</strong>
                    <small v-if="ubicacionPendiente">Dirección pendiente de confirmación</small>
                    <small v-else>Seleccionar esta estación</small>
                  </span>
                  <span class="reserva-opcion-ubicacion__marca" aria-hidden="true">
                    <AppIcon nombre="check" :tamano="19" />
                  </span>
                </button>
                <p v-if="!ubicaciones.length" class="env-help">No hay ubicaciones para este tamaño. Selecciona otra opción.</p>
              </div>
              <p v-if="ubicacionPendiente" class="reserva-bloque__nota">
                La dirección exacta de esta estación aún no está confirmada.
              </p>
            </section>

            <section v-if="paso===4" class="reserva-bloque" aria-labelledby="reserva-titulo-duracion">
              <div class="reserva-bloque__cabecera">
                <span class="reserva-bloque__icono"><IconoTablet nombre="tiempo" :tamano="30" /></span>
                <div>
                  <h3 id="reserva-titulo-duracion">Duración</h3>
                  <p>Elige el tiempo disponible para este casillero.</p>
                </div>
              </div>
              <div class="reserva-duraciones" role="group" aria-label="Duración de la reserva">
                <button v-for="m in duraciones" :key="m" type="button"
                  class="reserva-duracion" :class="{ 'es-seleccionada': minutos===m }"
                  :aria-pressed="minutos===m" @click="minutos=m">
                  <strong>{{ nombreDuracion(m) }}</strong>
                  <small>{{ minutos===m?'Seleccionado':'Seleccionar' }}</small>
                </button>
                <p v-if="!duraciones.length" class="env-help">No hay duraciones disponibles para esta ubicación.</p>
              </div>
              <p class="reserva-bloque__nota">Las opciones se consultan desde la disponibilidad actual de SYNCHRO.</p>
            </section>
          </div>

          <section v-if="paso===5 && servicio==='Encomienda'" class="reserva-bloque reserva-bloque--destinatario" aria-labelledby="reserva-titulo-whatsapp">
            <div class="reserva-bloque__cabecera">
              <span class="reserva-bloque__icono"><IconoTablet nombre="whatsapp" :tamano="30" /></span>
              <div>
                <h3 id="reserva-titulo-whatsapp">WhatsApp del destinatario</h3>
                <p>Indica el número de la persona que recogerá la encomienda.</p>
              </div>
            </div>
            <label for="reserva-telefono" class="reserva-telefono__etiqueta">Número con código de país</label>
            <div class="reserva-telefono__control">
              <AppIcon nombre="telefono" :tamano="21" />
              <input id="reserva-telefono" v-model="telefono" type="tel" inputmode="tel"
                autocomplete="off" maxlength="16" placeholder="+504XXXXXXXX"
                aria-describedby="reserva-telefono-ayuda" required />
            </div>
            <p id="reserva-telefono-ayuda" class="reserva-bloque__nota">
              La autorización de retiro del destinatario se gestiona después de confirmar el depósito.
            </p>
          </section>

          <div v-if="false" class="reserva-final__resumen">
            <span class="reserva-final__resumen-titulo">Tu selección</span>
            <dl>
              <div><dt>Servicio</dt><dd>{{ servicio==='Personal'?'Pertenencias personales':'Encomienda' }}</dd></div>
              <div><dt>Casillero</dt><dd>{{ tipo }} · {{ tamano }}</dd></div>
              <div><dt>Tiempo</dt><dd>{{ minutos ? nombreDuracion(minutos) : 'Por seleccionar' }}</dd></div>
              <div><dt>Estación</dt><dd>{{ ubicacionNombre }}</dd></div>
            </dl>
          </div>

        </form>

      </template>
      <section v-if="paso===6 || paso===7" class="env-card reserva-revision">
        <div class="reserva-final__intro">
          <span class="reserva-final__etapa">{{ paso===6 ? 'REVISIÓN DE LA RESERVA' : 'CONFIRMACIÓN DEL PAGO' }}</span>
          <h2>{{ paso===6 ? 'Revisa tu reserva' : 'Selecciona cómo pagar' }}</h2>
          <p>{{ paso===6 ? 'Confirma los datos, la duración y el total antes de pasar al pago.' : 'Selecciona quién pagará y completa la solicitud de confirmación.' }}</p>
        </div>
        <dl class="reserva-revision__datos">
          <div><dt>Servicio</dt><dd>{{ servicio==='Personal'?'Pertenencias personales':'Encomienda' }}</dd></div>
          <div><dt>Casillero</dt><dd>{{ tipo }} · {{ tamano }}</dd></div>
          <div><dt>Estación</dt><dd>{{ ubicacionNombre }}</dd></div>
          <div><dt>Duración</dt><dd>{{ nombreDuracion(minutos) }}</dd></div>
          <div class="reserva-revision__importe"><dt>Total de la reserva</dt><dd>{{ importeTexto }}</dd></div>
          <div v-if="servicio==='Encomienda'"><dt>Destinatario</dt><dd>{{ normalizarTelefono(telefono) }}</dd></div>
        </dl>
        <p v-if="importeReserva===null" class="reserva-revision__tarifa-aviso">Esta selección aún no tiene una tarifa configurada. El administrador deberá establecer el importe antes de confirmar el cobro.</p>
        <p v-else class="reserva-revision__tarifa-aviso">Importe de la reserva: <strong>{{ importeTexto }}</strong>. La confirmación del pago queda sujeta a revisión administrativa.</p>
        <div v-if="paso===7" class="reserva-pago__seccion">
          <div class="reserva-final__intro">
            <h3>¿Cómo deseas realizar el pago?</h3>
            <p>Elige cómo deseas gestionar el pago de esta reserva.</p>
          </div>
          <div class="reserva-pagadores" role="group" aria-label="Quién realizará el pago">
            <button type="button" class="reserva-pagador"
              :class="{'es-seleccionada':pagador==='Remitente'}"
              :aria-pressed="pagador==='Remitente'" :disabled="enviando"
              @click="pagador='Remitente'">
              <IconoTablet nombre="pago" :tamano="32" />
              <strong>Pagar desde la web</strong>
              <small>Pago con tarjeta · sujeto a confirmación.</small>
            </button>
            <button type="button" class="reserva-pagador"
              :class="{'es-seleccionada':pagador==='Destinatario'}"
              :aria-pressed="pagador==='Destinatario'" :disabled="enviando"
              @click="pagador='Destinatario'">
              <IconoTablet nombre="encomienda" :tamano="32" />
              <strong>{{ servicio==='Encomienda' ? 'Pagará el destinatario en la tablet' : 'Pagar en la estación' }}</strong>
              <small>{{ servicio==='Encomienda' ? 'Después del depósito, antes del retiro.' : 'Gestionar el pago presencial con el procedimiento de la tablet.' }}</small>
            </button>
          </div>

          <section v-if="pagador==='Remitente'" class="reserva-prueba" aria-labelledby="reserva-prueba-titulo">
            <div class="reserva-pago__encabezado">
              <div class="reserva-final__intro">
                <span class="reserva-final__etapa">PAGO EN LÍNEA</span>
                <h3 id="reserva-prueba-titulo">Pago con tarjeta</h3>
                <p>Revisa los datos de pago y confirma tu solicitud.</p>
              </div>
              <span class="reserva-pago__seguridad"><AppIcon nombre="seguridad" :tamano="18" /> Conexión segura</span>
            </div>
            <div class="reserva-pago__contenido">
              <div class="reserva-pago__tarjeta" aria-label="Tarjeta habilitada para el recorrido de pago">
                <div class="reserva-pago__tarjeta-superior">
                  <span>SYNCHRO</span>
                  <span class="reserva-pago__chip" aria-hidden="true"></span>
                </div>
                <span class="reserva-pago__numero-visual">4242  ••••  ••••  4242</span>
                <div class="reserva-pago__tarjeta-inferior">
                  <span><small>TITULAR</small> CLIENTE SYNCHRO</span>
                  <span><small>VENCE</small> 12/30</span>
                </div>
              </div>
              <div class="reserva-pago__formulario">
                <div class="reserva-pago__datos-prueba"><span>Para continuar, utiliza los datos habilitados para esta operación.</span><button type="button" class="reserva-pago__aplicar" :disabled="enviando" @click="usarDatosPrueba">Usar datos de prueba</button></div>
                <div class="reserva-prueba__campos">
                  <label class="campo reserva-prueba__numero">
                    <span>Número de tarjeta</span>
                    <input :value="numeroTarjetaPrueba" @input="cambiarNumeroTarjeta(($event.target as HTMLInputElement).value)" type="text" inputmode="numeric" autocomplete="off"
                      maxlength="23" placeholder="4242 4242 4242 4242" :disabled="enviando" />
                    <small>Número habilitado: 4242 4242 4242 4242</small>
                  </label>
                  <label class="campo">
                    <span>Fecha de vencimiento</span>
                    <input :value="venceTarjetaPrueba" @input="cambiarVencimiento(($event.target as HTMLInputElement).value)" type="text" inputmode="numeric" autocomplete="off"
                      maxlength="5" placeholder="MM/AA" :disabled="enviando" />
                    <small>Usa 12/30</small>
                  </label>
                  <label class="campo">
                    <span>Código de seguridad</span>
                    <input v-model="codigoTarjetaPrueba" type="password" inputmode="numeric" autocomplete="off"
                      maxlength="3" placeholder="•••" :disabled="enviando" @input="pagoError=''" />
                    <small>Usa 123 · no es un PIN bancario.</small>
                  </label>
                </div>
                <p v-if="pagoError" class="reserva-pago__error" role="alert">{{ pagoError }}</p>
                <p class="reserva-pago__aviso">Utiliza únicamente los valores habilitados en esta pantalla; no ingreses una tarjeta real. Los datos no se almacenan ni se envían al servidor. No se efectuarán cargos bancarios.</p>
              </div>
            </div>
            <div class="reserva-pago__pie">
              <AppIcon nombre="check" :tamano="19" />
              <span>La solicitud quedará pendiente de confirmación administrativa.</span>
            </div>
          </section>
          <p v-else class="reserva-bloque__nota">
            {{ servicio==='Encomienda' ? 'El destinatario solicitará el pago en la tablet después del depósito. El administrador deberá confirmarlo antes de permitir el retiro.' : 'El pago presencial se realizará mediante el proceso habitual de la tablet, con comprobación administrativa cuando corresponda.' }}
          </p>
        </div>


      </section>
      <section v-if="paso===8 && reservaEnEspera!==null" class="env-card reserva-pendiente" aria-live="polite">
        <div class="reserva-pendiente__icono"><AppIcon nombre="reloj" :tamano="40" /></div>
        <span class="reserva-final__etapa">SOLICITUD ENVIADA</span>
        <h2>Esperando confirmación del pago</h2>
        <p>La reserva #{{ reservaEnEspera }} se registró correctamente. El pago será revisado por el administrador.</p>
        <p class="reserva-pendiente__total">Total solicitado: <strong>{{ importeTexto }}</strong></p>
        <p role="status">{{ textoEstadoPago }}</p>
        <p class="reserva-bloque__nota">Esta pantalla se actualiza automáticamente. No vuelvas a confirmar el pago ni crees otra reserva mientras está pendiente. No se ha realizado ningún cargo bancario.</p>
        <button type="button" class="boton boton-secundario" :disabled="consultandoPago" @click="consultarPago">Actualizar estado</button>
        <p class="reserva-bloque__nota">Al aprobarse el pago, se abrirá tu reserva para continuar con el PIN de depósito. El PIN será generado y entregado por el administrador mediante el procedimiento de WhatsApp autorizado.</p>
        <button type="button" class="boton boton-secundario" @click="router.push('/reservas/'+reservaEnEspera)">Consultar seguimiento de esta reserva</button>
      </section>
    </div>
    <footer v-if="!cargando && paso<=ultimoPaso" class="reserva-paso__navegacion">
      <button v-if="paso>0" type="button" class="boton boton-secundario" @click="regresar" :disabled="enviando">Regresar</button>
      <button v-if="paso<7" type="button" class="boton boton-primario"
        :disabled="enviando || actualizando || (paso===2 && !tamano) || (paso===3 && !ubicacion) || (paso===4 && !disponible)"
        @click="continuar">
        {{ paso===6 ? 'Continuar al pago' : paso===4 && servicio==='Personal' || paso===5 ? 'Revisar reserva' : 'Continuar' }}
        <IconoTablet nombre="continuar" :tamano="21" />
      </button>
      <SynButton v-else tipo="button" :cargando="enviando" :disabled="enviando" @click="crear">
        {{ pagador==='Remitente' ? 'Confirmar pago' : 'Confirmar reserva' }}
      </SynButton>
    </footer>
  </section>
</template>
<style scoped>
/* Un solo paso por pantalla: cabecera y acciones permanecen visibles. */
:global(.panel-contenido:has(.env-page--asistente)) {
  height: calc(100dvh - 78px);
  min-height: 0;
  overflow: hidden;
  box-sizing: border-box;
  padding: clamp(8px,1.4vh,18px) clamp(10px,2.2vw,28px);
}
:global(.contenido-limite:has(.env-page--asistente)) {height:100%;min-height:0;}
.env-page--asistente {height:100%;min-height:0;padding:0;display:flex;flex-direction:column;gap:clamp(7px,1.4vh,14px);overflow:hidden;}
.env-page--asistente .env-heading {flex:0 0 auto;}
.env-page--asistente .env-heading h1 {font-size:clamp(20px,3vw,32px);}
.env-page--asistente .env-heading p {font-size:12px;}
.reserva-pasos {flex:0 0 auto;}
.reserva-vista {flex:1 1 auto;min-height:0;overflow-y:auto;overscroll-behavior:contain;scrollbar-width:thin;display:flex;flex-direction:column;gap:12px;align-items:stretch;}
.reserva-vista > .env-card {margin:0;flex:1 0 auto;padding:clamp(12px,1.7vh,24px);display:flex;flex-direction:column;justify-content:center;gap:clamp(10px,1.7vh,20px);}
.reserva-vista .reserva-opciones {width:100%;grid-template-columns:repeat(3,minmax(0,1fr));gap:clamp(7px,1.5vw,14px);}
.reserva-vista .reserva-opciones:not(.reserva-tres) {grid-template-columns:repeat(2,minmax(0,1fr));}
.reserva-vista .reserva-final__grid {grid-template-columns:minmax(0,1fr);}
.reserva-vista .reserva-final__resumen {display:none;}
.env-page--asistente > .reserva-paso__navegacion {
  flex:0 0 auto;
  display:flex;
  justify-content:flex-end;
  align-items:center;
  gap:12px;
  box-sizing:border-box;
  width:calc(100% - 2 * clamp(16px,2.4vw,30px));
  margin-inline:clamp(16px,2.4vw,30px);
  padding:8px 0;
  background:var(--bg);
  border:0;
  box-shadow:none;
}
.reserva-paso__navegacion .boton,.reserva-paso__navegacion .syn-button {display:inline-flex;align-items:center;justify-content:center;gap:10px;min-height:49px;padding:10px 18px;max-width:100%;box-sizing:border-box;}
.env-page--asistente > .reserva-paso__navegacion > :first-child {margin-left:auto;}
.env-page--asistente > .reserva-paso__navegacion > :last-child {margin-left:0;}
@media(max-width:800px){
  :global(.panel-contenido:has(.env-page--asistente)){height:calc(100dvh - 70px);padding:8px 10px;}
  .env-page--asistente .env-heading p {display:none;}
  .env-page--asistente .reserva-pasos {padding:9px 12px;gap:6px;}
  .reserva-vista > .env-card {padding:12px;}
  .reserva-vista .reserva-opciones {grid-template-columns:repeat(3,minmax(0,1fr));gap:8px;}
}
@media(max-width:390px) {
  .reserva-vista .reserva-opciones {grid-template-columns:repeat(3,minmax(0,1fr));gap:5px;}
  .env-page--asistente > .reserva-paso__navegacion {width:calc(100% - 28px);margin-inline:14px;}
  .reserva-paso__navegacion .boton,.reserva-paso__navegacion .syn-button {width:auto;flex:1 1 0;margin-left:0;}
}
@media(max-height:640px) {.env-page--asistente .env-heading p {display:none;}.reserva-vista > .env-card {justify-content:flex-start;}}

.reserva-pasos {display:grid;gap:10px;padding:16px 18px;background:var(--surface-soft);border:1px solid var(--border);border-radius:16px;}
.reserva-pasos__texto {display:flex;justify-content:space-between;gap:12px;align-items:center;color:var(--text-strong);font-size:13px;}
.reserva-pasos__texto span {color:var(--text-muted);}
.reserva-pasos__barra {display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:7px;}
.reserva-pasos__barra span {height:6px;border-radius:99px;background:var(--border-strong);transition:background 180ms ease;}
.reserva-pasos__barra span.activo {background:var(--brand-accent);}

.reserva-pendiente {display:grid;justify-items:start;gap:16px;padding:clamp(22px,4vw,42px);}
.reserva-pendiente__icono {display:grid;place-items:center;width:72px;height:72px;border-radius:20px;color:var(--text-strong);background:var(--surface-accent);}
.reserva-pendiente__total {padding:16px 20px;background:var(--surface-accent);border:1px solid var(--border);border-radius:13px;}
.reserva-pendiente__total strong {font-size:21px;}

@media(prefers-reduced-motion:reduce){.reserva-pasos__barra span{transition:none}}
.reserva-opciones {
  --reserva-gap: 14px;
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--reserva-gap);
  align-items: stretch;
}
.reserva-tres { grid-template-columns: repeat(3, minmax(0, 1fr)); }
.reserva-sr-only {
  position: absolute;
  width: 1px; height: 1px;
  margin: -1px; padding: 0; overflow: hidden;
  clip-path: inset(50%); white-space: nowrap; border: 0;
}
@media (max-width: 600px) {
  .reserva-opciones { grid-template-columns: 1fr; }
  .reserva-tres { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  
}

/* Los detalles continúan las tarjetas de selección, sin controles nativos sueltos. */
.reserva-final {
  margin-top: 0;
  gap: 20px;
  padding: clamp(18px, 3.2vw, 30px);
}
.reserva-final__intro { display: grid; gap: 7px; }
.reserva-final__etapa {
  color: var(--accent-text);
  font-size: 11px;
  letter-spacing: .1em;
  font-weight: 800;
}
.reserva-final__intro h2 { font-size: clamp(21px, 2.4vw, 27px); }
.reserva-final__intro p { color: var(--text-muted); }
.reserva-final__grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; }
.reserva-bloque {
  min-width: 0;
  display: grid;
  align-content: start;
  gap: 16px;
  padding: clamp(16px, 2.4vw, 22px);
  background: var(--surface-soft);
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
}
.reserva-bloque__cabecera { display: flex; align-items: flex-start; gap: 13px; }
.reserva-bloque__icono {
  display: grid;
  place-items: center;
  width: 46px;
  height: 46px;
  flex: 0 0 46px;
  background: var(--surface-accent);
  border: 1px solid var(--border-strong);
  border-radius: 13px;
}
.reserva-bloque__cabecera > div { min-width: 0; display: grid; gap: 5px; }
.reserva-bloque__cabecera h3 { font-size: 17px; line-height: 1.25; }
.reserva-bloque__cabecera p { font-size: 12px; line-height: 1.55; color: var(--text-muted); }
.reserva-ubicaciones { display: grid; gap: 10px; }
.reserva-opcion-ubicacion {
  font: inherit;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  width: 100%;
  min-height: 82px;
  min-width: 0;
  padding: 14px 15px;
  text-align: left;
  color: var(--text-strong);
  background: var(--surface);
  border: 2px solid var(--border-strong);
  border-radius: 13px;
  cursor: pointer;
  transition: border-color 160ms ease, background-color 160ms ease;
}
.reserva-opcion-ubicacion__contenido { min-width: 0; display: grid; gap: 5px; }
.reserva-opcion-ubicacion strong { font-size: 14px; overflow-wrap: anywhere; }
.reserva-opcion-ubicacion small { color: var(--text-muted); font-size: 12px; line-height: 1.4; }
.reserva-opcion-ubicacion__marca {
  display: grid;
  place-items: center;
  width: 26px;
  height: 26px;
  flex: 0 0 26px;
  border-radius: 50%;
  background: var(--surface-soft);
  border: 1px solid var(--border-strong);
  color: var(--text-muted);
  opacity: .5;
}
.reserva-opcion-ubicacion.es-seleccionada {
  background: var(--surface-accent);
  border-color: var(--brand-accent);
}
.reserva-opcion-ubicacion.es-seleccionada .reserva-opcion-ubicacion__marca {
  background: var(--brand-accent);
  color: #01305c;
  border-color: var(--brand-accent);
  opacity: 1;
}
.reserva-bloque__nota { color: var(--text-muted); font-size: 12px !important; line-height: 1.6 !important; }
.reserva-duraciones { display: grid; grid-template-columns: repeat(auto-fit, minmax(90px, 1fr)); gap: 10px; align-content: start; }
.reserva-duracion {
  display: grid;
  align-content: center;
  justify-items: center;
  gap: 9px;
  min-width: 0;
  min-height: 82px;
  padding: 12px 7px;
  font: inherit;
  color: var(--text-strong);
  background: var(--surface);
  border: 2px solid var(--border-strong);
  border-radius: 13px;
  cursor: pointer;
  transition: border-color 160ms ease, background-color 160ms ease;
}
.reserva-duracion strong { font-size: clamp(14px, 1.5vw, 17px); white-space: nowrap; }
.reserva-duracion small { color: var(--text-muted); font-size: 11px; }
.reserva-duracion.es-seleccionada { background: var(--surface-accent); border-color: var(--brand-accent); }
.reserva-final button:focus-visible, .reserva-telefono__control:focus-within {
  outline: 3px solid var(--ring);
  outline-offset: 3px;
}
.reserva-bloque--destinatario { gap: 12px; }
.reserva-telefono__etiqueta { color: var(--text-strong); font-size: 13px; font-weight: 750; }
.reserva-telefono__control {
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
  min-width: 0;
  padding: 0 16px;
  color: var(--text-muted);
  background: var(--surface);
  border: 1px solid var(--border-strong);
  border-radius: 13px;
}
.reserva-telefono__control input {
  flex: 1 1 0;
  min-width: 0;
  min-height: 54px;
  width: 100%;
  font: inherit;
  font-size: 15px;
  color: var(--text-strong);
  background: transparent;
  border: 0;
  outline: 0;
}
.reserva-telefono__control input::placeholder { color: var(--text-subtle); opacity: 1; }
.reserva-final__resumen {
  display: grid;
  gap: 14px;
  padding: 19px 20px;
  background: var(--surface-accent);
  border: 1px solid var(--border);
  border-radius: 14px;
}
.reserva-final__resumen-titulo { font-size: 14px; font-weight: 800; color: var(--text-strong); }
.reserva-final__resumen dl {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 14px;
  margin: 0;
}
.reserva-final__resumen dl > div { min-width: 0; display: grid; align-content: start; gap: 6px; }
.reserva-final__resumen dt { font-size: 11px; font-weight: 700; color: var(--text-muted); }
.reserva-final__resumen dd { margin: 0; font-size: 13px; font-weight: 750; color: var(--text-strong); overflow-wrap: anywhere; }
.reserva-final__acciones { display: flex; align-items: stretch; justify-content: space-between; gap: 14px; }
.reserva-final__acciones .boton {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  min-width: 0;
  min-height: 54px;
  padding: 12px 18px;
  font: inherit;
  font-size: 14px;
  font-weight: 800;
  line-height: 1.35;
  cursor: pointer;
}
.reserva-final__volver { flex: 0 1 auto; }
.reserva-final__continuar { flex: 1 1 280px; max-width: 460px; }
.reserva-final__continuar :deep(.icono-tablet) { filter:none; }
.reserva-final__acciones .boton:disabled { opacity: .55; cursor: not-allowed; }
@media (max-width: 760px) {
  .reserva-final__grid { grid-template-columns: minmax(0, 1fr); }
  .reserva-final__resumen dl { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}
@media (max-width: 470px) {
  .reserva-final__acciones { flex-direction: column-reverse; }
  .reserva-final__acciones .boton {
    width: 100%;
    max-width: none;
    min-height: 54px;
    height: auto;
    flex: 0 0 auto; /* Evita convertir el ancho de referencia de 280px en altura móvil. */
  }
  .reserva-final__resumen { padding: 16px; }
  .reserva-final__resumen dl { gap: 12px; }
  .reserva-bloque { padding: 16px 13px; }
}
@media (prefers-reduced-motion: reduce) {
  .reserva-final button { transition: none; }
}

.reserva-revision { gap: 24px; }
.reserva-revision__datos {
  display: grid;
  grid-template-columns: repeat(2,minmax(0,1fr));
  gap: 16px;
  margin: 0;
  padding: 20px;
  background: var(--surface-soft);
  border: 1px solid var(--border);
  border-radius: 15px;
}
.reserva-revision__datos > div { display:grid; gap:6px; min-width:0; }
.reserva-revision__importe {grid-column:1/-1;padding:16px 18px;border:1px solid var(--border-strong);background:var(--surface-accent);border-radius:11px;}
.reserva-revision__datos .reserva-revision__importe dd {font-size:clamp(20px,3vw,28px);color:var(--text-strong);overflow-wrap:normal;}
.reserva-revision__tarifa-aviso {font-size:13px;line-height:1.6;color:var(--text-muted);}
.reserva-revision__datos dt { font-size:12px; color:var(--text-muted); font-weight:700; }
.reserva-revision__datos dd { margin:0; color:var(--text-strong); font-weight:800; overflow-wrap:anywhere; }
.reserva-pagadores { display:grid; grid-template-columns:repeat(2,minmax(0,1fr));gap:14px; }
.reserva-pagador {
  display:grid; justify-items:start; align-content:start; gap:12px;
  text-align:left; padding:18px; min-height:158px;
  font:inherit; color:var(--text-strong); background:var(--surface-soft);
  border:2px solid var(--border-strong); border-radius:15px; cursor:pointer;
  transition: border-color 150ms ease, background 150ms ease;
}
.reserva-pagador strong { font-size:15px; line-height:1.45; }
.reserva-pagador small { color:var(--text-muted);font-size:12px;line-height:1.55; }
.reserva-pagador.es-seleccionada { border-color:var(--brand-accent);background:var(--surface-accent); }
.reserva-pagador:disabled { opacity:.6;cursor:wait; }
.reserva-prueba {
  display:grid;gap:16px;padding:20px;
  background:var(--surface-soft); border:1px solid var(--border);border-radius:15px;
}
.reserva-prueba__campos { display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px; }
.reserva-pago__seccion { display:grid; gap:20px; }
.reserva-pago__encabezado { display:flex;justify-content:space-between;align-items:flex-start;gap:18px; }
.reserva-pago__seguridad { display:inline-flex;align-items:center;gap:7px;white-space:nowrap;color:var(--accent-text);font-size:12px;font-weight:750;padding:9px 12px;background:var(--surface-accent);border-radius:99px; }
.reserva-pago__contenido { display:grid;grid-template-columns:minmax(240px,.85fr) minmax(0,1.15fr);align-items:start;gap:22px; }
.reserva-pago__tarjeta { min-height:200px;aspect-ratio:1.6;display:flex;flex-direction:column;justify-content:space-between;gap:20px;padding:24px;color:#fff;background:linear-gradient(128deg,#01305c 0%,#07517a 65%,#008d9d 100%);border:1px solid rgba(255,255,255,.24);border-radius:19px;box-shadow:0 16px 35px rgba(1,48,92,.16); }
.reserva-pago__tarjeta-superior,.reserva-pago__tarjeta-inferior { display:flex;align-items:center;justify-content:space-between;gap:15px; }
.reserva-pago__tarjeta-superior { font-weight:850;letter-spacing:.13em;font-size:15px; }
.reserva-pago__chip { width:36px;height:27px;flex:0 0 36px;border:1px solid rgba(255,255,255,.65);border-radius:7px;background:linear-gradient(140deg,#9ed1de,#f3fafb 55%,#8ac4d2); }
.reserva-pago__numero-visual { font-size:clamp(17px,1.8vw,23px);font-weight:700;letter-spacing:.08em;white-space:nowrap; }
.reserva-pago__tarjeta-inferior {font-size:12px;font-weight:700;letter-spacing:.06em;}
.reserva-pago__tarjeta-inferior span {display:grid;gap:5px;}
.reserva-pago__tarjeta-inferior small {font-size:9px;opacity:.75;}
.reserva-pago__formulario { min-width:0;display:grid;gap:14px; }
.reserva-pago__datos-prueba {display:flex;align-items:center;justify-content:space-between;gap:12px;flex-wrap:wrap;padding:11px 13px;border:1px solid var(--border);border-radius:12px;background:var(--surface-accent);font-size:12px;color:var(--text-strong);}
.reserva-pago__aplicar {font:inherit;font-size:12px;font-weight:800;color:var(--text-strong);border:1px solid var(--border-strong);border-radius:10px;background:var(--surface);padding:10px 12px;cursor:pointer;}
.reserva-pago__aplicar:hover:not(:disabled){border-color:var(--brand-accent);}
.reserva-pago__error {font-size:12px;line-height:1.5;color:var(--text-strong);border-left:3px solid #c15d37;padding:10px 12px;background:var(--surface);}
.reserva-pago__aviso {font-size:11px;line-height:1.6;color:var(--text-muted);margin:0;}
.reserva-pago__pie {display:flex;gap:10px;align-items:center;padding:13px 15px;color:var(--text-strong);font-size:12px;font-weight:700;background:var(--surface-accent);border:1px solid var(--border);border-radius:12px;}
.reserva-pago__pie :deep(svg) {flex:0 0 auto;}
@media(max-width:850px){.reserva-pago__contenido{grid-template-columns:minmax(0,1fr);}.reserva-pago__tarjeta{max-width:420px;width:100%;}}
@media(max-width:470px){.reserva-pago__encabezado{flex-direction:column;}.reserva-pago__tarjeta{padding:19px;min-height:175px;}.reserva-pago__numero-visual{font-size:17px;}.reserva-prueba__campos{grid-template-columns:minmax(0,1fr);}.reserva-prueba__numero{grid-column:auto;}}

.reserva-prueba__numero { grid-column:1/-1; }
.reserva-revision__acciones { display:flex; gap:14px; justify-content:flex-end;flex-wrap:wrap; }
.reserva-revision__acciones > * { min-height:54px; flex:1 1 200px;max-width:400px; }
@media(max-width:600px){
  .reserva-pagadores { grid-template-columns:1fr; }
  .reserva-revision__datos {grid-template-columns:repeat(2,minmax(0,1fr));padding:16px;}
  .reserva-revision__acciones { flex-direction:column-reverse; }
  .reserva-revision__acciones > * { width:100%;max-width:none;flex:0 0 auto; }
}
@media(prefers-reduced-motion:reduce){.reserva-pagador{transition:none;}}

/* Rejilla tipo tablet, con dos columnas y uso completo del espacio. */
.reserva-vista .reserva-opciones,
.reserva-vista .reserva-opciones.reserva-tres {
  display:grid;
  grid-template-columns:repeat(2,minmax(0,1fr));
  grid-auto-rows:auto;
  gap:clamp(8px,1.4vw,14px);
  width:100%;
  flex:0 0 auto;
  align-content:start;
}
/* La cuadrícula 2×2 reparte el alto real del paso sin crear scroll en escritorio. */
.reserva-vista > .env-card:has(.reserva-opciones > :nth-child(3)) {
  flex:1 1 auto;
  min-height:0;
  overflow:hidden;
}
.reserva-vista .reserva-opciones:has(> :nth-child(3)) {
  flex:1 1 auto;
  min-height:0;
  grid-template-rows:repeat(2,minmax(0,1fr));
  align-content:stretch;
}
.reserva-vista .reserva-opciones:has(> :nth-child(3)) :deep(.seleccion-reserva) {
  height:100%;
  min-height:0;
  grid-template-rows:minmax(28px,auto) minmax(20px,auto) minmax(0,1fr) auto auto;
  padding:clamp(5px,1vh,12px);
  gap:clamp(1px,.6vh,5px);
}
.reserva-vista .reserva-opciones:has(> :nth-child(3)) :deep(.seleccion-reserva__icono) {
  width:40px;
  height:34px;
}
.reserva-vista .reserva-opciones:has(> :nth-child(3)) :deep(.icono-tablet) {
  max-width:38px;
  max-height:38px;
}
/* Tres opciones ocupan tres celdas: la cuarta queda libre para conservar la proporción. */
.reserva-vista .reserva-opciones > :last-child:nth-child(odd):not(:first-child) {
  grid-column:auto;
}
/* Una sola opción se centra, sin estirarse a todo el ancho de la pantalla. */
.reserva-vista .reserva-opciones > :only-child {
  grid-column:1/-1;
  justify-self:center;
  width:min(100%,clamp(190px,45vw,330px));
  max-width:100%;
}
 .reserva-vista .reserva-opciones :deep(.seleccion-reserva) {
  height:clamp(185px,28dvh,225px);
  min-height:0;
  display:grid;
  grid-template-rows:43px minmax(28px,auto) minmax(27px,1fr) auto auto;
  align-content:center;
  gap:clamp(2px,.8vh,7px);
  padding:clamp(9px,1.5vh,17px);
}
.reserva-vista .reserva-opciones :deep(.seleccion-reserva__icono) {
  width:50px;height:46px;margin:auto;
}
.reserva-vista .reserva-opciones :deep(.seleccion-reserva__titulo) {
  font-size:clamp(12px,2.8vw,17px);
  line-height:1.25;
}
.reserva-vista .reserva-opciones :deep(.seleccion-reserva__descripcion) {
  font-size:clamp(10px,1.8vw,12px);
  line-height:1.3;
}
.reserva-vista .reserva-opciones :deep(.seleccion-reserva__disponibles),
.reserva-vista .reserva-opciones :deep(.seleccion-reserva__seleccion) {
  font-size:11px;white-space:normal;
}
.reserva-vista > .env-card:has(.reserva-opciones) {
  flex:0 0 auto;
  justify-content:flex-start;
  align-content:start;
  min-height:0;
}
.reserva-vista > .env-card:has(.reserva-opciones > :nth-child(3)) {
  flex:1 1 auto;
  justify-content:flex-start;
  min-height:0;
  overflow:hidden;
}
.reserva-vista > .env-card:has(.reserva-opciones) > h2 {
  flex:0 0 auto;
  font-size:clamp(18px,2.2vw,23px);
  line-height:1.2;
}
.reserva-pasos {
  display:grid;
  gap:6px;
  padding:4px 0 7px;
  background:transparent;
  border:0;
  border-radius:0;
}
.reserva-pasos__texto {
  display:flex;
  justify-content:flex-start;
  align-items:center;
  gap:9px;
  font-size:12px;
  line-height:1.2;
}
.reserva-pasos__contador {
  padding:3px 7px;
  border-radius:7px;
  background:var(--surface-accent);
  font-size:11px;
  font-weight:800;
  font-variant-numeric:tabular-nums;
}
.reserva-pasos__barra {
  display:block;
  height:3px;
  overflow:hidden;
  border-radius:99px;
  background:var(--border);
}
.reserva-pasos__barra span {
  display:block;
  height:100%;
  background:var(--brand-accent);
  border-radius:inherit;
  transition:width 180ms ease;
}
@media(max-width:800px) {
  .env-page--asistente .reserva-pasos {padding:2px 0 5px;gap:5px;}
  .reserva-vista .reserva-opciones,
  .reserva-vista .reserva-opciones.reserva-tres {
    grid-template-columns:repeat(2,minmax(0,1fr));
    gap:8px;
  }
  .reserva-vista .reserva-opciones :deep(.seleccion-reserva) {height:clamp(185px,28dvh,220px);min-height:0;padding:8px 9px;}
  .reserva-vista .reserva-opciones:has(> :nth-child(3)) :deep(.seleccion-reserva) {
    height:100%;
    min-height:0;
    padding:6px;
  }
  .reserva-vista .reserva-opciones :deep(.seleccion-reserva__icono) {width:42px;height:39px;}
  .reserva-vista .reserva-opciones :deep(.icono-tablet) {max-width:43px;max-height:43px;}
}
@media(max-height:580px) {
  .reserva-vista .reserva-opciones :deep(.seleccion-reserva) {height:clamp(140px,27dvh,190px);min-height:0;}
  .reserva-vista .reserva-opciones:has(> :nth-child(3)) :deep(.seleccion-reserva) {height:100%;min-height:0;}
  .reserva-vista > .env-card:has(.reserva-opciones) {gap:6px;padding:9px;}
  .reserva-vista .reserva-opciones:has(> :nth-child(3)) {gap:6px;}

}
@media(prefers-reduced-motion:reduce) {
  .reserva-pasos__barra span {transition:none;}
}

/* Botonera y pago: mismos tonos finales de la Tablet, con SVG originales sin filtros. */
.env-page--asistente .boton-primario,
.env-page--asistente :deep(.syn-button:not([data-variante="secundario"])) {
  background:#f4feff;
  color:#01305c;
  border:2px solid #00b9c7;
  border-radius:15px;
  box-shadow:0 4px 0 #00a5b2,0 7px 16px rgb(1 48 92 / 10%);
  font-weight:800;
}
.env-page--asistente .boton-secundario,
.env-page--asistente :deep(.syn-button[data-variante="secundario"]) {
  background:#e4f2f6;
  color:#01305c;
  border:2px solid #86bdcb;
  border-radius:14px;
  box-shadow:0 4px 0 rgb(1 48 92 / 14%),0 5px 12px rgb(1 48 92 / 8%);
  font-weight:750;
}
.env-page--asistente .boton-primario:hover:not(:disabled),
.env-page--asistente :deep(.syn-button:not([data-variante="secundario"]):hover:not(:disabled)) {
  background:#ddf7f9;
}
.env-page--asistente .boton-secundario:hover:not(:disabled) {background:#d4edf4;}
.env-page--asistente .boton-primario:disabled,
.env-page--asistente :deep(.syn-button:not([data-variante="secundario"]):disabled) {
  background:#edf3f5;
  color:#647784;
  border-color:#b5cad3;
  box-shadow:none;
  opacity:1;
}
.env-page--asistente :deep(.icono-tablet),
.env-page--asistente .boton-primario :deep(.icono-tablet) {
  filter:none;
}
.env-page--asistente .reserva-pagador {
  background:#ffffff;
  border-color:#dfeaf0;
  color:#01305c;
}
.env-page--asistente .reserva-pagador small {color:#5b7282;}
.env-page--asistente .reserva-pagador.es-seleccionada {
  background:#e8f9fa;
  border-color:#00b9c7;
}
:global(:root[data-theme="oscuro"]) .env-page--asistente .boton-primario,
:global(:root[data-theme="oscuro"]) .env-page--asistente :deep(.syn-button:not([data-variante="secundario"])) {
  background:#123d5b;
  color:#ffffff;
  border-color:#5ec6e8;
  box-shadow:0 4px 0 #087d93,0 7px 16px rgb(0 0 0 / 22%);
}
:global(:root[data-theme="oscuro"]) .env-page--asistente .boton-primario:hover:not(:disabled),
:global(:root[data-theme="oscuro"]) .env-page--asistente :deep(.syn-button:not([data-variante="secundario"]):hover:not(:disabled)) {
  background:#205674;
}
:global(:root[data-theme="oscuro"]) .env-page--asistente .boton-secundario,
:global(:root[data-theme="oscuro"]) .env-page--asistente :deep(.syn-button[data-variante="secundario"]) {
  background:#204766;
  color:#ffffff;
  border-color:#5ec6e8;
  box-shadow:0 4px 0 rgb(0 0 0 / 35%),0 5px 12px rgb(0 0 0 / 15%);
}
:global(:root[data-theme="oscuro"]) .env-page--asistente .boton-secundario:hover:not(:disabled) {background:#285b7a;}
:global(:root[data-theme="oscuro"]) .env-page--asistente .boton-primario:disabled,
:global(:root[data-theme="oscuro"]) .env-page--asistente :deep(.syn-button:not([data-variante="secundario"]):disabled) {
  background:#254054;
  color:#c0d0da;
  border-color:#426984;
  box-shadow:none;
}
:global(:root[data-theme="oscuro"]) .env-page--asistente .reserva-pagador {
  background:#103956;
  border-color:#315573;
  color:#ffffff;
}
:global(:root[data-theme="oscuro"]) .env-page--asistente .reserva-pagador small {color:#c6dfea;}
:global(:root[data-theme="oscuro"]) .env-page--asistente .reserva-pagador.es-seleccionada {
  background:#174b65;
  border-color:#00b9c7;
}

</style>
