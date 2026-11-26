<script setup lang="ts">
import { onMounted,reactive,ref } from 'vue';
import { RouterLink } from 'vue-router';
import { encomiendasApi } from '../services/encomiendasApi.js';
import { fechaHonduras,nombreEstadoReserva } from '../services/formatos.js';
import AppIcon from '../components/ui/AppIcon.vue';
const items=ref<any[]>([]),cargando=ref(true),error=ref(''),pagina=ref(1),total=ref(0),totalPaginas=ref(0);
const filtros=reactive({estado:'',desde:'',hasta:'',numero:''});
let numeroConsulta=0;
async function cargar(reiniciar=false){
  if(reiniciar)pagina.value=1;
  const actual=++numeroConsulta;
  cargando.value=true;
  error.value='';
  if(filtros.desde && filtros.hasta && filtros.desde>filtros.hasta){
    error.value='La fecha inicial debe ser anterior o igual a la fecha final.';
    cargando.value=false;
    return;
  }
  try {
    const datos=await encomiendasApi.listar({...filtros,pagina:pagina.value,tamanoPagina:12});
    if(actual!==numeroConsulta)return;
    if(!Array.isArray(datos?.encomiendas) || !Number.isInteger(datos?.total))
      throw new Error('No fue posible verificar la lista de reservas.');
    items.value=datos.encomiendas;
    total.value=datos.total;
    totalPaginas.value=Math.max(1,Number(datos.totalPaginas)||1);
  } catch(e) {
    if(actual!==numeroConsulta)return;
    items.value=[];
    total.value=0;
    totalPaginas.value=1;
    error.value=(e as Error).message || 'No fue posible consultar tus reservas.';
  } finally {
    if(actual===numeroConsulta)cargando.value=false;
  }
}
async function cambiarPagina(cambio:number){
  if(cargando.value)return;
  const nueva=pagina.value+cambio;
  if(nueva<1 || nueva>totalPaginas.value)return;
  pagina.value=nueva;
  await cargar();
}
function cancelable(e:any){return !e.depositoUtc&&['Preparacion','ListaParaDeposito','Vencida','PendienteConsentimiento','Pendiente'].includes(e.estado);}
function servicio(e:any){return e.reserva?.servicio==='Personal'?'Pertenencias personales':'Encomienda';}
function estadoVisible(e:any){
  if(e.retiradaConfirmada)return e.reserva?.servicio==='Personal'?'Retiro confirmado':'Entrega confirmada';
  if(e.estado==='Retirada')return 'Retiro pendiente de confirmar';
  return nombreEstadoReserva(e.estado);
}
function icono(e:any){return e.reserva?.tipoCasillero==='Refrigerado'?'frio':e.reserva?.tipoCasillero==='Caliente'?'calor':'casillero';}
onMounted(()=>{void cargar();});
</script>
<template>
  <section class="env-page">
    <header class="env-heading"><div><h1>Mis reservas</h1><p>Consulta las reservas, los depósitos y los retiros confirmados por SYNCHRO.</p></div><div class="env-actions"><button type="button" class="boton boton-secundario" :disabled="cargando" @click="cargar()">Actualizar</button><RouterLink to="/reservar" class="boton boton-primario">Reservar casillero</RouterLink></div></header>
    <p v-if="error" class="alerta" role="alert">{{ error }} <button class="boton boton-secundario" @click="cargar()">Reintentar</button></p>
    <form class="env-card filtros-reservas" @submit.prevent="cargar(true)">
      <label class="campo"><span>Estado</span><select v-model="filtros.estado" aria-label="Estado"><option value="">Todos</option><option value="Preparacion">En preparación</option><option value="ListaParaDeposito">Lista para depósito</option><option value="Depositada">Depositada</option><option value="Retirada">Retirada</option><option value="Cancelada">Cancelada</option><option value="Vencida">Vencida</option><option value="Rechazada">Rechazada</option></select></label>
      <label class="campo"><span>Número de reserva</span><input v-model="filtros.numero" type="number" min="1" /></label>
      <details><summary>Filtrar por fechas</summary><label class="campo"><span>Desde</span><input v-model="filtros.desde" type="date" /></label><label class="campo"><span>Hasta</span><input v-model="filtros.hasta" type="date" /></label></details>
      <button class="boton boton-secundario" :disabled="cargando">Buscar reservas</button>
    </form>
    <p v-if="cargando" role="status">Consultando tus reservas…</p>
    <template v-if="!cargando && !error">
      <div class="reservas-tarjetas">
        <article v-for="e in items" :key="e.id" class="env-card reserva-tarjeta">
          <div class="env-card-heading"><AppIcon :nombre="icono(e)" :tamano="46" /><span class="env-status">{{ estadoVisible(e) }}</span></div>
          <h2>{{ servicio(e) }} #{{ e.id }}</h2>
          <p>{{ e.reserva?.tipoCasillero||'Normal' }} · {{ nombreEstadoReserva(e.tamano) }}<template v-if="e.reserva?.minutos"> · {{ e.reserva.minutos }} minutos</template></p>
          <p>{{ fechaHonduras(e.creadaUtc) }}</p><p v-if="e.reserva?.ubicacion" class="env-help">{{ e.reserva.ubicacion }}</p>
          <p v-if="e.reserva?.servicio==='Encomienda' && e.telefonoDestinatario">Destinatario: {{ e.telefonoDestinatario }}</p>
          <p v-if="e.depositoUtc">Depósito confirmado: {{ fechaHonduras(e.depositoUtc) }}</p>
          <p v-if="e.retiradaConfirmada">Retiro confirmado: {{ fechaHonduras(e.entregaUtc) }}</p><p v-else-if="e.estado==='Depositada'">Pendiente de retiro.</p>
          <p v-if="e.cancelacion">Cancelada el {{ fechaHonduras(e.cancelacion.fechaUtc) }} · {{ e.cancelacion.motivo }}</p>
          <p v-if="e.pagoRequiereRevision" class="env-help">Pago pendiente de revisión administrativa.</p>
          <div class="env-actions"><RouterLink :to="'/reservas/'+e.id" class="boton boton-primario">Ver reserva</RouterLink><RouterLink v-if="cancelable(e)" :to="{path:'/reservas/'+e.id,query:{cancelar:'1'}}" class="boton boton-secundario">Cancelar reserva</RouterLink></div>
        </article>
      </div>
      <p v-if="!items.length" class="env-empty">No hay reservas con estos filtros.</p>
      <nav v-if="totalPaginas>1" class="env-card-heading" aria-label="Paginación de mis reservas"><button class="boton boton-secundario" :disabled="pagina<=1" @click="cambiarPagina(-1)">Anterior</button><span>Página {{ pagina }} de {{ totalPaginas||1 }} · {{ total }} reservas</span><button class="boton boton-secundario" :disabled="pagina>=totalPaginas" @click="cambiarPagina(1)">Siguiente</button></nav>
    </template>
  </section>
</template>
<style scoped>
.reservas-tarjetas{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:20px}.filtros-reservas{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));align-items:end}.filtros-reservas details{align-self:start}.reserva-tarjeta .env-actions .boton{flex:1 1 140px}@media(max-width:650px){.reservas-tarjetas,.filtros-reservas{grid-template-columns:1fr}}
</style>
