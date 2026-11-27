<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, ref, watch } from 'vue';
import { RouterLink, useRoute } from 'vue-router';
import { encomiendasApi } from '../services/encomiendasApi.js';

const route=useRoute();
const detalle=ref<any>(null);
const error=ref('');
const aviso=ref('');
const cargando=ref(false);
let montada=true;
let consultaActual=0;
let reloj:ReturnType<typeof setInterval>|undefined;

const esPersonal=computed(()=>detalle.value?.reserva?.servicio==='Personal');
const depositoConfirmado=computed(()=>Boolean(detalle.value?.depositoUtc)
  ||['Depositada','Retirada'].includes(detalle.value?.estado));
const pinRetiroEntregado=computed(()=>Boolean(detalle.value?.pines?.some(
  (p:any)=>p.finalidad==='Retiro'&&['Entregado','Consumido'].includes(p.estado))));

async function cargar(){
  const id=String(route.params.id);
  const actual=++consultaActual;
  cargando.value=true;
  try{
    const resultado=await encomiendasApi.detalle(id);
    if(montada&&actual===consultaActual&&id===String(route.params.id)){
      detalle.value=resultado;
      error.value='';
    }
  }catch(e){
    if(montada&&actual===consultaActual)error.value=(e as Error).message||'No se pudo consultar tu reserva.';
  }finally{
    if(montada&&actual===consultaActual)cargando.value=false;
  }
}
function enlaceValido(valor:unknown):valor is string{
  return typeof valor==='string'&&/^https:\/\/wa\.me\/[1-9][0-9]{7,14}\?text=/.test(valor);
}
async function copiarMensaje(){
  try{
    await navigator.clipboard.writeText(detalle.value.compartir.mensaje);
    aviso.value='Invitación copiada. Envíala al destinatario para que autorice los mensajes desde su teléfono.';
  }catch{
    error.value='No fue posible copiar la invitación.';
  }
}
watch(()=>route.params.id,()=>{
  detalle.value=null;error.value='';aviso.value='';
  void cargar();
});
onMounted(async()=>{
  await cargar();
  if(!montada)return;
  reloj=setInterval(()=>{
    if(!document.hidden&&!cargando.value)void cargar();
  },15000);
});
onBeforeUnmount(()=>{montada=false;consultaActual++;clearInterval(reloj);});
</script>
<template>
  <section class="env-page reserva-detalle-simple">
    <RouterLink to="/mis-reservas" class="enlace-volver">Mis reservas</RouterLink>
    <p v-if="error" class="alerta" role="alert">{{ error }}</p>
    <p v-if="aviso" class="mensaje-exito" role="status">{{ aviso }}</p>
    <p v-if="!detalle && cargando" role="status">Consultando la reserva…</p>
    <template v-if="detalle">
      <section class="env-card reserva-ficha" aria-labelledby="ficha-reserva-titulo">
        <span class="reserva-ficha__etiqueta">MI RESERVA</span>
        <h1 id="ficha-reserva-titulo">Reserva #{{ detalle.id }}</h1>
        <div class="reserva-ficha__datos">
          <div>
            <span>Servicio</span>
            <strong>{{ esPersonal ? 'Guardar pertenencias' : 'Encomienda' }}</strong>
          </div>
          <div>
            <span>Importe</span>
            <strong>{{ detalle.pago?.importeHnl==null ? 'Por definir' : 'L '+Number(detalle.pago.importeHnl).toFixed(2) }}</strong>
            <small v-if="detalle.pago?.estado!=='ConfirmadoManual'">Pendiente de confirmación</small>
          </div>
          <div class="reserva-ficha__ubicacion">
            <span>Estación</span>
            <strong>{{ detalle.reserva?.ubicacion || 'Ubicación pendiente' }}</strong>
          </div>
        </div>
      </section>

      <section v-if="depositoConfirmado && !detalle.cancelacion" id="deposito-retiro"
        class="env-card reserva-operacion" aria-labelledby="operacion-titulo">
        <span class="reserva-ficha__etiqueta">DEPÓSITO CONFIRMADO</span>
        <template v-if="esPersonal">
          <h2 id="operacion-titulo">{{ detalle.retiradaConfirmada ? 'Retiro completado' : 'Tus pertenencias están resguardadas' }}</h2>
          <p>{{ detalle.retiradaConfirmada
            ? 'El retiro de tus pertenencias quedó registrado correctamente.'
            : 'Tus pertenencias ya están depositadas. Cuando desees retirarlas, sigue el procedimiento de verificación en la estación SYNCHRO.' }}</p>
        </template>
        <template v-else>
          <h2 id="operacion-titulo">{{ detalle.retiradaConfirmada ? 'Encomienda entregada'
            : pinRetiroEntregado ? 'Esperando retiro del destinatario'
            : detalle.destinatario?.aceptaEncomienda && detalle.destinatario?.consentimiento==='Confirmado'
            ? 'Preparando PIN de retiro' : 'Autorizar al destinatario' }}</h2>
          <p v-if="detalle.retiradaConfirmada">El retiro de la encomienda quedó confirmado.</p>
          <p v-else-if="pinRetiroEntregado">El PIN de retiro fue entregado al número autorizado del destinatario.</p>
          <p v-else-if="detalle.destinatario?.aceptaEncomienda && detalle.destinatario?.consentimiento==='Confirmado'">El destinatario autorizó recibir mensajes. SYNCHRO gestionará la entrega del PIN de retiro.</p>
          <template v-else>
            <p>El depósito está confirmado. Ahora puedes compartir la invitación para que el destinatario autorice recibir mensajes por WhatsApp.</p>
            <div id="destinatario-autorizacion" class="reserva-operacion__acciones">
              <a v-if="enlaceValido(detalle.compartir?.enlaceWhatsApp)"
                :href="detalle.compartir.enlaceWhatsApp" class="boton boton-primario"
                target="_blank" rel="noopener noreferrer">
                Enviar invitación por WhatsApp
              </a>
              <button v-else-if="detalle.compartir?.mensaje" type="button" class="boton boton-secundario" @click="copiarMensaje">
                Copiar invitación
              </button>
              <p v-else class="env-help">La invitación estará disponible cuando SYNCHRO prepare la autorización del destinatario.</p>
            </div>
            <p class="env-help">El destinatario debe aceptar desde su teléfono. Compartir el mensaje no confirma su autorización.</p>
          </template>
        </template>
      </section>

      <section v-if="detalle.cancelacion" class="env-card reserva-operacion" aria-label="Reserva cancelada">
        <h2>Reserva cancelada</h2>
        <p>Esta reserva ya no está disponible para depósito ni retiro.</p>
        <p v-if="detalle.pagoRequiereRevision" class="env-help">El pago confirmado requiere revisión administrativa; no se realizó un reembolso automático.</p>
      </section>
    </template>
  </section>
</template>

<style scoped>
.reserva-detalle-simple{display:grid;align-content:start;gap:clamp(12px,2vw,18px);max-width:850px;margin-inline:auto;padding-bottom:24px;}
.reserva-ficha,.reserva-operacion{display:grid;gap:clamp(14px,2.3vw,20px);padding:clamp(18px,3vw,28px);min-width:0;}
.reserva-ficha__etiqueta{color:var(--accent-text);font-size:10px;letter-spacing:.12em;font-weight:850;}
.reserva-ficha h1{font-size:clamp(24px,4vw,34px);letter-spacing:-.025em;color:var(--text-strong);line-height:1.15;}
.reserva-ficha__datos{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:18px 16px;}
.reserva-ficha__datos>div{display:grid;align-content:start;gap:7px;min-width:0;}
.reserva-ficha__datos span{font-size:12px;color:var(--text-muted);}
.reserva-ficha__datos strong{font-size:clamp(15px,2vw,20px);line-height:1.3;color:var(--text-strong);overflow-wrap:anywhere;}
.reserva-ficha__datos small{font-size:11px;color:var(--text-muted);}
.reserva-ficha__ubicacion{grid-column:1/-1;padding-top:16px;border-top:1px solid var(--border);}
.reserva-operacion{border-color:var(--border);margin-top:4px;}
.reserva-operacion h2{font-size:clamp(19px,3vw,26px);color:var(--text-strong);line-height:1.25;}
.reserva-operacion p{font-size:clamp(12px,1.7vw,15px);color:var(--text-muted);line-height:1.6;}
.reserva-operacion__acciones{display:flex;flex-wrap:wrap;gap:10px;scroll-margin-top:90px;}
.reserva-operacion__acciones .boton{min-height:48px;padding:11px 18px;text-align:center;}
#deposito-retiro{scroll-margin-top:90px;}
@media(max-width:470px){
  .reserva-detalle-simple{padding-bottom:18px;}
  .reserva-ficha,.reserva-operacion{padding:18px 15px;}
  .reserva-ficha__datos{gap:15px 10px;}
  .reserva-operacion__acciones .boton{width:100%;}
}
</style>
