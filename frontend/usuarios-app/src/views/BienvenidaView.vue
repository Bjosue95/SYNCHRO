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
const cancelando=ref(false);
const procesandoCancelacion=ref(false);
const motivoCancelacion=ref('');
const detalleCancelacion=ref('');
const confirmarCancelacion=ref(false);
const avisoReserva=ref('');
let montado=true;
let consultaActual=0;
const esPersonal=computed(()=>reservaPendiente.value?.reserva?.servicio==='Personal');
const depositoConfirmado=computed(()=>Boolean(reservaPendiente.value?.depositoUtc)
  ||['Depositada','Retirada'].includes(reservaPendiente.value?.estado));
const retiroConfirmado=computed(()=>Boolean(reservaPendiente.value?.retiradaConfirmada));
const pinDepositoEntregado=computed(()=>Boolean(reservaPendiente.value?.pines?.some(
  (p:any)=>p.finalidad==='Deposito'&&['Entregado','Consumido'].includes(p.estado))));
const pinDepositoEmitido=computed(()=>Boolean(reservaPendiente.value?.pines?.some(
  (p:any)=>p.finalidad==='Deposito'&&p.estado==='Emitido')));
const pinSolicitado=computed(()=>Boolean(reservaPendiente.value?.historial?.some(
  (h:any)=>h.accion==='PinDepositoSolicitado')));
const pinRetiroEntregado=computed(()=>Boolean(reservaPendiente.value?.pines?.some(
  (p:any)=>p.finalidad==='Retiro'&&['Entregado','Consumido'].includes(p.estado))));
const pagoConfirmado=computed(()=>Boolean(reservaPendiente.value?.condiciones?.pagoConfirmado));
const pagoDiferido=computed(()=>reservaPendiente.value?.reserva?.servicio==='Encomienda'
  &&reservaPendiente.value?.reserva?.pagador==='Destinatario'
  &&reservaPendiente.value?.reserva?.modalidadPago==='Tablet');
const whatsappConfirmado=computed(()=>Boolean(reservaPendiente.value?.condiciones?.remitenteAutorizado));
const destinatarioAutorizado=computed(()=>Boolean(reservaPendiente.value?.destinatario?.consentimiento==='Confirmado'
  &&reservaPendiente.value?.destinatario?.aceptaEncomienda));
const etapaSiguiente=computed(()=>{
  if(!reservaPendiente.value)return 'ninguna';
  if(retiroConfirmado.value)return 'completada';
  if(depositoConfirmado.value){
    if(esPersonal.value)return 'personal-resguardado';
    if(pinRetiroEntregado.value)return 'esperar-retiro';
    if(destinatarioAutorizado.value)return 'pin-retiro';
    return 'autorizar-destinatario';
  }
  if(pinDepositoEntregado.value)return 'esperar-deposito';
  if(pinDepositoEmitido.value||pinSolicitado.value)return 'pin-pendiente';
  if(!whatsappConfirmado.value)return 'autorizar-whatsapp';
  if(pagoConfirmado.value)return 'solicitar-pin';
  if(pagoDiferido.value)return 'pago-en-estacion';
  return 'pago-pendiente';
});
const textosEtapa:Record<string,{titulo:string,descripcion:string,boton:string,icono:string}>={
  'completada':{titulo:'Operación completada',descripcion:'El retiro se confirmó correctamente. Puedes consultar los detalles de esta operación en tu historial.',boton:'Ver historial',icono:'historial'},
  'personal-resguardado':{titulo:'Tus pertenencias están resguardadas',descripcion:'Tu depósito se realizó correctamente. Cuando necesites retirar tus pertenencias, utiliza el procedimiento de verificación de SYNCHRO.',boton:'Ver instrucciones de retiro',icono:'guardar'},
  'esperar-retiro':{titulo:'Esperando retiro del destinatario',descripcion:'El PIN de retiro ya fue entregado al WhatsApp autorizado. El destinatario puede dirigirse a la estación para recoger la encomienda.',boton:'Ver mi reserva',icono:'encomienda'},
  'pin-retiro':{titulo:'Preparando PIN de retiro',descripcion:'El destinatario autorizó recibir mensajes. SYNCHRO gestionará el envío de su PIN de retiro al WhatsApp correspondiente.',boton:'Ver estado de la reserva',icono:'seguridad'},
  'autorizar-destinatario':{titulo:'Autorizar al destinatario',descripcion:'Tu encomienda ya fue depositada. Envía la invitación al destinatario para que autorice recibir mensajes y el PIN de retiro por WhatsApp.',boton:'Gestionar destinatario',icono:'encomienda'},
  'esperar-deposito':{titulo:'Esperando depósito en estación',descripcion:'Tu PIN de depósito ya fue entregado al WhatsApp autorizado. Dirígete a la estación SYNCHRO y utiliza el PIN para realizar el depósito.',boton:'Ver mi reserva',icono:'casillero'},
  'pin-pendiente':{titulo:'Preparando PIN de depósito',descripcion:'Tu solicitud está registrada. El personal autorizado de SYNCHRO gestionará la emisión y entrega del PIN por WhatsApp.',boton:'Ver estado de la reserva',icono:'seguridad'},
  'autorizar-whatsapp':{titulo:'Autorizar tu WhatsApp',descripcion:'Comprueba la autorización de tu número antes de continuar con la solicitud del PIN de depósito.',boton:'Ver autorización',icono:'seguridad'},
  'solicitar-pin':{titulo:'Solicitar PIN de depósito',descripcion:'El pago está confirmado. Solicita el PIN para realizar el depósito en la estación SYNCHRO.',boton:'Solicitar PIN de depósito',icono:'casillero'},
  'pago-en-estacion':{titulo:'Continuar depósito en estación',descripcion:'Tu reserva está registrada. El pago de esta encomienda corresponde al destinatario en la terminal, según las condiciones de la operación.',boton:'Ver mi reserva',icono:'encomienda'},
  'pago-pendiente':{titulo:'Pago pendiente de confirmación',descripcion:'Consulta el estado de tu reserva. El proceso de depósito continuará cuando se cumplan las condiciones de pago.',boton:'Ver mi reserva',icono:'casillero'}
};
const siguienteAccion=computed(()=>textosEtapa[etapaSiguiente.value]?.titulo||'Ver mi reserva');
const descripcionSiguiente=computed(()=>textosEtapa[etapaSiguiente.value]?.descripcion||'Consulta los detalles de tu operación.');
const botonSiguiente=computed(()=>textosEtapa[etapaSiguiente.value]?.boton||'Ver mi reserva');
const iconoSiguiente=computed(()=>textosEtapa[etapaSiguiente.value]?.icono||'casillero');
const enlaceSiguiente=computed(()=>{
  const id=reservaPendiente.value?.id;
  if(!id)return '/mis-reservas';
  if(etapaSiguiente.value==='solicitar-pin')return '/reservas/'+id+'/solicitar-pin';
  if(etapaSiguiente.value==='autorizar-destinatario')return '/reservas/'+id+'#destinatario-autorizacion';
  if(etapaSiguiente.value==='autorizar-whatsapp')return '/reservas/'+id+'#personas-autorizaciones';
  if(etapaSiguiente.value==='personal-resguardado'||etapaSiguiente.value==='esperar-retiro')return '/reservas/'+id+'#deposito-retiro';
  return '/reservas/'+id;
});
const puedeCancelar=computed(()=>Boolean(reservaPendiente.value)
  &&!depositoConfirmado.value&&!retiroConfirmado.value
  &&!reservaPendiente.value?.historica&&!reservaPendiente.value?.cancelacion
  &&['Preparacion','ListaParaDeposito','Pendiente','PendienteConsentimiento','Vencida'].includes(reservaPendiente.value?.estado)));
function restablecerCancelacion(){
  cancelando.value=false;
  confirmarCancelacion.value=false;
  motivoCancelacion.value='';
  detalleCancelacion.value='';
}
async function cancelarReserva(){
  const d=reservaPendiente.value;
  if(!d||!puedeCancelar.value||procesandoCancelacion.value||!confirmarCancelacion.value||!motivoCancelacion.value
    ||motivoCancelacion.value==='Otro motivo'&&!detalleCancelacion.value.trim())return;
  procesandoCancelacion.value=true;
  errorReservas.value='';
  try{
    // No se confirma nada en el navegador: SQL comprueba estado, depósito y versión.
    await encomiendasApi.cancelar(d.id,{
      motivo:motivoCancelacion.value,detalle:motivoCancelacion.value==='Otro motivo'?detalleCancelacion.value.trim():'',
      confirmada:true,version:d.version
    });
    restablecerCancelacion();
    avisoReserva.value='Reserva cancelada. Puedes consultar su registro en Mis reservas.';
    await cargarSiguienteAccion();
  }catch(e){
    errorReservas.value=(e as Error).message||'No se pudo cancelar la reserva.';
    await cargarSiguienteAccion(false);
  }finally{procesandoCancelacion.value=false;}
}
async function cargarSiguienteAccion(limpiarError=true){
  const actual=++consultaActual;
  consultando.value=true;
  if(limpiarError)errorReservas.value='';
  try{
    const respuesta=await encomiendasApi.listar({pagina:1,tamanoPagina:30});
    if(!Array.isArray(respuesta?.encomiendas))throw new Error('No fue posible comprobar tus reservas.');
    const activas=respuesta.encomiendas.filter((e:any)=>
      !e.cancelacion&&!e.historica&&!e.retiradaConfirmada
      &&!['Cancelada','Rechazada','Vencida'].includes(e.estado));
    const finalizadas=respuesta.encomiendas.filter((e:any)=>
      !e.cancelacion&&!e.historica&&e.retiradaConfirmada);
    const candidatas=[...activas,...finalizadas];
    let elegida:any=null;
    for(const reserva of candidatas.slice(0,12)){
      const d=await encomiendasApi.detalle(reserva.id);
      if(!montado||actual!==consultaActual)return;
      if(!d?.cancelacion&&!d?.historica
        &&(d?.retiradaConfirmada||!['Cancelada','Rechazada','Vencida'].includes(d?.estado))){
        elegida=d;break;
      }
    }
    if(montado&&actual===consultaActual){
      reservaPendiente.value=elegida;
      if(!elegida)restablecerCancelacion();
    }
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
      <div class="inicio-cliente__siguiente-principal">
        <div class="inicio-cliente__siguiente-icono"><AppIcon :nombre="iconoSiguiente" :tamano="29"/></div>
        <div class="inicio-cliente__siguiente-texto">
          <span class="inicio-cliente__etiqueta">SIGUIENTE PASO · RESERVA #{{ reservaPendiente.id }}</span>
          <h2 id="siguiente-paso-titulo">{{ siguienteAccion }}</h2>
          <p>{{ descripcionSiguiente }}</p>
        </div>
        <RouterLink :to="enlaceSiguiente" class="inicio-cliente__accion inicio-cliente__accion--principal">
          {{ botonSiguiente }} <AppIcon nombre="flecha" :tamano="19"/>
        </RouterLink>
      </div>
      <div v-if="puedeCancelar" class="inicio-cliente__cancelacion">
        <template v-if="!cancelando">
          <div class="inicio-cliente__cancelacion-texto">
            <strong>¿Ya no deseas continuar?</strong>
            <p>Puedes cancelar esta reserva mientras no hayas realizado el depósito. Si el pago está confirmado, quedará pendiente de revisión; no se reembolsa automáticamente.</p>
          </div>
          <button type="button" class="inicio-cliente__cancelacion-boton" @click="cancelando=true">Cancelar reserva</button>
        </template>
        <form v-else class="inicio-cliente__cancelacion-formulario" @submit.prevent="cancelarReserva" aria-label="Confirmar cancelación de reserva">
          <h3>Confirmar cancelación</h3>
          <p>Se cerrará la reserva y se invalidarán sus PIN. Esta operación no puede deshacerse.</p>
          <label class="campo"><span>Motivo</span>
            <select v-model="motivoCancelacion" :disabled="procesandoCancelacion" required>
              <option value="" disabled>Selecciona un motivo</option>
              <option v-if="!esPersonal">Número del destinatario incorrecto</option>
              <option>Tamaño de casillero incorrecto</option>
              <option>Ya no desea utilizar el casillero</option>
              <option>Otro motivo</option>
            </select>
          </label>
          <label v-if="motivoCancelacion==='Otro motivo'" class="campo"><span>Explica brevemente</span>
            <textarea v-model="detalleCancelacion" maxlength="160" required :disabled="procesandoCancelacion" />
          </label>
          <label class="inicio-cliente__confirmacion"><input v-model="confirmarCancelacion" type="checkbox" :disabled="procesandoCancelacion"/> Confirmo que deseo cancelar esta reserva.</label>
          <div class="inicio-cliente__cancelacion-acciones">
            <button type="button" class="inicio-cliente__cancelacion-boton" :disabled="procesandoCancelacion" @click="restablecerCancelacion">Volver</button>
            <button type="submit" class="inicio-cliente__confirmar-cancelacion" :disabled="procesandoCancelacion||!confirmarCancelacion||!motivoCancelacion||(motivoCancelacion==='Otro motivo'&&!detalleCancelacion.trim())">
              {{ procesandoCancelacion?'Cancelando…':'Confirmar cancelación' }}
            </button>
          </div>
        </form>
      </div>
    </section>
    <p v-if="avisoReserva" class="inicio-cliente__aviso" role="status">{{ avisoReserva }}</p>
    <p v-if="errorReservas" class="inicio-cliente__error" role="alert">
      {{ errorReservas }} <RouterLink to="/mis-reservas">Consultar mis reservas</RouterLink>
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

.inicio-cliente__siguiente{display:grid;gap:0;border:1px solid #00b9c7;border-radius:18px;background:var(--surface);overflow:hidden;box-shadow:0 4px 17px rgb(0 185 199 / 8%);}
.inicio-cliente__siguiente-principal{display:grid;grid-template-columns:auto minmax(0,1fr) auto;align-items:center;gap:clamp(12px,2vw,22px);padding:clamp(16px,2.6vw,24px);}
.inicio-cliente__siguiente-icono{display:grid;place-items:center;width:58px;height:58px;border-radius:15px;background:var(--surface-accent);color:var(--accent-text);}
.inicio-cliente__siguiente-texto{display:grid;gap:6px;min-width:0;}
.inicio-cliente__siguiente-texto h2{font-size:clamp(19px,2.7vw,26px);color:var(--text-strong);}
.inicio-cliente__siguiente-texto p{font-size:clamp(12px,1.5vw,14px);line-height:1.5;color:var(--text-muted);}
.inicio-cliente__cancelacion{display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:14px;padding:clamp(14px,2vw,19px) clamp(16px,2.6vw,24px);border-top:1px solid var(--border);background:var(--surface-soft);}
.inicio-cliente__cancelacion-texto{display:grid;gap:5px;flex:1 1 260px;}
.inicio-cliente__cancelacion-texto strong{color:var(--text-strong);font-size:14px;}
.inicio-cliente__cancelacion-texto p{color:var(--text-muted);font-size:12px;line-height:1.5;max-width:65ch;}
.inicio-cliente__cancelacion-boton{min-height:43px;padding:10px 16px;cursor:pointer;background:var(--surface);color:var(--text-strong);border:1px solid var(--border-strong);border-radius:11px;font-weight:750;font-size:12px;}
.inicio-cliente__cancelacion-boton:hover{border-color:var(--brand-accent);}
.inicio-cliente__cancelacion-formulario{display:grid;gap:12px;width:100%;max-width:590px;}
.inicio-cliente__cancelacion-formulario h3{font-size:17px;color:var(--text-strong);}
.inicio-cliente__cancelacion-formulario p{font-size:12px;color:var(--text-muted);line-height:1.5;}
.inicio-cliente__cancelacion-formulario .campo{display:grid;gap:6px;}
.inicio-cliente__cancelacion-formulario select,.inicio-cliente__cancelacion-formulario textarea{width:100%;min-height:44px;border-radius:10px;border:1px solid var(--border-strong);background:var(--surface);color:var(--text-strong);padding:9px;}
.inicio-cliente__confirmacion{display:flex;align-items:flex-start;gap:8px;font-size:12px;line-height:1.5;color:var(--text-strong);}
.inicio-cliente__cancelacion-acciones{display:flex;flex-wrap:wrap;gap:10px;}
.inicio-cliente__confirmar-cancelacion{padding:10px 18px;border-radius:11px;background:#01305c;border:1px solid #00b9c7;color:white;font-weight:800;cursor:pointer;}
.inicio-cliente__confirmar-cancelacion:disabled{opacity:.5;cursor:not-allowed;}
.inicio-cliente__aviso{font-size:13px;color:var(--text-strong);padding:12px;background:var(--surface-accent);border-radius:12px;}
.inicio-cliente__error{font-size:12px;color:var(--text-muted);padding:10px 0;}
.inicio-cliente__error a{color:var(--accent-text);font-weight:800;}
@media(max-width:760px){.inicio-cliente__siguiente-principal{grid-template-columns:auto minmax(0,1fr);}.inicio-cliente__siguiente-principal>.inicio-cliente__accion{grid-column:1/-1;width:100%;}.inicio-cliente__siguiente-icono{width:48px;height:48px;}.inicio-cliente__cancelacion-boton{width:100%;}.inicio-cliente__cancelacion-acciones .inicio-cliente__cancelacion-boton{width:auto;}}
</style>
