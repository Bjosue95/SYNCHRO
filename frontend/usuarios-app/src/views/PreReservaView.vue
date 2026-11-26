<script setup lang="ts">
import { computed,onMounted,ref,watch } from 'vue';
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
const tipos=[{id:'Normal',nombre:'Normal'},{id:'Refrigerado',nombre:'Refrigerado'},{id:'Caliente',nombre:'Caliente'}];
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
const nombreUbicacion=(nombre:string)=>ubicacionPendiente.value?'Estación SYNCHRO':nombre;
const ubicacionNombre=computed(()=>{
  const nombre=ubicaciones.value.find(([id])=>id===ubicacion.value)?.[1];
  if(!nombre)return 'Por seleccionar';
  return ubicacionPendiente.value?'Estación SYNCHRO · Dirección pendiente':nombre;
});
const nombreDuracion=(valor:number)=>valor%60===0
  ? `${valor/60} ${valor===60?'hora':'horas'}`
  : `${valor} min`;
function volverATamanos(){
  const reducir=window.matchMedia?.('(prefers-reduced-motion: reduce)')?.matches;
  document.getElementById('reserva-seleccion-tamano')
    ?.scrollIntoView({behavior:reducir?'auto':'smooth',block:'start'});
}
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
    if(!disponible.value)revisando.value=false;
  } catch(e) {
    if(actual!==versionCatalogo)return;
    opciones.value=[];
    ubicacionPendiente.value=false;
    revisando.value=false;
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
  revisando.value=false;
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
}
async function crear(){
  if(enviando.value)return;
  if(!disponible.value || (servicio.value==='Encomienda'&&!normalizarTelefono(telefono.value))){
    revisando.value=false;
    error.value='Verifica la selección antes de crear tu reserva.';
    return;
  }
  enviando.value=true;
  error.value='';
  try {
    const d=await encomiendasApi.reservar({
      servicio:servicio.value,
      tipoCasillero:tipo.value,
      tamanoSolicitado:tamano.value,
      ubicacionId:ubicacion.value,
      minutos:minutos.value,
      telefonoDestinatario:servicio.value==='Encomienda'?normalizarTelefono(telefono.value):null
    });
    if(!Number.isSafeInteger(Number(d?.id))||Number(d.id)<=0)
      throw new Error('La reserva fue recibida, pero el servidor no devolvió una referencia válida. Consulta Mis reservas antes de crear otra.');
    await router.replace('/reservas/'+d.id);
  } catch(e) {
    error.value=(e as Error).message || 'No se pudo completar la reserva.';
    // En caso de error de conexión no se genera otra reserva automáticamente.
    revisando.value=false;
  } finally { enviando.value=false; }
}
onMounted(cargar);
</script>
<template>
  <section class="env-page">
    <header class="env-heading"><div><h1>Reservar casillero</h1><p>Elige qué vas a guardar y encuentra un casillero disponible.</p></div></header>
    <p v-if="error" class="alerta" role="alert">{{ error }}</p><p v-if="cargando" role="status">Consultando disponibilidad…</p>
    <span v-if="actualizando" class="reserva-sr-only" role="status">Actualizando disponibilidad</span>
    <template v-if="!cargando">
      <template v-if="!revisando">
        <section class="env-card"><h2>¿Qué vas a guardar?</h2><div class="reserva-opciones">
          <SeleccionReservaCard titulo="Pertenencias personales" descripcion="Guardarlas y retirarlas con tus PIN" icono-tablet="guardar" :seleccionado="servicio==='Personal'" @seleccionar="servicio='Personal'" />
          <SeleccionReservaCard titulo="Una encomienda" descripcion="El destinatario retira con su PIN" icono-tablet="encomienda" :seleccionado="servicio==='Encomienda'" @seleccionar="servicio='Encomienda'" />
        </div></section>
        <section class="env-card"><h2>Tipo de casillero</h2><div class="reserva-opciones reserva-tres">
          <SeleccionReservaCard v-for="t in tipos" :key="t.id" :titulo="t.nombre"
            :icono-tablet="t.id" :seleccionado="tipo===t.id"
            :descripcion="servicio==='Personal' && t.id!=='Normal' ? 'Exclusivo para encomiendas' : undefined"
            :disponibles="servicio==='Personal' && t.id!=='Normal' ? undefined : cantidadPorTipo(t.id)"
            :disabled="servicio==='Personal' && t.id!=='Normal'"
            @seleccionar="tipo=t.id" />
        </div><p v-if="!opciones.length">No hay casilleros compatibles disponibles. <button class="boton boton-secundario" @click="cargar(true)">Actualizar disponibilidad</button></p></section>
        <section v-if="porTipo.length" id="reserva-seleccion-tamano" class="env-card"><h2>Tamaño</h2><div class="reserva-opciones reserva-tres">
          <SeleccionReservaCard v-for="t in TAMANOS_ENCOMIENDA" :key="t.valor" :titulo="t.nombre" :descripcion="t.detalle" :icono-tablet="t.valor" :seleccionado="tamano===t.valor" :disponibles="cantidadPorTamano(t.valor)" @seleccionar="tamano=t.valor" />
        </div></section>
        <form v-if="tamano" class="env-card formulario reserva-final" @submit.prevent="revisar">
          <div class="reserva-final__intro">
            <span class="reserva-final__etapa">DETALLES DE LA RESERVA</span>
            <h2>Ubicación, tiempo y entrega</h2>
            <p>Completa los últimos datos para revisar tu selección.</p>
          </div>

          <div class="reserva-final__grid">
            <section class="reserva-bloque" aria-labelledby="reserva-titulo-ubicacion">
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

            <section class="reserva-bloque" aria-labelledby="reserva-titulo-duracion">
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

          <section v-if="servicio==='Encomienda'" class="reserva-bloque reserva-bloque--destinatario" aria-labelledby="reserva-titulo-whatsapp">
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

          <div class="reserva-final__resumen">
            <span class="reserva-final__resumen-titulo">Tu selección</span>
            <dl>
              <div><dt>Servicio</dt><dd>{{ servicio==='Personal'?'Pertenencias personales':'Encomienda' }}</dd></div>
              <div><dt>Casillero</dt><dd>{{ tipo }} · {{ tamano }}</dd></div>
              <div><dt>Tiempo</dt><dd>{{ minutos ? nombreDuracion(minutos) : 'Por seleccionar' }}</dd></div>
              <div><dt>Estación</dt><dd>{{ ubicacionNombre }}</dd></div>
            </dl>
          </div>
          <div class="reserva-final__acciones">
            <button type="button" class="boton boton-secundario reserva-final__volver" @click="volverATamanos">
              <IconoTablet nombre="volver" :tamano="21" />
              Volver a tamaños
            </button>
            <button type="submit" class="boton boton-primario reserva-final__continuar" :disabled="!disponible || actualizando">
              Revisar reserva
              <IconoTablet nombre="continuar" :tamano="21" />
            </button>
          </div>
        </form>
      </template>
      <section v-else class="env-card"><h2>Tu reserva</h2><dl class="env-facts"><div><dt>Servicio</dt><dd>{{ servicio==='Personal'?'Pertenencias personales':'Encomienda' }}</dd></div><div><dt>Casillero</dt><dd>{{ tipo }} · {{ tamano }}</dd></div><div><dt>Ubicación</dt><dd>{{ ubicacionNombre }}</dd></div><div><dt>Duración</dt><dd>{{ minutos }} minutos</dd></div><div v-if="servicio==='Encomienda'"><dt>Destinatario</dt><dd>{{ normalizarTelefono(telefono) }}</dd></div></dl>
        <p>El administrador comprobará tu autorización de WhatsApp y el pago antes de entregarte el PIN de depósito.</p>
        <p class="env-help" v-if="servicio==='Encomienda'">Después de confirmar el depósito, se podrá gestionar la autorización del destinatario y entregarle un PIN diferente para el retiro.</p>
        <p class="env-help" v-else>Recibirás un PIN de depósito y otro de retiro. Consulta el estado de cada operación desde Mis reservas.</p>
        <SynButton tipo="button" :cargando="enviando" @click="crear">Confirmar reserva</SynButton><button class="boton boton-secundario" :disabled="enviando" @click="revisando=false">Cambiar selección</button>
      </section>
    </template>
  </section>
</template>
<style scoped>
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
  .reserva-tres > :last-child:nth-child(odd) {
    grid-column: 1 / -1;
    justify-self: center;
    width: calc((100% - var(--reserva-gap)) / 2);
  }
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
.reserva-final__acciones .boton { display: inline-flex; align-items: center; gap: 12px; min-height: 54px; padding-inline: 22px; font: inherit; font-size: 14px; font-weight: 800; cursor: pointer; }
.reserva-final__volver { flex: 0 1 auto; }
.reserva-final__continuar { flex: 1 1 280px; max-width: 460px; }
.reserva-final__continuar :deep(.icono-tablet) { filter: var(--action-icon-filter); }
.reserva-final__acciones .boton:disabled { opacity: .55; cursor: not-allowed; }
@media (max-width: 760px) {
  .reserva-final__grid { grid-template-columns: minmax(0, 1fr); }
  .reserva-final__resumen dl { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}
@media (max-width: 470px) {
  .reserva-final__acciones { flex-direction: column-reverse; }
  .reserva-final__acciones .boton { width: 100%; max-width: none; }
  .reserva-final__resumen { padding: 16px; }
  .reserva-final__resumen dl { gap: 12px; }
  .reserva-bloque { padding: 16px 13px; }
}
@media (prefers-reduced-motion: reduce) {
  .reserva-final button { transition: none; }
}
</style>
