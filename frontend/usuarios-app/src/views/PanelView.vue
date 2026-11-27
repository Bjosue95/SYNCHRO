<script setup lang="ts">
import { onMounted, onBeforeUnmount, reactive, ref } from 'vue';
import { RouterLink } from 'vue-router';
import { encomiendasApi } from '../services/encomiendasApi.js';
import { fechaHonduras,nombreEstadoReserva } from '../services/formatos.js';
import AppIcon from '../components/ui/AppIcon.vue';
const items=ref<any[]>([]),cargando=ref(true),error=ref(''),pagina=ref(1),total=ref(0),totalPaginas=ref(0);
const filtros=reactive({estado:'',desde:'',hasta:'',numero:''});
const detalles=ref<Record<number,any>>({});
const cancelandoId=ref<number|null>(null);
const motivoCancelacion=ref('');
const detalleCancelacion=ref('');
const confirmarCancelacion=ref(false);
const procesandoCancelacion=ref(false);
const aviso=ref('');
let montado=true;
let reloj:ReturnType<typeof setInterval>|undefined;
function detalleDe(e:any){return detalles.value[Number(e.id)]||e;}
function depositada(d:any){return Boolean(d?.depositoUtc)||['Depositada','Retirada'].includes(d?.estado);}
function pinEntregado(d:any,finalidad:string){return Boolean(d?.pines?.some((p:any)=>
  p.finalidad===finalidad&&['Entregado','Consumido'].includes(p.estado)));}
function pinSolicitado(d:any){return Boolean(d?.historial?.some((h:any)=>h.accion==='PinDepositoSolicitado'));}
function esPersonal(d:any){return d?.reserva?.servicio==='Personal';}
function estadoEtapa(d:any){
  if(d?.cancelacion||d?.estado==='Cancelada')return 'Cancelada';
  if(d?.retiradaConfirmada)return 'Completada';
  if(depositada(d)){
    if(esPersonal(d))return 'Pertenencias resguardadas';
    if(pinEntregado(d,'Retiro'))return 'Esperando retiro';
    if(d?.destinatario?.consentimiento==='Confirmado'&&d?.destinatario?.aceptaEncomienda)return 'Preparando PIN de retiro';
    return 'Autorizar destinatario';
  }
  if(pinEntregado(d,'Deposito'))return 'Esperando depósito';
  if(pinSolicitado(d)||d?.pines?.some((p:any)=>p.finalidad==='Deposito'&&p.estado==='Emitido'))return 'Preparando PIN';
  if(d?.condiciones?.pagoConfirmado&&d?.condiciones?.remitenteAutorizado)return 'Solicitar PIN';
  if(d?.condiciones&&!d?.condiciones?.remitenteAutorizado)return 'Autorizar WhatsApp';
  return 'En preparación';
}
function accionDe(d:any){
  if(estadoEtapa(d)==='Solicitar PIN')return '/reservas/'+d.id+'/solicitar-pin';
  if(estadoEtapa(d)==='Autorizar destinatario')return '/reservas/'+d.id+'#destinatario-autorizacion';
  return '/reservas/'+d.id;
}
function accionTexto(d:any){
  if(estadoEtapa(d)==='Solicitar PIN')return 'Solicitar PIN';
  if(estadoEtapa(d)==='Autorizar destinatario')return 'Gestionar destinatario';
  return 'Ver reserva';
}
function cerrarCancelacion(){cancelandoId.value=null;motivoCancelacion.value='';detalleCancelacion.value='';confirmarCancelacion.value=false;}
async function cancelarSeleccionada(){
  const id=cancelandoId.value;
  const d=items.value.find((e:any)=>e.id===id);
  const actual=d&&detalleDe(d);
  if(!actual||!cancelable(actual)||!confirmarCancelacion.value||!motivoCancelacion.value
    ||(motivoCancelacion.value==='Otro motivo'&&!detalleCancelacion.value.trim())||procesandoCancelacion.value)return;
  procesandoCancelacion.value=true;error.value='';
  try{
    await encomiendasApi.cancelar(id,{
      motivo:motivoCancelacion.value,
      detalle:motivoCancelacion.value==='Otro motivo'?detalleCancelacion.value.trim():'',
      confirmada:true,version:actual.version
    });
    cerrarCancelacion();
    aviso.value='Reserva #'+id+' cancelada. Consulta el registro desde Mis reservas.';
    await cargar();
  }catch(e){
    error.value=(e as Error).message||'No fue posible cancelar la reserva.';
  }finally{procesandoCancelacion.value=false;}
}

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
    detalles.value={};
    // Los estados de PIN y de autorización solo provienen del detalle real.
    const resultados=await Promise.allSettled(datos.encomiendas.map((e:any)=>encomiendasApi.detalle(e.id)));
    if(!montado||actual!==numeroConsulta)return;
    const mapa:Record<number,any>={};
    resultados.forEach((r,i)=>{
      if(r.status==='fulfilled'&&Number(r.value?.id)===Number(datos.encomiendas[i].id))
        mapa[Number(r.value.id)]=r.value;
    });
    detalles.value=mapa;
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
function cancelable(e:any){return !depositada(e)&&!e?.cancelacion&&!e?.historica&&!e?.retiradaConfirmada
  &&['Preparacion','ListaParaDeposito','Vencida','PendienteConsentimiento','Pendiente'].includes(e.estado);}
function servicio(e:any){return e.reserva?.servicio==='Personal'?'Pertenencias personales':'Encomienda';}
function estadoVisible(e:any){
  if(e.retiradaConfirmada)return e.reserva?.servicio==='Personal'?'Retiro confirmado':'Entrega confirmada';
  if(e.estado==='Retirada')return 'Retiro pendiente de confirmar';
  return nombreEstadoReserva(e.estado);
}
function icono(e:any){return e.reserva?.tipoCasillero==='Refrigerado'?'frio':e.reserva?.tipoCasillero==='Caliente'?'calor':'casillero';}
onMounted(()=>{
  void cargar();
  reloj=setInterval(()=>{
    if(!document.hidden&&!cargando.value&&!procesandoCancelacion.value)void cargar();
  },15000);
});
onBeforeUnmount(()=>{montado=false;numeroConsulta++;clearInterval(reloj);});
</script>
<template>
  <section class="env-page">
    <header class="env-heading"><div><h1>Mis reservas</h1><p>Consulta las reservas, los depósitos y los retiros confirmados por SYNCHRO.</p></div><div class="env-actions"><button type="button" class="boton boton-secundario" :disabled="cargando" @click="cargar()">Actualizar</button><RouterLink to="/reservar" class="boton boton-primario">Reservar casillero</RouterLink></div></header>
    <p v-if="aviso" class="mensaje-exito" role="status">{{ aviso }}</p>
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
          <div class="reserva-tarjeta__cabecera">
            <h2>Reserva #{{ e.id }}</h2>
            <span class="env-status">{{ estadoEtapa(detalleDe(e)) }}</span>
          </div>
          <p class="reserva-tarjeta__servicio">{{ servicio(e) }}</p>
          <p class="reserva-tarjeta__ubicacion">{{ e.reserva?.ubicacion||'Estación pendiente' }}</p>
          <div class="reserva-tarjeta__acciones">
            <RouterLink :to="accionDe(detalleDe(e))" class="boton boton-primario">{{ accionTexto(detalleDe(e)) }}</RouterLink>
            <button v-if="cancelable(detalleDe(e))" type="button" class="boton boton-secundario"
              :disabled="procesandoCancelacion" @click="cerrarCancelacion();cancelandoId=e.id">Cancelar reserva</button>
          </div>
          <form v-if="cancelandoId===e.id" class="reserva-tarjeta__cancelacion" @submit.prevent="cancelarSeleccionada">
            <strong>Confirmar cancelación</strong>
            <p>Solo puedes cancelar antes del depósito. Si el pago está confirmado, requiere revisión administrativa.</p>
            <label class="campo"><span>Motivo</span>
              <select v-model="motivoCancelacion" required :disabled="procesandoCancelacion">
                <option value="" disabled>Selecciona un motivo</option>
                <option v-if="!esPersonal(detalleDe(e))">Número de destinatario incorrecto</option>
                <option>Tamaño de casillero incorrecto</option>
                <option>Ya no desea utilizar el casillero</option>
                <option>Otro motivo</option>
              </select>
            </label>
            <label v-if="motivoCancelacion==='Otro motivo'" class="campo"><span>Describe el motivo</span>
              <textarea v-model="detalleCancelacion" maxlength="160" required :disabled="procesandoCancelacion"></textarea>
            </label>
            <label class="reserva-tarjeta__confirmacion"><input v-model="confirmarCancelacion" type="checkbox" :disabled="procesandoCancelacion" />
              Confirmo que deseo cancelar esta reserva.
            </label>
            <div class="reserva-tarjeta__acciones">
              <button type="button" class="boton boton-secundario" :disabled="procesandoCancelacion" @click="cerrarCancelacion">Volver</button>
              <button class="boton boton-primario" :disabled="procesandoCancelacion||!motivoCancelacion||!confirmarCancelacion||(motivoCancelacion==='Otro motivo'&&!detalleCancelacion.trim())">
                {{ procesandoCancelacion?'Cancelando…':'Confirmar cancelación' }}
              </button>
            </div>
          </form>
        </article>
      </div>
      <p v-if="!items.length" class="env-empty">No hay reservas con estos filtros.</p>
      <nav v-if="totalPaginas>1" class="env-card-heading" aria-label="Paginación de mis reservas"><button class="boton boton-secundario" :disabled="pagina<=1" @click="cambiarPagina(-1)">Anterior</button><span>Página {{ pagina }} de {{ totalPaginas||1 }} · {{ total }} reservas</span><button class="boton boton-secundario" :disabled="pagina>=totalPaginas" @click="cambiarPagina(1)">Siguiente</button></nav>
    </template>
  </section>
</template>
<style scoped>
.reservas-tarjetas{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px;}
.filtros-reservas{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));align-items:end;}
.filtros-reservas details{align-self:start;}
.reserva-tarjeta{display:grid;align-content:start;gap:12px;min-width:0;padding:clamp(15px,2vw,22px);}
.reserva-tarjeta__cabecera{display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:9px;}
.reserva-tarjeta__cabecera h2{font-size:clamp(17px,2.5vw,22px);color:var(--text-strong);}
.reserva-tarjeta__servicio{font-size:13px;font-weight:700;color:var(--text-strong);}
.reserva-tarjeta__ubicacion{font-size:12px;line-height:1.5;color:var(--text-muted);}
.reserva-tarjeta__acciones{display:flex;flex-wrap:wrap;gap:9px;margin-top:5px;}
.reserva-tarjeta__acciones .boton{min-height:47px;height:auto;flex:1 1 130px;text-align:center;}
.reserva-tarjeta__cancelacion{display:grid;gap:11px;border-top:1px solid var(--border);padding-top:14px;}
.reserva-tarjeta__cancelacion>strong{color:var(--text-strong);}
.reserva-tarjeta__cancelacion>p{font-size:12px;line-height:1.5;color:var(--text-muted);}
.reserva-tarjeta__confirmacion{display:flex;gap:9px;align-items:flex-start;font-size:12px;color:var(--text-strong);}
.reserva-tarjeta__cancelacion textarea{width:100%;min-height:70px;}
@media(max-width:650px){.reservas-tarjetas,.filtros-reservas{grid-template-columns:1fr;}.reserva-tarjeta__acciones .boton{flex:1 1 125px;}}
</style>
