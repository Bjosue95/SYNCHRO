<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { useRoute } from 'vue-router';
import { apiAdmin } from '../services/adminApi';
import WhatsAppManualAdminView from "./WhatsAppManualAdminView.vue";
import EncomiendasView from './EncomiendasView.vue';
const route=useRoute();
const servicio=ref(''),telefono=ref(''),referencia=ref(''),pagina=ref(1),totalPaginas=ref(0),total=ref(0);
const solicitudes=ref<any[]>([]),cargando=ref(false),error=ref('');
const seleccionEncomienda=ref(Number(route.query.encomienda)||undefined),seleccionPersonal=ref(typeof route.query.intento==='string'?route.query.intento:'');
const servicios=[{id:'',nombre:'Todos'},{id:'Encomiendas',nombre:'Reservas web'},{id:'ResguardoPersonal',nombre:'Resguardo personal (tablet)'}];
function fecha(valor:string){return valor?new Date(valor).toLocaleString('es-HN',{timeZone:'America/Tegucigalpa'}):'No registrada';}
function rotulo(valor:string){return ({NoEmitido:'Sin emitir',EnviadoManual:'Envío manual confirmado',ConfirmadoManual:'Confirmado manualmente',PendienteRevision:'Pendiente de revisión',Deposito:'Depósito',RetiroPersonal:'Retiro personal',Preparacion:'En preparación',Depositada:'Depositada y pendiente de retiro',Retirada:'Retirada por el destinatario'} as Record<string,string>)[valor]||valor;}
function estadoOperacion(item:any){return item.servicio==='ResguardoPersonal'&&item.estado==='Retirada'?'Pertenencias retiradas por el titular':item.servicio==='ResguardoPersonal'&&item.estado==='Depositada'?'Pertenencias depositadas':rotulo(item.estado);}
async function cargar(reiniciar=false){
  if(reiniciar)pagina.value=1;cargando.value=true;error.value='';
  try{
    const query=new URLSearchParams({servicio:servicio.value,telefono:telefono.value,referencia:referencia.value,pagina:String(pagina.value),tamanoPagina:'20'});
    const datos=await apiAdmin('/api/admin/whatsapp-pin/solicitudes?'+query);
    solicitudes.value=datos.solicitudes;total.value=datos.total;totalPaginas.value=datos.totalPaginas;
  }catch(e){error.value=(e as Error).message;solicitudes.value=[];}finally{cargando.value=false;}
}
function elegirServicio(id:string){servicio.value=id;seleccionEncomienda.value=undefined;seleccionPersonal.value='';void cargar(true);}
function seleccionar(item:any){seleccionEncomienda.value=item.servicio==='Encomiendas'?item.encomiendaId:undefined;seleccionPersonal.value=item.servicio==='ResguardoPersonal'?item.intentoId:'';}
async function cambiarPagina(cambio:number){pagina.value+=cambio;await cargar();}
onMounted(()=>cargar());
</script>

<template>
  <div class="wa-workspace">
    <div class="page-heading">
      <div>
        <p class="eyebrow">OPERACIONES · VERIFICACIÓN MANUAL</p>
        <h1>WhatsApp y PIN</h1>
        <p>Verificación manual de mensajes y entrega de códigos, separados por servicio y participante.</p>
      </div>
    </div>
    <p v-if="error" class="inline-error" role="alert">{{ error }}</p>
    <section class="wa-card">
      <div class="wa-actions" role="group" aria-label="Servicio de origen"><button v-for="s in servicios" :key="s.id" class="wa-button" :aria-pressed="servicio===s.id" @click="elegirServicio(s.id)">{{ s.nombre }}</button></div>
      <form class="wa-actions wa-filters" @submit.prevent="cargar(true)">
        <label>Teléfono completo<input v-model="telefono" type="tel" placeholder="+504…" /></label>
        <label>Reserva, operación o código de aceptación<input v-model="referencia" maxlength="40" /></label>
        <button class="wa-button" :disabled="cargando">Buscar solicitudes</button>
      </form>
      <p class="wa-help">Comprueba el mensaje real en el WhatsApp oficial. En resguardo personal, el teléfono se vincula al verificar el mensaje; antes puedes localizar la solicitud por su código. Carga de dispositivos conserva su procedimiento y no requiere un PIN nuevo.</p>
      <p v-if="cargando" role="status">Consultando solicitudes…</p>
      <p class="wa-help">{{ total }} solicitudes · Página {{ pagina }} de {{ totalPaginas||1 }}</p>
      <div class="wa-tarjetas">
        <article v-for="item in solicitudes" :key="item.servicio+item.operacionId+item.rol" class="wa-solicitud">
          <h2>{{ item.servicio==='Encomiendas'?'Reserva #'+item.encomiendaId:'Resguardo personal · tablet' }}</h2>
          <p><strong>{{ item.claseReserva==='Personal'?'Pertenencias personales':item.servicio==='Encomiendas'?'Encomienda':'Uso #'+(item.usoId||'Pendiente') }}</strong> · {{ item.tipoCasillero||'Normal' }}<template v-if="item.minutos"> · {{ item.minutos }} minutos</template></p>
          <p>{{ item.persona }} · {{ item.rol }}<br /><strong>{{ item.telefono||'Teléfono pendiente de comprobar' }}</strong></p>
          <p>WhatsApp: {{ rotulo(item.consentimiento) }}<br />PIN: {{ item.finalidad==='DepositoRetiro'?'Depósito y retiro':rotulo(item.finalidad) }} · {{ rotulo(item.emision) }}<br />Entrega: {{ rotulo(item.entrega) }}</p>
          <p>{{ estadoOperacion(item) }}<template v-if="item.servicio==='Encomiendas'"> · Pago {{ rotulo(item.pagoEstado) }}</template></p>
          <details><summary>Referencia y fechas</summary><p>{{ item.referencia }}<br />Solicitud: {{ fecha(item.creadoUtc) }}<br />Comprobación: {{ fecha(item.verificadoUtc) }} {{ item.responsable }}<br />Envío: {{ fecha(item.entregadoUtc) }}</p><small v-if="item.servicio==='ResguardoPersonal'">Intento {{ item.intentoId }}</small></details>
          <button class="wa-button" @click="seleccionar(item)">{{ item.servicio==='ResguardoPersonal'?'Abrir operación de resguardo':'Gestionar '+item.rol.toLowerCase() }}</button>
        </article>
      </div>
      <p v-if="!cargando&&!solicitudes.length">No hay solicitudes con estos filtros.</p>
      <nav class="wa-actions" aria-label="Paginación de solicitudes"><button class="wa-button" :disabled="pagina<=1||cargando" @click="cambiarPagina(-1)">Anterior</button><button class="wa-button" :disabled="pagina>=totalPaginas||cargando" @click="cambiarPagina(1)">Siguiente</button></nav>
    </section>
    <EncomiendasView v-if="seleccionEncomienda" :centro-whats-app="true" :encomienda-id="seleccionEncomienda" @actualizado="cargar()" />
    <WhatsAppManualAdminView v-if="seleccionPersonal" :integrado="true" :intento-id="seleccionPersonal" @actualizado="cargar()" />
  </div>
</template>
<style scoped>
.wa-tarjetas{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px}.wa-solicitud{display:grid;align-content:start;gap:12px;border:1px solid var(--border);border-radius:10px;padding:20px;min-width:0;background:var(--white);color:var(--ink)}.wa-solicitud h2{margin:0;font-size:20px}.wa-solicitud p{margin:0;overflow-wrap:anywhere}.wa-solicitud details{color:var(--muted);font-size:13px}@media(max-width:700px){.wa-tarjetas{grid-template-columns:1fr}}
.wa-card{background:var(--white);border:1px solid var(--border);border-radius:12px;padding:24px;margin-bottom:22px;color:var(--ink);min-width:0}.wa-actions{display:flex;flex-wrap:wrap;align-items:center;gap:12px;margin:12px 0}.wa-filters{align-items:end}.wa-filters label{display:grid;gap:6px;flex:1 1 210px;min-width:0}.wa-filters input{width:100%;min-width:0;padding:10px;border:1px solid var(--border);border-radius:7px;background:var(--white);color:var(--ink)}.wa-button{min-height:44px;padding:10px 15px;border:1px solid var(--border);border-radius:7px;background:var(--white);color:var(--ink);font-weight:600}.wa-button[aria-pressed=true]{background:var(--navy);color:var(--white)}.wa-help,small,caption{font-size:13px;color:var(--muted)}.wa-table-wrap{overflow:auto;max-height:560px;max-width:100%}table{width:100%;border-collapse:collapse;text-align:left;font-size:14px}th,td{padding:14px 12px;border-bottom:1px solid var(--border);min-width:130px;vertical-align:top}td small{display:block;margin-top:5px;overflow-wrap:anywhere}caption{text-align:left;padding:12px 0}@media(max-width:600px){.wa-card{padding:16px}.wa-filters label{flex-basis:100%}}
</style>
