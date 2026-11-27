<script setup lang="ts">
import { RouterLink } from 'vue-router';
import { computed,onMounted,onBeforeUnmount,ref } from 'vue';
import { estadoSesion } from '../services/sesion.js';
import { encomiendasApi } from '../services/encomiendasApi.js';
import AppIcon from '../components/ui/AppIcon.vue';

const usuario=computed(()=> (estadoSesion.cliente as {nombreUsuario?:string}|null)?.nombreUsuario || '');
const reservaPendiente=ref<any>(null);
const consultando=ref(true);
const errorReservas=ref('');
let montado=true;
let consultaActual=0;
const pinSolicitado=computed(()=>Boolean(reservaPendiente.value?.historial?.some(
  (h:any)=>h.accion==='PinDepositoSolicitado'
)));
const pinEmitido=computed(()=>Boolean(reservaPendiente.value?.pines?.some(
  (p:any)=>p.finalidad==='Deposito'&&['Emitido','Entregado','Consumido'].includes(p.estado)
)));
const pagoConfirmado=computed(()=>Boolean(reservaPendiente.value?.condiciones?.pagoConfirmado));
const whatsappConfirmado=computed(()=>Boolean(reservaPendiente.value?.condiciones?.remitenteAutorizado));
const siguienteAccion=computed(()=>{
  if(!reservaPendiente.value)return '';
  if(pinSolicitado.value)return 'Solicitud de PIN registrada';
  if(pinEmitido.value)return 'PIN de depósito en preparación';
  if(!whatsappConfirmado.value)return 'Completar autorización de WhatsApp';
  return 'Solicitar PIN de depósito';
});
const descripcionSiguiente=computed(()=>{
  if(pinSolicitado.value)return 'La solicitud está registrada. SYNCHRO gestionará la entrega del PIN.';
  if(pinEmitido.value)return 'Consulta el WhatsApp autorizado para recibir tu PIN.';
  if(!whatsappConfirmado.value)return 'Autoriza tu número antes de solicitar el PIN de depósito.';
  return 'El pago está confirmado. Puedes solicitar el PIN para depositar tus pertenencias o encomienda.';
});
async function cargarSiguienteAccion(){
  const actual=++consultaActual;
  consultando.value=true;
  errorReservas.value='';
  try{
    const respuesta=await encomiendasApi.listar({pagina:1,tamanoPagina:20});
    if(!Array.isArray(respuesta?.encomiendas))throw new Error('No fue posible comprobar tus reservas.');
    // Solo las operaciones cuyo pago ha sido confirmado; sin inventar estados.
    const candidatas=respuesta.encomiendas.filter((e:any)=>
      e.pagoEstado==='ConfirmadoManual'&&!e.depositoUtc&&!e.retiradaConfirmada&&!e.cancelacion
      &&!['Cancelada','Vencida','Rechazada','Depositada','Retirada'].includes(e.estado));
    let elegida:any=null;
    for(const reserva of candidatas.slice(0,8)){
      const detalle=await encomiendasApi.detalle(reserva.id);
      if(!montado||actual!==consultaActual)return;
      if(detalle?.condiciones?.pagoConfirmado&&!detalle?.depositoUtc&&!detalle?.cancelacion
        &&!detalle?.historica&&!['Vencida','Cancelada','Rechazada'].includes(detalle?.estado)){
        elegida=detalle;
        break;
      }
    }
    if(montado&&actual===consultaActual)reservaPendiente.value=elegida;
  }catch(e){
    if(montado&&actual===consultaActual)errorReservas.value=(e as Error).message||'No pudimos actualizar tus reservas.';
  }finally{
    if(montado&&actual===consultaActual)consultando.value=false;
  }
}
onMounted(()=>{void cargarSiguienteAccion();});
onBeforeUnmount(()=>{montado=false;consultaActual++;});
</script>

<template>
  <section class="inicio-cliente" aria-labelledby="inicio-cliente-titulo">
    <div class="inicio-cliente__presentacion">
      <div class="inicio-cliente__texto">
        <span class="inicio-cliente__etiqueta">TU ESPACIO SYNCHRO</span>
        <h1 id="inicio-cliente-titulo">Bienvenido{{ usuario ? ', '+usuario : '' }}.</h1>
        <p>Guarda tus pertenencias o gestiona una encomienda desde tu cuenta. Elige un servicio para comenzar cuando lo necesites.</p>
        <div class="inicio-cliente__acciones">
          <RouterLink to="/reservar" class="inicio-cliente__accion inicio-cliente__accion--principal">
            <AppIcon nombre="casillero" :tamano="24" />
            <span>Reservar casillero</span>
            <AppIcon nombre="flecha" :tamano="20" />
          </RouterLink>
          <RouterLink to="/mis-reservas" class="inicio-cliente__accion inicio-cliente__accion--secundaria">
            <AppIcon nombre="reservas" :tamano="21" />
            Mis reservas
          </RouterLink>
        </div>
      </div>
      <div class="inicio-cliente__escena" aria-hidden="true">
        <div class="inicio-cliente__modulo">
          <span class="inicio-cliente__marca">SYNCHRO</span>
          <div class="inicio-cliente__casilleros">
            <span><AppIcon nombre="casillero" :tamano="31" /></span>
            <span><AppIcon nombre="encomienda" :tamano="31" /></span>
            <span><AppIcon nombre="guardar" :tamano="31" /></span>
            <span><AppIcon nombre="seguridad" :tamano="31" /></span>
          </div>
          <span class="inicio-cliente__detalle">El punto donde todo se conecta</span>
        </div>
      </div>
    </div>

    <section v-if="reservaPendiente" class="inicio-cliente__siguiente" aria-labelledby="siguiente-paso-titulo">
      <div class="inicio-cliente__siguiente-icono"><AppIcon nombre="casillero" :tamano="31"/></div>
      <div class="inicio-cliente__siguiente-texto">
        <span class="inicio-cliente__etiqueta">SIGUIENTE PASO · RESERVA #{{ reservaPendiente.id }}</span>
        <h2 id="siguiente-paso-titulo">{{ siguienteAccion }}</h2>
        <p>{{ descripcionSiguiente }}</p>
      </div>
      <RouterLink :to="{path:'/reservas/'+reservaPendiente.id,hash:'#solicitar-pin-deposito'}" class="inicio-cliente__accion inicio-cliente__accion--principal">
        {{ pagoConfirmado&&whatsappConfirmado&&!pinSolicitado&&!pinEmitido ? 'Solicitar PIN de depósito' : 'Ver mi reserva' }}
        <AppIcon nombre="flecha" :tamano="19"/>
      </RouterLink>
    </section>
    <p v-else-if="errorReservas" class="inicio-cliente__error" role="status">
      No fue posible comprobar tus reservas. <RouterLink to="/mis-reservas">Consultar mis reservas</RouterLink>
    </p>
    <div class="inicio-cliente__servicios">
      <div class="inicio-cliente__encabezado">
        <h2>¿Qué puedes hacer en SYNCHRO?</h2>
        <p>Selecciona una opción y sigue los pasos de la plataforma.</p>
      </div>
      <div class="inicio-cliente__rejilla">
        <RouterLink :to="{path:'/reservar',query:{servicio:'Personal'}}" class="inicio-cliente__tarjeta">
          <span class="inicio-cliente__icono"><AppIcon nombre="guardar" :tamano="34"/></span>
          <h3>Guardar pertenencias</h3>
          <p>Elige un casillero, una ubicación y el tiempo que necesitas.</p>
          <span class="inicio-cliente__enlace">Comenzar <AppIcon nombre="flecha" :tamano="17"/></span>
        </RouterLink>
        <RouterLink :to="{path:'/reservar',query:{servicio:'Encomienda'}}" class="inicio-cliente__tarjeta">
          <span class="inicio-cliente__icono"><AppIcon nombre="encomienda" :tamano="34"/></span>
          <h3>Enviar una encomienda</h3>
          <p>Prepara una reserva para que otra persona pueda recogerla.</p>
          <span class="inicio-cliente__enlace">Comenzar <AppIcon nombre="flecha" :tamano="17"/></span>
        </RouterLink>
        <RouterLink to="/mis-reservas" class="inicio-cliente__tarjeta">
          <span class="inicio-cliente__icono"><AppIcon nombre="historial" :tamano="34"/></span>
          <h3>Consultar tus reservas</h3>
          <p>Revisa solicitudes, pagos, depósitos y retiros desde tu historial.</p>
          <span class="inicio-cliente__enlace">Ver reservas <AppIcon nombre="flecha" :tamano="17"/></span>
        </RouterLink>
      </div>
    </div>
    <div class="inicio-cliente__ayuda">
      <AppIcon nombre="seguridad" :tamano="23"/>
      <p><strong>Un proceso sencillo:</strong> selecciona tu servicio, revisa la reserva y sigue las indicaciones de SYNCHRO para el depósito o retiro.</p>
    </div>
  </section>
</template>

<style scoped>
.inicio-cliente{display:grid;gap:clamp(20px,3vw,34px);padding:clamp(10px,1.5vw,22px) 0 32px;color:var(--text-strong);}
.inicio-cliente__presentacion{display:grid;grid-template-columns:minmax(0,1.35fr) minmax(220px,.8fr);overflow:hidden;border-radius:22px;border:1px solid var(--border);background:var(--surface);min-height:300px;}
.inicio-cliente__texto{display:flex;flex-direction:column;align-items:flex-start;justify-content:center;gap:clamp(12px,2vw,20px);padding:clamp(22px,4vw,52px);}
.inicio-cliente__etiqueta{font-weight:850;letter-spacing:.12em;color:var(--accent-text);font-size:11px;}
.inicio-cliente__texto h1{font-size:clamp(27px,4.4vw,46px);line-height:1.15;letter-spacing:-.03em;overflow-wrap:anywhere;}
.inicio-cliente__texto>p{max-width:54ch;line-height:1.6;font-size:clamp(13px,1.5vw,16px);color:var(--text-muted);}
.inicio-cliente__acciones{display:flex;align-items:stretch;gap:11px;flex-wrap:wrap;}
.inicio-cliente__accion{display:inline-flex;align-items:center;justify-content:center;gap:11px;padding:13px 17px;min-height:52px;border-radius:13px;text-decoration:none;font-size:13px;font-weight:800;border:2px solid;transition:background-color .15s ease,border-color .15s ease;}
.inicio-cliente__accion--principal{background:#01305c;color:white;border-color:#00b9c7;}
.inicio-cliente__accion--principal:hover{background:#07517a;}
.inicio-cliente__accion--secundaria{background:var(--surface-soft);color:var(--text-strong);border-color:var(--border-strong);}
.inicio-cliente__accion--secundaria:hover{border-color:var(--brand-accent);}
.inicio-cliente__escena{display:grid;place-items:center;padding:26px;background:linear-gradient(135deg,#01305c 0%,#075478 65%,#008d9d 100%);}
.inicio-cliente__modulo{display:grid;gap:19px;width:min(100%,260px);padding:22px;border-radius:18px;background:#0e3858;border:2px solid #56bfcc;box-shadow:0 20px 45px #001f35a6;}
.inicio-cliente__marca{font-weight:900;letter-spacing:.16em;color:white;text-align:center;}
.inicio-cliente__casilleros{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:10px;}
.inicio-cliente__casilleros span{height:68px;display:grid;place-items:center;border:1px solid #60c6d8;border-radius:9px;background:#184c69;color:#fff;}
.inicio-cliente__detalle{font-size:10px;text-align:center;color:#c6e5f0;}
.inicio-cliente__servicios{display:grid;gap:17px;}
.inicio-cliente__encabezado{display:grid;gap:6px;}
.inicio-cliente__encabezado h2{font-size:clamp(19px,2.4vw,26px);}
.inicio-cliente__encabezado p{font-size:13px;color:var(--text-muted);}
.inicio-cliente__rejilla{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:15px;}
.inicio-cliente__tarjeta{display:flex;flex-direction:column;gap:11px;min-height:230px;padding:clamp(16px,2vw,24px);border:1px solid var(--border);border-radius:18px;text-decoration:none;background:var(--surface);color:var(--text-strong);transition:border-color .15s ease,transform .15s ease;}
.inicio-cliente__tarjeta:hover{border-color:var(--brand-accent);transform:translateY(-2px);}
.inicio-cliente__icono{width:58px;height:58px;display:grid;place-items:center;background:var(--surface-accent);border:1px solid var(--border);border-radius:15px;}
.inicio-cliente__tarjeta h3{font-size:clamp(14px,1.6vw,18px);}
.inicio-cliente__tarjeta p{font-size:13px;line-height:1.55;color:var(--text-muted);}
.inicio-cliente__enlace{margin-top:auto;display:flex;align-items:center;gap:7px;font-size:12px;font-weight:800;color:var(--accent-text);}
.inicio-cliente__ayuda{display:flex;align-items:center;gap:13px;padding:14px 17px;border-radius:13px;border:1px solid var(--border);background:var(--surface-soft);}
.inicio-cliente__ayuda p{font-size:12px;line-height:1.5;color:var(--text-muted);}
.inicio-cliente__ayuda strong{color:var(--text-strong);}
.inicio-cliente a:focus-visible{outline:3px solid var(--ring);outline-offset:3px;}
@media(max-width:900px){.inicio-cliente__presentacion{grid-template-columns:minmax(0,1fr) 205px;}.inicio-cliente__rejilla{gap:10px;}}
@media(max-width:650px){.inicio-cliente__presentacion{grid-template-columns:1fr;}.inicio-cliente__escena{display:none;}.inicio-cliente__texto{padding:23px 19px;}.inicio-cliente__acciones{width:100%;}.inicio-cliente__accion{flex:1 1 150px;}.inicio-cliente__rejilla{grid-template-columns:1fr;}.inicio-cliente__tarjeta{min-height:145px;padding:16px;}.inicio-cliente__ayuda{align-items:flex-start;}}
@media(prefers-reduced-motion:reduce){.inicio-cliente__tarjeta,.inicio-cliente__accion{transition:none;}}

.inicio-cliente__siguiente{display:grid;grid-template-columns:auto minmax(0,1fr) auto;align-items:center;gap:clamp(12px,2vw,22px);padding:clamp(16px,2.6vw,24px);border:1px solid #00b9c7;border-radius:18px;background:var(--surface);box-shadow:0 4px 17px rgb(0 185 199 / 8%);}
.inicio-cliente__siguiente-icono{display:grid;place-items:center;width:58px;height:58px;border-radius:15px;background:var(--surface-accent);color:var(--accent-text);}
.inicio-cliente__siguiente-texto{display:grid;gap:5px;min-width:0;}
.inicio-cliente__siguiente-texto h2{font-size:clamp(19px,2.7vw,26px);color:var(--text-strong);}
.inicio-cliente__siguiente-texto p{font-size:clamp(12px,1.5vw,14px);line-height:1.5;color:var(--text-muted);}
.inicio-cliente__error{font-size:12px;color:var(--text-muted);padding:10px 0;}
.inicio-cliente__error a{color:var(--accent-text);font-weight:800;}
@media(max-width:760px){.inicio-cliente__siguiente{grid-template-columns:auto minmax(0,1fr);}.inicio-cliente__siguiente>.inicio-cliente__accion{grid-column:1/-1;width:100%;}.inicio-cliente__siguiente-icono{width:48px;height:48px;}}
</style>
