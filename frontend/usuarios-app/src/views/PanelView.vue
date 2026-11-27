<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, ref, watch } from 'vue';
import { RouterLink, useRoute } from 'vue-router';
import { encomiendasApi } from '../services/encomiendasApi.js';
import { nombreEstadoReserva } from '../services/formatos.js';
const route=useRoute();
const esHistorial=computed(()=>route.name==='historial');
const categoria=computed(()=>esHistorial.value?'Historial':'Activas');
const items=ref<any[]>([]),cargando=ref(true),error=ref(''),pagina=ref(1),total=ref(0),totalPaginas=ref(0);
const buscarNumero=ref('');
const numeroAplicado=ref<number|null>(null);
function buscarEnHistorial(){
  if(!esHistorial.value)return;
  const texto=buscarNumero.value.trim().replace(/^#/,'');
  if(texto&&!/^[1-9][0-9]*$/.test(texto)){
    error.value='Introduce un número de reserva válido.';
    return;
  }
  const numero=texto?Number(texto):null;
  if(numero!==null&&!Number.isSafeInteger(numero)){
    error.value='Introduce un número de reserva válido.';
    return;
  }
  numeroAplicado.value=numero;
  void cargar(true);
}
function limpiarBusqueda(){
  buscarNumero.value='';
  numeroAplicado.value=null;
  void cargar(true);
}
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
  const d=items.value.find((e:any)=>Number(e.id)===id);
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
    aviso.value='Reserva #'+id+' cancelada. Puedes encontrarla en Historial.';
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
  try {
    const datos=await encomiendasApi.listar({categoria:categoria.value,...(esHistorial.value&&numeroAplicado.value!==null?{numero:numeroAplicado.value}:{}),pagina:pagina.value,tamanoPagina:12});
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
watch(()=>route.name,()=>{cerrarCancelacion();buscarNumero.value='';numeroAplicado.value=null;void cargar(true);});
onBeforeUnmount(()=>{montado=false;numeroConsulta++;clearInterval(reloj);});
</script>
<template>
  <section class="env-page">
    <header class="env-heading"><div><h1>{{ esHistorial?'Historial':'Mis reservas' }}</h1><p>{{ esHistorial?'Consulta las reservas canceladas, vencidas y finalizadas.':'Consulta y gestiona únicamente tus reservas vigentes.' }}</p></div><div class="env-actions"><button type="button" class="boton boton-secundario" :disabled="cargando" @click="cargar()">Actualizar</button><RouterLink :to="esHistorial?'/mis-reservas':'/historial'" class="boton boton-primario">{{ esHistorial?'Mis reservas':'Ver historial' }}</RouterLink></div></header>
    <p v-if="aviso" class="mensaje-exito" role="status">{{ aviso }}</p>
    <p v-if="error" class="alerta" role="alert">{{ error }} <button class="boton boton-secundario" @click="cargar()">Reintentar</button></p>
    <form v-if="esHistorial" class="historial-busqueda" role="search" @submit.prevent="buscarEnHistorial">
      <label for="numero-buscar" class="historial-busqueda__etiqueta">Buscar una reserva</label>
      <div class="historial-busqueda__controles">
        <input id="numero-buscar" v-model="buscarNumero" type="text" inputmode="numeric"
          maxlength="12" autocomplete="off" placeholder="Número de reserva, ej. 5012" aria-label="Número de reserva" />
        <button class="boton boton-primario" type="submit" :disabled="cargando">Buscar</button>
        <button v-if="numeroAplicado!==null || buscarNumero.trim()" class="boton boton-secundario" type="button"
          :disabled="cargando" @click="limpiarBusqueda">Limpiar</button>
      </div>
    </form>
    <p v-if="cargando" role="status">Consultando tus reservas…</p>
    <template v-if="!cargando && !error">
      <div class="reservas-tarjetas">
        <article v-for="e in items" :key="e.id" class="env-card reserva-tarjeta" :class="{'reserva-tarjeta--historica':esHistorial}">
          <div class="reserva-tarjeta__cabecera">
            <h2>Reserva #{{ e.id }}</h2>
            <span class="env-status">{{ estadoEtapa(detalleDe(e)) }}</span>
          </div>
          <div class="reserva-tarjeta__acciones">
            <RouterLink :to="esHistorial?'/reservas/'+e.id:accionDe(detalleDe(e))" class="boton boton-primario">{{ esHistorial?'Ver detalle':accionTexto(detalleDe(e)) }}</RouterLink>
            <button v-if="!esHistorial && detalles[Number(e.id)] && cancelable(detalleDe(e))" type="button" class="boton boton-secundario"
              :disabled="procesandoCancelacion" @click="cerrarCancelacion();cancelandoId=Number(e.id)">Cancelar reserva</button>
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
      <p v-if="!items.length" class="env-empty">{{ esHistorial?(numeroAplicado!==null?'No se encontró esa reserva en tu historial.':'Todavía no tienes operaciones en el historial.'):'No tienes reservas activas.' }}</p>
      <nav v-if="totalPaginas>1" class="env-card-heading" aria-label="Paginación de mis reservas"><button class="boton boton-secundario" :disabled="pagina<=1" @click="cambiarPagina(-1)">Anterior</button><span>Página {{ pagina }} de {{ totalPaginas||1 }} · {{ total }} reservas</span><button class="boton boton-secundario" :disabled="pagina>=totalPaginas" @click="cambiarPagina(1)">Siguiente</button></nav>
    </template>
  </section>
</template>
<style scoped>
.reservas-tarjetas{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px;}
.historial-busqueda{display:grid;gap:9px;max-width:620px;padding:0;}
.historial-busqueda__etiqueta{font-size:13px;font-weight:750;color:var(--text-strong);}
.historial-busqueda__controles{display:flex;flex-wrap:wrap;align-items:center;gap:9px;}
.historial-busqueda__controles input{height:47px;min-width:0;flex:1 1 220px;border-radius:12px;background:var(--surface);color:var(--text-strong);border:1px solid var(--border-strong);padding:10px 13px;box-sizing:border-box;}
.historial-busqueda__controles button{min-height:47px;height:47px;padding:10px 17px;flex:0 0 auto;}
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
@media(max-width:650px){.reservas-tarjetas{grid-template-columns:1fr;}.reserva-tarjeta__acciones .boton{flex:1 1 125px;}.historial-busqueda__controles input{flex-basis:100%;}.historial-busqueda__controles button{flex:1 1 120px;}}
</style>
