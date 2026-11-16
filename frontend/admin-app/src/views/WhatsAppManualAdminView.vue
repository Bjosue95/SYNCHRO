<script setup>
import { onMounted, onBeforeUnmount, ref } from "vue";
import { onBeforeRouteLeave } from 'vue-router';
import { mensajePin } from '../services/mensajePin';
const props = defineProps({ integrado: Boolean });

// Vista de operador: requiere cookie de administrador y CSRF del backend.
// Nunca registra el PIN en logs ni lo conserva en el navegador.
const usuario = ref("");
const clave = ref("");
const autenticado = ref(false);
const rol = ref("");
const operaciones = ref([]);
const error = ref("");
const aviso = ref("");
const trabajando = ref("");
const telefonos = ref({});
const comprobados = ref({});
const pinActual = ref(null);
let reloj = null;
let montado = true;

async function request(url, opciones = {}) {
  const r = await fetch(url, { credentials:"same-origin", cache:"no-store", ...opciones });
  let json = await r.json().catch(()=>null);
  if (!r.ok) {
    const e = new Error(json?.mensaje || `Solicitud rechazada (${r.status}).`);
    e.status = r.status;
    throw e;
  }
  return json;
}

async function csfr() {
  const datos = await request("/api/admin/csrf-token");
  if (!datos?.csrfToken) throw new Error("No se obtuvo el token de seguridad administrativo.");
  return datos.csrfToken;
}

async function ejecutar(url, payload) {
  const token = await csfr();
  return request(url, {
    method:"POST",
    headers:{"Content-Type":"application/json","X-CSRF-TOKEN":token},
    body: JSON.stringify(payload),
  });
}

async function iniciarSesion() {
  error.value="";
  trabajando.value="acceso";
  try {
    const datos = await request("/api/admin/login",{
      method:"POST", headers:{"Content-Type":"application/json"},
      body:JSON.stringify({nombreUsuario:usuario.value, contrasena:clave.value}),
    });
    autenticado.value=true;
    rol.value=datos?.rol || "Operador";
    clave.value="";
    await actualizar();
  } catch(e) { error.value=e.message; }
  finally { trabajando.value=""; }
}

async function comprobarSesion() {
  try {
    const datos=await request("/api/admin/mi-sesion");
    const roles=datos?.roles || [];
    if (!roles.some(x=>x==="Administrador" || x==="Operador")) {
      error.value="Necesitas un usuario con permiso Administrador u Operador.";
      return;
    }
    autenticado.value=true;
    rol.value=datos.rol || "Operador";
    await actualizar();
  } catch { autenticado.value=false; }
}

async function actualizar() {
  if (!autenticado.value || !montado) return;
  try {
    const datos=await request("/api/admin/whatsapp-manual/pendientes");
    if (montado) operaciones.value=datos?.operaciones || [];
  } catch(e) {
    if (montado && e.status===401) autenticado.value=false;
    else if(montado) error.value=e.message;
  }
}

async function confirmar(item) {
  error.value=""; aviso.value=""; trabajando.value=String(item.intentoId);
  try {
    const datos=await ejecutar("/api/admin/whatsapp-manual/confirmar",{
      intentoId:item.intentoId,
      codigoAutorizacion:item.codigoAutorizacion,
      telefonoCliente:(telefonos.value[item.intentoId]||"").trim(),
      mensajeComprobado:true,
    });
    pinActual.value={ intentoId:item.intentoId, pin:datos.pinRetiro, mensaje:mensajePin(datos), casillero:datos.codigoCasillero, telefono:datos.telefonoCliente };
    aviso.value=`Autorización confirmada y PIN generado. Copia el mensaje completo y envíalo manualmente.`;
    await actualizar();
  } catch(e) { error.value=e.message; }
  finally { trabajando.value=""; }
}

async function emitir(item) {
  error.value=""; aviso.value=""; trabajando.value=String(item.intentoId);
  try {
    const datos=await ejecutar("/api/admin/whatsapp-manual/emitir-pin",{intentoId:item.intentoId, reemplazar:false});
    pinActual.value={
      intentoId:item.intentoId, pin:datos.pinRetiro,
      mensaje:mensajePin(datos),
      casillero:datos.codigoCasillero, telefono:datos.telefonoCliente,
    };
    aviso.value="Se generó el PIN verdadero. Cópialo y envíalo por WhatsApp Business antes de salir de esta pantalla.";
    await actualizar();
  } catch(e) { error.value=e.message; }
  finally { trabajando.value=""; }
}


async function reemplazarPin(item) {
  if (!window.confirm("El código anterior quedará INVALIDADO y será reemplazado. ¿No se envió al cliente y deseas continuar?")) return;
  error.value=""; aviso.value=""; trabajando.value=String(item.intentoId);
  try {
    const datos=await ejecutar("/api/admin/whatsapp-manual/emitir-pin",{intentoId:item.intentoId, reemplazar:true});
    pinActual.value={
      intentoId:item.intentoId, pin:datos.pinRetiro,
      mensaje:mensajePin(datos),
      casillero:datos.codigoCasillero, telefono:datos.telefonoCliente,
    };
    aviso.value="PIN anterior invalidado. Copia y envía únicamente el nuevo código.";
    await actualizar();
  } catch(e) { error.value=e.message; }
  finally { trabajando.value=""; }
}

async function copiarPin() {
  if (!pinActual.value) return;
  try {
    await navigator.clipboard.writeText(pinActual.value.mensaje);
    aviso.value="Mensaje completo copiado. Envíalo manualmente a la conversación verificada.";
  } catch(e) { error.value="El navegador no pudo copiar. Selecciona el código manualmente."; }
}

async function enviado(item) {
  if (!pinActual.value || pinActual.value.intentoId!==item.intentoId) {
    error.value="El PIN se muestra una sola vez. No marques enviado si no conservaste el código.";
    return;
  }
  error.value=""; aviso.value=""; trabajando.value=String(item.intentoId);
  try {
    await ejecutar("/api/admin/whatsapp-manual/marcar-enviado",{
      intentoId:item.intentoId, envioComprobado:true,
    });
    pinActual.value=null;
    aviso.value="Envío manual declarado por el operador. La tablet podrá continuar.";
    await actualizar();
  } catch(e) { error.value=e.message; }
  finally { trabajando.value=""; }
}

async function cerrarSesion() {
  try { await ejecutar("/api/admin/logout",{}); }
  catch(e) { error.value=e.message; }
  finally { autenticado.value=false; pinActual.value=null; operaciones.value=[]; clave.value=""; }
}

async function cancelar(item) {
  if(!window.confirm(`¿Cancelar la reserva ${item.codigoCasillero}? Solo se liberará si no se depositaron pertenencias.`))return;
  error.value='';aviso.value='';trabajando.value=String(item.intentoId);
  try { await ejecutar('/api/admin/whatsapp-manual/cancelar',{intentoId:item.intentoId});if(pinActual.value?.intentoId===item.intentoId)pinActual.value=null;aviso.value='Reserva cancelada y casillero liberado con validación del servidor.';await actualizar(); }
  catch(e){error.value=e.message;}finally{trabajando.value='';}
}
function protegerSalida(evento){if(pinActual.value){evento.preventDefault();evento.returnValue='';}}
onBeforeRouteLeave(()=>!pinActual.value || window.confirm('El PIN aún no se marcó como enviado y se muestra una sola vez. ¿Salir de esta pantalla?'));

onMounted(()=>{
  comprobarSesion();
  reloj=setInterval(actualizar,5000);
  window.addEventListener('beforeunload',protegerSalida);
});
onBeforeUnmount(()=>{
  montado=false;
  clearInterval(reloj);
  window.removeEventListener('beforeunload',protegerSalida);
  pinActual.value=null;
});
</script>

<template>
  <main class="manual-admin">
    <section v-if="!props.integrado" class="encabezado">
      <span class="etiqueta">SYNCHRO · OPERACIONES</span>
      <h1>Autorizaciones por WhatsApp</h1>
      <p>Modo manual de demostración · Solo operadores autorizados.</p>
      <button v-if="autenticado" class="secundario" @click="cerrarSesion">Cerrar sesión</button>
    </section>

    <p v-if="error" class="alerta" role="alert">{{ error }}</p>
    <p v-if="aviso" class="mensaje" role="status">{{ aviso }}</p>
<section v-if="autenticado" class="resumen-operaciones" aria-label="Resumen de operaciones">
  <article class="indicador indicador-pendiente">
    <span>Solicitudes pendientes</span>
    <strong>{{ operaciones.filter(x => x.estado === "Pendiente").length }}</strong>
    <small>Por verificar</small>
  </article>

  <article class="indicador indicador-pin">
    <span>PIN pendientes de envío</span>
    <strong>{{ operaciones.filter(x => ["Reservado", "PinEmitido"].includes(x.estado)).length }}</strong>
    <small>Por completar</small>
  </article>

  <article class="indicador indicador-enviado">
    <span>PIN enviados</span>
    <strong>{{ operaciones.filter(x => x.estado === "Enviado").length }}</strong>
    <small>En espera de depósito</small>
  </article>
</section>

    <section v-if="!autenticado" class="tarjeta acceso" aria-live="polite">
  <p>Comprobando la sesión administrativa...</p>
  <RouterLink to="/login">Ir al inicio de sesión</RouterLink>
</section>

    <template v-else>
      <div class="acciones">
        <span>Sesión: {{ rol }}</span>
        <button class="secundario" @click="actualizar">Actualizar solicitudes</button>
      </div>
      <p class="ayuda">Comprueba en WhatsApp el mensaje de aceptación del cliente, identifica la solicitud correspondiente e introduce su número antes de generar el PIN.</p>
      <p v-if="operaciones.length===0" class="tarjeta">No hay autorizaciones pendientes.</p>

      <section v-for="item in operaciones" :key="item.intentoId" class="tarjeta">
        <div class="cabecera-tarjeta">
          <h2>Casillero {{ item.codigoCasillero }}</h2>
          <span class="estado" :class="'estado-' + item.estado.toLowerCase()">{{ item.estado }}</span>
        </div>
        
        <div class="datos"><span>Solicitud</span><span>{{ new Date(item.creadoUtc).toLocaleString() }}</span></div>
        <div class="datos"><span>Expira</span><span>{{ new Date(item.venceUtc).toLocaleString() }}</span></div>

        <form v-if="item.estado==='Pendiente'" @submit.prevent="confirmar(item)" class="confirmacion">
          <label>Teléfono del cliente (504 + 8 dígitos)
            <input v-model="telefonos[item.intentoId]" inputmode="tel" placeholder="504XXXXXXXX" required />
          </label>
          
          <button type="submit" :disabled="!!trabajando || !!pinActual">Confirmar mensaje y generar PIN</button>
        </form>

        <div v-else-if="item.estado==='Reservado'" class="confirmacion">
          <p>Reserva vinculada a SQL Server. Emite el PIN verdadero una única vez.</p>
          <button :disabled="!!trabajando || !!pinActual" @click="emitir(item)">Completar emisión de PIN de reserva anterior</button>
        </div>

        <div v-else-if="item.estado==='PinEmitido'" class="confirmacion">
          <p>PIN generado. Envíalo desde WhatsApp Business al número {{ item.telefonoCliente }}.</p>
          <div v-if="pinActual?.intentoId===item.intentoId" class="pin">
            <label>Mensaje listo para WhatsApp Business<textarea :value="pinActual.mensaje" readonly rows="7" aria-label="Mensaje completo con PIN real"></textarea></label>
            <button type="button" class="secundario" @click="copiarPin">Copiar mensaje completo</button>
          </div>
          <div v-else>
            <p class="precaucion">El PIN ya se emitió y no puede consultarse otra vez. Si aún no lo enviaste, puedes invalidarlo y emitir otro; nunca habrá dos PIN válidos simultáneamente.</p>
            <button type="button" class="secundario" :disabled="trabajando===String(item.intentoId)" @click="reemplazarPin(item)">Invalidar PIN anterior y emitir uno nuevo</button>
          </div>
          <button :disabled="!pinActual || pinActual.intentoId!==item.intentoId || trabajando===String(item.intentoId)" @click="enviado(item)">Ya envié el PIN por WhatsApp</button>
        </div>

        <div v-else-if="item.estado==='Enviado'" class="confirmacion">
          <p>Envío manual declarado. Esperando que el cliente termine el depósito en la tablet.</p>
        </div>
        <p v-else-if="item.estado==='Vencido'" class="precaucion">La reserva venció. Puedes cancelarla para liberar el casillero si no hay objetos dentro.</p>
        <button type="button" class="secundario" style="margin-top:16px" :disabled="!!trabajando" @click="cancelar(item)">Cancelar reserva sin depósito</button>
      </section>
    </template>
  </main>
</template>

<style scoped>
.manual-admin { max-width:1000px; margin:auto; color:#01305c; font-family:Arial,sans-serif; }
.pin label {width:100%}.pin textarea{width:100%;resize:vertical;border:0;background:#f2fbfc;color:#01305c;padding:12px;font:inherit;line-height:1.7}
.encabezado { padding:8px 0 22px; }
.etiqueta { color:#008b9e; font-size:12px; font-weight:800; letter-spacing:1.5px; }
h1 { font-size:clamp(24px,4vw,34px); margin:12px 0 8px; }
h2 { font-size:19px; margin:0; }
p { line-height:1.45; }
.tarjeta { padding:22px; margin:15px 0; border:1px solid #dce7eb; border-radius:18px; background:white; box-shadow:0 5px 20px #01305c0d; }
.cabecera-tarjeta,.datos,.acciones { display:flex; justify-content:space-between; gap:14px; align-items:center; }
.datos { flex-wrap:wrap; padding:9px 0; border-bottom:1px solid #edf2f5; }
.datos strong { letter-spacing:1px; }
.estado { border-radius:20px; padding:6px 12px; background:#e4f7fa; font-weight:700; font-size:13px; }
.confirmacion,.acceso form { display:flex; flex-direction:column; gap:13px; margin-top:17px; }
label:not(.check) { display:flex; flex-direction:column; gap:6px; font-weight:700; }
input:not([type="checkbox"]) { box-sizing:border-box; width:100%; padding:12px; background:white; color:#01305c; border:1px solid #b5cbd3; border-radius:9px; font-size:16px; }
.check { display:flex; align-items:flex-start; gap:8px; font-size:14px; }
.check input { margin-top:3px; }
button { cursor:pointer; padding:12px 16px; border:0; border-radius:9px; background:#00b9c7; color:#002b46; font-weight:800; font-size:14px; }
button:disabled { opacity:.5; cursor:not-allowed; }
button.secundario { color:#01305c; background:#e4edf2; }
.alerta,.mensaje { padding:13px 15px; border-radius:10px; }
.alerta { background:#ffe8e8; color:#8b1622; }.mensaje { background:#e2f8ee; color:#12673a; }
.precaucion { color:#945a13; font-weight:600; }
.pin { display:flex; flex-wrap:wrap; align-items:center; justify-content:space-between; gap:15px; border:2px solid #00b9c7; padding:14px; border-radius:12px; }
.pin strong { font-size:30px; letter-spacing:4px; }
.acciones { padding:5px 0; }.ayuda { font-size:14px; }

/* SYNCHRO - Administracion adaptable */

.manual-admin {
  width: min(100%, 1100px);
  max-width: 1100px;
  min-height: 100dvh;
  margin: 0 auto;
  padding: clamp(16px, 3vw, 40px);
  background: #f2f7fa;
  overflow: visible;
  overflow-wrap: anywhere;
}

.manual-admin > *,
.manual-admin .tarjeta,
.manual-admin .confirmacion,
.manual-admin .pin {
  min-width: 0;
  max-width: 100%;
}

.manual-admin .tarjeta {
  width: 100%;
  margin-bottom: 18px;
}

.manual-admin .cabecera-tarjeta,
.manual-admin .acciones {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
}

.manual-admin .cabecera-tarjeta {
  justify-content: space-between;
}

.manual-admin .datos {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 6px 14px;
  min-width: 0;
}

.manual-admin .datos > * {
  min-width: 0;
  overflow-wrap: anywhere;
}

.manual-admin .confirmacion {
  display: grid;
  gap: 12px;
}

.manual-admin .confirmacion label {
  min-width: 0;
}

.manual-admin input:not([type="checkbox"]),
.manual-admin select,
.manual-admin textarea {
  display: block;
  width: 100%;
  max-width: 100%;
  min-width: 0;
  box-sizing: border-box;
}

.manual-admin textarea {
  min-height: 150px;
  overflow-wrap: anywhere;
}

.manual-admin button {
  max-width: 100%;
  white-space: normal;
  overflow-wrap: anywhere;
}

.manual-admin .pin {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: 12px;
}

@media (max-width: 680px) {
  .manual-admin {
    padding: 16px 12px 32px;
  }

  .manual-admin h1 {
    font-size: clamp(23px, 6vw, 30px);
    line-height: 1.2;
  }

  .manual-admin h2 {
    font-size: clamp(18px, 5vw, 23px);
  }

  .manual-admin .tarjeta {
    padding: 16px;
    margin-bottom: 14px;
  }

  .manual-admin .cabecera-tarjeta {
    align-items: flex-start;
  }

  .manual-admin .datos {
    flex-direction: column;
    align-items: flex-start;
    gap: 4px;
  }

  .manual-admin .acciones {
    flex-direction: column;
    align-items: stretch;
  }

  .manual-admin button {
    width: 100%;
    min-height: 46px;
    padding: 10px 14px;
  }

  .manual-admin input:not([type="checkbox"]),
  .manual-admin textarea {
    font-size: 16px;
  }
}


/* SYNCHRO-ADMIN-DASHBOARD-V2 */

.manual-admin {
  width: min(100%, 1180px);
  padding: clamp(16px, 3vw, 36px);
  background: #f2f7fa;
  color: #01305c;
  font-family: Arial, Helvetica, sans-serif;
}

/* Encabezado institucional */
.manual-admin .encabezado {
  padding: clamp(22px, 3vw, 36px);
  margin-bottom: 24px;
  background: #01305c;
  color: #ffffff;
  border-radius: 20px;
  box-shadow: 0 12px 30px rgba(1, 48, 92, .12);
}

.manual-admin .encabezado h1 {
  color: #ffffff;
  font-size: clamp(24px, 3vw, 35px);
  margin: 8px 0;
}

.manual-admin .encabezado p {
  color: #cbe3ed;
}

.manual-admin .encabezado .etiqueta {
  color: #5ec6e8;
}

.manual-admin .encabezado button.secundario {
  background: #204766;
  color: #ffffff;
  border: 1px solid #5ec6e8;
  margin-top: 16px;
}

/* Indicadores de operaciones */
.resumen-operaciones {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 16px;
  margin-bottom: 26px;
}

.indicador {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
  padding: 22px;
  background: #ffffff;
  border: 1px solid #dfeaf0;
  border-radius: 17px;
  box-shadow: 0 5px 18px rgba(1, 48, 92, .045);
}

.indicador span {
  font-size: 14px;
  font-weight: 700;
  color: #526b7a;
}

.indicador strong {
  font-size: clamp(30px, 3.8vw, 43px);
  line-height: 1.1;
  font-weight: 800;
  font-variant-numeric: tabular-nums;
}

.indicador small {
  font-size: 12px;
  color: #718694;
}

.indicador-pendiente strong { color: #a76a1c; }
.indicador-pin strong { color: #008c99; }
.indicador-enviado strong { color: #227d5a; }

/* Solicitudes */
.manual-admin .tarjeta {
  background: #ffffff;
  border: 1px solid #dce8ee;
  border-radius: 18px;
  padding: clamp(18px, 2.3vw, 28px);
  box-shadow: 0 5px 20px rgba(1, 48, 92, .055);
  margin-bottom: 18px;
}

.manual-admin .tarjeta.acceso {
  max-width: 520px;
  margin-inline: auto;
}

.manual-admin .cabecera-tarjeta {
  padding-bottom: 14px;
  margin-bottom: 16px;
  border-bottom: 1px solid #e8f0f3;
}

.manual-admin .cabecera-tarjeta h2 {
  color: #01305c;
  font-size: clamp(19px, 2vw, 23px);
}

/* Distintivos de estado */
.manual-admin .estado {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: max-content;
  max-width: 100%;
  padding: 7px 12px;
  border-radius: 100px;
  font-size: 12px;
  font-weight: 800;
  background: #e5eef3;
  color: #315573;
}

.manual-admin .estado-pendiente {
  background: #fff3dd;
  color: #87590d;
}

.manual-admin .estado-reservado,
.manual-admin .estado-pinemitido {
  background: #e4f7fa;
  color: #006e7c;
}

.manual-admin .estado-enviado,
.manual-admin .estado-confirmado {
  background: #e3f5ed;
  color: #176d49;
}

.manual-admin .estado-vencido,
.manual-admin .estado-cancelado {
  background: #fce8e8;
  color: #a23737;
}

/* Formularios */
.manual-admin .confirmacion {
  gap: 15px;
}

.manual-admin .confirmacion input:not([type="checkbox"]),
.manual-admin .confirmacion textarea,
.manual-admin .acceso input {
  width: 100%;
  max-width: 100%;
  min-height: 48px;
  padding: 12px 14px;
  border: 1px solid #bdced8;
  border-radius: 11px;
  background: #ffffff;
  color: #01305c;
  font-size: 16px;
  outline-offset: 2px;
}

.manual-admin textarea {
  line-height: 1.6;
}

/* Acciones */
.manual-admin button {
  min-height: 46px;
  border-radius: 11px;
  padding: 10px 17px;
  font-weight: 700;
  transition: background-color .15s ease,
              transform .15s ease;
}

.manual-admin button:not(.secundario):not(:disabled) {
  background: #00b9c7;
  color: #01305c;
}

.manual-admin button.secundario {
  background: #e5f1f5;
  color: #01305c;
}

.manual-admin button:active:not(:disabled) {
  transform: translateY(1px);
}

.manual-admin button:focus-visible,
.manual-admin input:focus-visible,
.manual-admin textarea:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 2px;
}

/* Mensaje con PIN */
.manual-admin .pin {
  background: #f4fbfc;
  border-color: #00b9c7;
  border-radius: 15px;
}

.manual-admin .pin textarea {
  background: #ffffff;
}

/* Tablets y móviles */
@media (max-width: 800px) {
  .resumen-operaciones {
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 12px;
  }

  .indicador {
    padding: 17px;
  }
}

@media (max-width: 520px) {
  .manual-admin {
    padding: 12px 12px 32px;
  }

  .manual-admin .encabezado {
    padding: 22px 18px;
    border-radius: 15px;
    margin-bottom: 16px;
  }

  .resumen-operaciones {
    grid-template-columns: minmax(0, 1fr);
    gap: 10px;
    margin-bottom: 18px;
  }

  .indicador {
    padding: 14px 17px;
    gap: 3px;
  }

  .indicador strong {
    font-size: 30px;
  }

  .manual-admin .tarjeta {
    padding: 17px 14px;
    border-radius: 14px;
  }

  .manual-admin .cabecera-tarjeta {
    flex-wrap: wrap;
    gap: 10px;
  }

  .manual-admin .confirmacion button,
  .manual-admin .pin button {
    width: 100%;
  }
}

@media (prefers-reduced-motion: reduce) {
  .manual-admin button {
    transition: none;
  }
}
</style>
