<script setup>
import { ref, computed, nextTick, onBeforeUnmount } from "vue";
import { useRouter } from "vue-router";
import { useTema } from "../composables/useTema";
import {
  identificarRetiroReservaWeb,
  solicitarPagoReservaWebTablet,
  consultarPagoReservaWebTablet,
  capturarRetiroReservaWeb,
  confirmarRetiroReservaWeb
} from "../services/reservasWebTerminalApi.js";

const router=useRouter();
const { oscuro }=useTema();
const fase=ref("pin");
const pin=ref("");
const reservaId=ref(0);
const casillero=ref("");
const importe=ref(null);
const metodo=ref("Tarjeta");
const consentimiento=ref(false);
const video=ref(null);
const procesando=ref(false);
const consultando=ref(false);
const error=ref("");
const aviso=ref("");
let stream=null;
let intervalo=null;
let desmontada=false;

const precio=computed(()=>Number.isFinite(Number(importe.value))&&importe.value!==null
  ? "L "+Number(importe.value).toLocaleString("es-HN",{minimumFractionDigits:2,maximumFractionDigits:2})
  : "Importe pendiente de configurar");

function detenerIntervalo(){
  if(intervalo!==null){window.clearInterval(intervalo);intervalo=null;}
}
function detenerCamara(){
  if(stream){stream.getTracks().forEach(p=>p.stop());stream=null;}
  if(video.value)video.value.srcObject=null;
}
function salir(){
  detenerIntervalo();
  detenerCamara();
  pin.value="";
  reservaId.value=0;
  void router.replace("/menu");
}
async function identificar(){
  if(procesando.value||!/^[0-9]{6}$/.test(pin.value))return;
  procesando.value=true;error.value="";aviso.value="";
  try {
    const datos=await identificarRetiroReservaWeb(pin.value);
    if(!Number.isInteger(datos?.encomiendaId)||datos.encomiendaId<=0)
      throw new Error("El servidor no pudo identificar la encomienda.");
    reservaId.value=datos.encomiendaId;
    casillero.value=String(datos.casillero||"");
    importe.value=datos.importeHnl??null;
    fase.value=datos.pagoAdicionalRequerido?"pago":"captura";
  }catch(e){error.value=e?.message||"No fue posible validar el PIN.";}
  finally{procesando.value=false;}
}
async function solicitarPago(){
  if(procesando.value||fase.value!=="pago")return;
  procesando.value=true;error.value="";
  try {
    const r=await solicitarPagoReservaWebTablet(reservaId.value,pin.value,metodo.value);
    if(r?.pendiente!==true||r?.pagoRealProcesado!==false)
      throw new Error("El servidor no registró el cobro de prueba.");
    fase.value="espera";
    aviso.value="Solicitud enviada a Cobros pendientes. Espera la aprobación del administrador.";
    detenerIntervalo();
    intervalo=window.setInterval(()=>{void comprobarEstado();},8000);
  }catch(e){error.value=e?.message||"No se pudo solicitar la confirmación del pago.";}
  finally{procesando.value=false;}
}
async function comprobarEstado(){
  if(fase.value!=="espera"||consultando.value||desmontada)return;
  consultando.value=true;
  try {
    const r=await consultarPagoReservaWebTablet(reservaId.value);
    if(desmontada)return;
    if(r?.pagoConfirmado===true){
      detenerIntervalo();
      fase.value="captura";
      aviso.value="Pago confirmado por el administrador. Continúa con el registro de retiro.";
      error.value="";
    }
  }catch(e){if(!desmontada)error.value=e?.message||"No se pudo actualizar el estado de pago.";}
  finally{consultando.value=false;}
}
async function activarCamara(){
  if(procesando.value||!consentimiento.value)return;
  procesando.value=true;error.value="";
  try {
    if(!navigator.mediaDevices?.getUserMedia)throw new Error("Esta terminal necesita una conexión segura para usar la cámara.");
    detenerCamara();
    stream=await navigator.mediaDevices.getUserMedia({
      video:{facingMode:"user",width:{ideal:640},height:{ideal:480}},audio:false
    });
    await nextTick();
    if(!video.value)throw new Error("No se pudo iniciar la vista previa.");
    video.value.srcObject=stream;
    await video.value.play();
  }catch(e){detenerCamara();error.value=e?.message||"No se pudo iniciar la cámara.";}
  finally{procesando.value=false;}
}
async function confirmarEntrega(){
  if(procesando.value||fase.value!=="captura"||!stream||!consentimiento.value)return;
  procesando.value=true;error.value="";
  let imagenPng="";
  try {
    const v=video.value;
    if(!v||!v.videoWidth||!v.videoHeight)throw new Error("La cámara todavía no está lista.");
    const lienzo=document.createElement("canvas");
    lienzo.width=480;lienzo.height=360;
    const ctx=lienzo.getContext("2d");
    if(!ctx)throw new Error("La captura de imagen no está disponible.");
    ctx.drawImage(v,0,0,lienzo.width,lienzo.height);
    imagenPng=lienzo.toDataURL("image/png").split(",")[1]||"";
    if(!imagenPng)throw new Error("No se pudo capturar la imagen.");
    // El backend protege la imagen; se usa como evidencia, no se afirma coincidencia biométrica.
    const evidencia=await capturarRetiroReservaWeb(reservaId.value,{
      imagenPng,descriptor:null,modelo:"SYNCHRO-CAPTURA-DEMOSTRATIVA-v1",
      versionConsentimiento:"SYNCHRO-CAPTURA-DEMOSTRATIVA-2026-11"
    });
    if(!evidencia?.evidenciaId)throw new Error("El servidor no devolvió la evidencia.");
    const resultado=await confirmarRetiroReservaWeb(reservaId.value,{
      evidenciaId:evidencia.evidenciaId,pin:pin.value,entregaComprobada:true
    });
    if(resultado?.retiroConfirmado!==true)throw new Error("El retiro no fue confirmado.");
    detenerCamara();
    pin.value="";
    fase.value="completado";
    aviso.value="Retiro de encomienda confirmado.";
  }catch(e){error.value=e?.message||"No se pudo confirmar el retiro. Revisa el PIN y la captura.";}
  finally{imagenPng="";procesando.value=false;}
}
onBeforeUnmount(()=>{desmontada=true;detenerIntervalo();detenerCamara();pin.value="";});
</script>

<template>
  <main class="syn-encomienda-retirar" :class="{'tema-oscuro':oscuro}">
    <header class="syn-encomienda-retirar__cabecera">
      <button type="button" class="syn-encomienda-retirar__volver" @click="salir">Volver al menú</button>
      <span>SYNCHRO · Retiro de encomiendas</span>
    </header>
    <section class="syn-encomienda-retirar__panel">
      <img src="/iconos/09_encomienda_paquete.svg" alt="" aria-hidden="true"
        class="syn-encomienda-retirar__marca" />
      <h1>Retirar una encomienda</h1>
      <p v-if="error" class="syn-encomienda-retirar__error" role="alert">{{ error }}</p>
      <p v-if="aviso" class="syn-encomienda-retirar__aviso" role="status">{{ aviso }}</p>

      <form v-if="fase==='pin'" class="syn-encomienda-retirar__form" @submit.prevent="identificar">
        <label for="syn-encomienda-pin">PIN de retiro recibido por WhatsApp</label>
        <input id="syn-encomienda-pin" v-model="pin" type="password"
          inputmode="numeric" pattern="[0-9]{6}" maxlength="6"
          autocomplete="off" placeholder="••••••" required />
        <p>El PIN identifica una encomienda depositada en esta terminal.</p>
        <button type="submit" :disabled="procesando||!/^[0-9]{6}$/.test(pin)">
          {{ procesando?'Verificando…':'Continuar' }}
        </button>
      </form>

      <section v-else-if="fase==='pago'" class="syn-encomienda-retirar__form">
        <h2>Pago pendiente de la encomienda</h2>
        <p>Casillero {{ casillero }} · {{ precio }}</p>
        <p>Selecciona cómo deseas realizar el pago de prueba en la Tablet.</p>
        <div class="syn-encomienda-retirar__metodos" role="group" aria-label="Modalidad de pago">
          <button v-for="opcion in ['Efectivo','Tarjeta','NFC']" :key="opcion"
            type="button" class="syn-encomienda-retirar__metodo"
            :class="{elegido:metodo===opcion}" :aria-pressed="metodo===opcion"
            @click="metodo=opcion">{{ opcion==='NFC'?'NFC del celular':opcion }}</button>
        </div>
        <p>No introduzcas datos bancarios. No se cobra dinero real.</p>
        <button type="button" :disabled="procesando" @click="solicitarPago">
          {{ procesando?'Solicitando…':'Solicitar confirmación del pago' }}
        </button>
      </section>

      <section v-else-if="fase==='espera'" class="syn-encomienda-retirar__form">
        <h2>Esperando confirmación</h2>
        <p>El administrador debe revisar el pago de prueba en Cobros pendientes.</p>
        <p>Casillero {{ casillero }} · {{ precio }}</p>
        <button type="button" :disabled="consultando" @click="comprobarEstado">
          {{ consultando?'Consultando…':'Actualizar estado de pago' }}
        </button>
      </section>

      <section v-else-if="fase==='captura'" class="syn-encomienda-retirar__form">
        <h2>Registro del retiro</h2>
        <p>Casillero {{ casillero }}. El pago ya fue confirmado o se realizó anteriormente.</p>
        <p>Para registrar la entrega es necesaria una imagen de evidencia consentida y el PIN de retiro. La imagen no se utiliza aquí para afirmar una coincidencia facial.</p>
        <label class="syn-encomienda-retirar__consentimiento">
          <input v-model="consentimiento" type="checkbox" />
          Autorizo la captura de mi imagen para documentar el retiro de esta encomienda.
        </label>
        <video ref="video" autoplay playsinline muted aria-label="Vista previa de la cámara"></video>
        <button type="button" :disabled="!consentimiento||procesando" @click="activarCamara">Activar cámara</button>
        <button type="button" :disabled="!consentimiento||!stream||procesando" @click="confirmarEntrega">
          {{ procesando?'Registrando retiro…':'Confirmar retiro con PIN e imagen' }}
        </button>
      </section>

      <section v-else class="syn-encomienda-retirar__form">
        <h2>Encomienda retirada</h2>
        <p>Se confirmó la entrega y el PIN quedó consumido.</p>
        <button type="button" @click="salir">Finalizar</button>
      </section>
    </section>
  </main>
</template>

<style scoped>
.syn-encomienda-retirar {
  --local-bg:#f3f7fb;--local-fondo:#fff;--local-texto:#01305c;
  --local-sub:#496879;--local-borde:#d7e5ed;--local-suave:#e6f7f8;
  min-height:100dvh;
  padding:clamp(14px,3vw,32px);
  display:grid;align-content:start;justify-items:center;gap:24px;
  background:var(--local-bg);color:var(--local-texto);
}
.syn-encomienda-retirar.tema-oscuro {
  --local-bg:#0b1929;--local-fondo:#142b40;--local-texto:#f2fbff;
  --local-sub:#b0c8d7;--local-borde:#34536a;--local-suave:#124454;
}
.syn-encomienda-retirar__cabecera {
  width:min(100%,750px);display:flex;align-items:center;justify-content:space-between;
  gap:12px;font-size:14px;font-weight:750;
}
.syn-encomienda-retirar__volver {
  padding:12px 18px;background:var(--local-fondo);color:var(--local-texto);
  border:1px solid var(--local-borde);border-radius:12px;font:inherit;cursor:pointer;
}
.syn-encomienda-retirar__panel {
  width:min(100%,750px);padding:clamp(20px,4vw,36px);
  display:grid;justify-items:stretch;gap:20px;align-content:start;
  border:1px solid var(--local-borde);border-radius:22px;background:var(--local-fondo);
}
.syn-encomienda-retirar__marca {width:64px;height:64px;object-fit:contain;justify-self:center;}
.syn-encomienda-retirar__panel h1 {margin:0;text-align:center;font-size:clamp(24px,4vw,34px);}
.syn-encomienda-retirar__form {display:grid;gap:18px;}
.syn-encomienda-retirar__form h2{font-size:20px;margin:0;}
.syn-encomienda-retirar__form p {color:var(--local-sub);font-size:14px;line-height:1.6;margin:0;}
.syn-encomienda-retirar__form label{font-size:14px;font-weight:750;}
.syn-encomienda-retirar__form > input {
  padding:16px;border:1px solid var(--local-borde);border-radius:12px;
  background:var(--local-fondo);color:var(--local-texto);font:inherit;font-size:24px;
  letter-spacing:.16em;text-align:center;
}
.syn-encomienda-retirar__form button:not(.syn-encomienda-retirar__metodo) {
  min-height:54px;padding:12px 18px;font:inherit;font-weight:800;border:0;
  border-radius:13px;background:#01305c;color:#fff;cursor:pointer;
}
.syn-encomienda-retirar__form button:disabled{opacity:.55;cursor:not-allowed;}
.syn-encomienda-retirar__metodos{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:10px;}
.syn-encomienda-retirar__metodo {
  min-width:0;min-height:82px;border:2px solid var(--local-borde);border-radius:13px;
  background:var(--local-fondo);color:var(--local-texto);font:inherit;font-weight:750;
  cursor:pointer;
}
.syn-encomienda-retirar__metodo.elegido{border-color:#00b9c7;background:var(--local-suave);}
.syn-encomienda-retirar__consentimiento{display:flex;gap:12px;align-items:start;line-height:1.5;}
.syn-encomienda-retirar__consentimiento input{width:22px;height:22px;flex:0 0 22px;}
.syn-encomienda-retirar__form video{width:100%;max-height:360px;border-radius:14px;background:#081724;object-fit:cover;}
.syn-encomienda-retirar__error,.syn-encomienda-retirar__aviso{padding:12px 16px;border-radius:11px;}
.syn-encomienda-retirar__error{background:#ffe7e3;color:#91251b;}
.syn-encomienda-retirar__aviso{background:var(--local-suave);color:var(--local-texto);}
.syn-encomienda-retirar button:focus-visible,.syn-encomienda-retirar input:focus-visible{
  outline:3px solid #00b9c7;outline-offset:3px;
}
@media(max-width:520px){
  .syn-encomienda-retirar__cabecera{flex-direction:column;align-items:stretch;}
  .syn-encomienda-retirar__metodos{grid-template-columns:1fr;}
  .syn-encomienda-retirar__metodo{min-height:54px;}
}
</style>
