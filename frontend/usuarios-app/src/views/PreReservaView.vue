<script setup lang="ts">
import { computed,onMounted,ref,watch } from 'vue';
import { useRouter,useRoute } from 'vue-router';
import { encomiendasApi } from '../services/encomiendasApi.js';
import { normalizarTelefono,TAMANOS_ENCOMIENDA } from '../services/validaciones.js';
import SeleccionReservaCard from '../components/ui/SeleccionReservaCard.vue';
import SynButton from '../components/ui/SynButton.vue';
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
const ubicacionNombre=computed(()=>ubicaciones.value.find(([id])=>id===ubicacion.value)?.[1]);
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
        <section v-if="porTipo.length" class="env-card"><h2>Tamaño</h2><div class="reserva-opciones reserva-tres">
          <SeleccionReservaCard v-for="t in TAMANOS_ENCOMIENDA" :key="t.valor" :titulo="t.nombre" :descripcion="t.detalle" :icono-tablet="t.valor" :seleccionado="tamano===t.valor" :disponibles="cantidadPorTamano(t.valor)" @seleccionar="tamano=t.valor" />
        </div></section>
        <form v-if="tamano" class="env-card formulario" @submit.prevent="revisar">
          <h2>Ubicación y duración</h2>
          <label class="campo"><span>Ubicación</span><select v-model="ubicacion" required><option value="" disabled>Selecciona una ubicación</option><option v-for="[id,nombre] in ubicaciones" :key="id" :value="id">{{ nombre }}</option></select></label>
          <p v-if="ubicacionPendiente" class="env-help">La ubicación de esta estación está pendiente de confirmación. Comprueba la dirección antes de continuar.</p>
          <label class="campo"><span>Duración</span><select v-model="minutos" required><option :value="0" disabled>Selecciona una duración</option><option v-for="m in duraciones" :key="m" :value="m">{{ m }} minutos</option></select></label>
          <label v-if="servicio==='Encomienda'" class="campo"><span>WhatsApp del destinatario</span><input v-model="telefono" type="tel" inputmode="tel" maxlength="16" placeholder="+504XXXXXXXX" required /><small>La autorización de retiro del destinatario se habilita después de confirmar el depósito.</small></label>
          <button class="boton boton-primario" :disabled="!disponible">Revisar reserva</button>
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
</style>
