<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, ref, watch } from "vue";
import { RouterLink, useRoute } from "vue-router";
import { encomiendasApi } from "../services/encomiendasApi.js";
import { fechaHonduras, nombreEstadoReserva } from "../services/formatos.js";
const route = useRoute(),
  detalle = ref<any>(null),
  error = ref(""),
  aviso = ref(""),
  cargando = ref(false),
  procesando = ref(false),
  cancelando=ref(route.query.cancelar==='1'),motivoCancelacion=ref(""),detalleCancelacion=ref(""),confirmarCancelacion=ref(false);
let reloj: ReturnType<typeof setInterval> | undefined,
  montada = true;
let consultaActual = 0;
const pinEntregado = computed(() =>
  detalle.value?.pines?.some(
    (p: any) => p.finalidad === "Deposito" && ["Entregado","Consumido"].includes(p.estado),
  ),
);
const puedeCancelar=computed(()=>detalle.value&&['Preparacion','ListaParaDeposito','Vencida','PendienteConsentimiento','Pendiente'].includes(detalle.value.estado)&&!detalle.value.depositoUtc);
const esPersonal = computed(() => detalle.value?.reserva?.servicio === 'Personal');
const depositoDisponible=computed(()=>
  Boolean(detalle.value?.condiciones?.remitenteAutorizado)&&
  (Boolean(detalle.value?.condiciones?.pagoConfirmado)||
   detalle.value?.reserva?.servicio==='Encomienda'&&
   detalle.value?.reserva?.pagador==='Destinatario'&&
   detalle.value?.reserva?.modalidadPago==='Tablet')
);
const pinRetiroEntregado=computed(()=>detalle.value?.pines?.some(
  (p:any)=>p.finalidad==='Retiro'&&['Entregado','Consumido'].includes(p.estado)
));
const etapaActual=computed(()=>{
  if(!detalle.value)return 0;
  if(detalle.value?.retiradaConfirmada)return 4;
  if(depositoConfirmado.value)return 3;
  if(pinEntregado.value)return 2;
  if(depositoDisponible.value)return 1;
  return 0;
});
const depositoConfirmado = computed(() =>
  Boolean(detalle.value?.depositoUtc) ||
  ['Depositada','Retirada'].includes(detalle.value?.estado)
);
const solicitudPinRegistrada=computed(()=>Boolean(detalle.value?.historial?.some(
  (h:any)=>h.accion==='PinDepositoSolicitado'
)));
const pinDepositoEmitido=computed(()=>Boolean(detalle.value?.pines?.some(
  (p:any)=>p.finalidad==='Deposito'&&['Emitido','Entregado','Consumido'].includes(p.estado)
)));
const puedeSolicitarPin=computed(()=>Boolean(detalle.value?.condiciones?.pagoConfirmado)
  &&Boolean(detalle.value?.condiciones?.remitenteAutorizado)
  &&!depositoConfirmado.value&&!pinDepositoEmitido.value
  &&!solicitudPinRegistrada.value
  &&!detalle.value?.historica&&!detalle.value?.cancelacion
  &&['Preparacion','ListaParaDeposito','Pendiente'].includes(detalle.value?.estado));
async function solicitarPinDeposito(){
  if(procesando.value||!puedeSolicitarPin.value)return;
  procesando.value=true;error.value='';aviso.value='';
  try {
    await encomiendasApi.solicitarPinDeposito(detalle.value.id);
    await cargar();
    aviso.value='Solicitud de PIN de depósito registrada. El equipo de SYNCHRO continuará con su emisión y entrega por WhatsApp.';
  } catch(e){
    error.value=(e as Error).message||'No fue posible registrar la solicitud del PIN.';
    await cargar();
  } finally{procesando.value=false;}
}
async function cancelar() {
  if(procesando.value||!confirmarCancelacion.value||!motivoCancelacion.value)return;
  procesando.value=true;error.value="";
  try {
    await encomiendasApi.cancelar(detalle.value.id,{motivo:motivoCancelacion.value,detalle:detalleCancelacion.value,confirmada:true,version:detalle.value.version});
    cancelando.value=false;confirmarCancelacion.value=false;
    aviso.value="Reserva cancelada. Se conserva en tu historial. Tu autorización general de WhatsApp permanece vigente.";
    await cargar();
  } catch(e){
    const mensaje=(e as Error).message || 'No se pudo cancelar la reserva.';
    await cargar();
    error.value=mensaje;
  } finally{procesando.value=false;}
}
async function cargar() {
  const numeroReserva = String(route.params.id);
  const turno = ++consultaActual;
  cargando.value = true;
  try {
    const d = await encomiendasApi.detalle(numeroReserva);
    if (montada && turno === consultaActual && String(route.params.id) === numeroReserva) {
      detalle.value = d;
      error.value = "";
    }
  } catch (e) {
    if (montada && turno === consultaActual)
      error.value = (e as Error).message || "No se pudo consultar tu reserva.";
  } finally {
    if (turno === consultaActual) cargando.value = false;
  }
}
function enlaceValido(valor: unknown): valor is string {
  return (
    typeof valor === "string" &&
    /^https:\/\/wa\.me\/[1-9][0-9]{7,14}\?text=/.test(valor)
  );
}
async function copiarMensaje() {
  try {
    await navigator.clipboard.writeText(
      detalle.value.compartir.mensaje,
    );
    aviso.value =
      "Mensaje copiado. Envíalo al destinatario para que acepte desde su propio WhatsApp. Compartirlo no registra consentimiento.";
  } catch {
    error.value =
      "No se pudo copiar. Selecciona el mensaje de invitación y cópialo manualmente.";
  }
}
watch(() => route.params.id, () => {
  detalle.value = null;
  aviso.value = "";
  error.value = "";
  cancelando.value = route.query.cancelar === '1';
  void cargar();
});
onMounted(async () => {
  await cargar();
  if(!montada)return;
  reloj = setInterval(() => {
    if (!document.hidden && !procesando.value && !cargando.value) void cargar();
  }, 15000);
});
onBeforeUnmount(() => {
  montada = false;
  consultaActual++;
  clearInterval(reloj);
});
</script>
<template>
  <section class="env-page">
    <RouterLink to="/mis-reservas" class="enlace-volver"
      >Mis reservas</RouterLink
    >
    <p v-if="error" class="alerta" role="alert">{{ error }}</p>
    <p v-if="aviso" class="mensaje-exito" role="status">{{ aviso }}</p>
    <p v-if="!detalle && cargando" role="status">Consultando la reserva…</p>
    <template v-if="detalle">
      <header class="env-heading reserva-cabecera">
        <div>
          <span class="reserva-cabecera__etiqueta">MI RESERVA</span>
          <h1>Reserva #{{ detalle.id }}</h1>
          <p>
            {{ detalle.tamano }} ·
            <span class="env-status">{{
              detalle.estado==='Retirada'&&!detalle.retiradaConfirmada?'Retiro pendiente de confirmar':esPersonal&&detalle.retiradaConfirmada?'Pertenencias retiradas':nombreEstadoReserva(detalle.estado)
            }}</span>
          </p>
        </div>
        <button
          type="button"
          class="boton boton-secundario"
          :disabled="cargando"
          @click="cargar"
        >
          Actualizar estado
        </button>
      </header>
      <section v-if="!detalle.historica && !detalle.cancelacion" class="env-card reserva-seguimiento" aria-labelledby="reserva-seguimiento-titulo">
        <div class="reserva-seguimiento__encabezado">
          <div><span class="reserva-seguimiento__etiqueta">ESTADO ACTUAL</span><h2 id="reserva-seguimiento-titulo">Progreso de tu operación</h2></div>
          <span class="reserva-seguimiento__contador">{{ Math.min(etapaActual+1,5) }} de 5</span>
        </div>
        <ol class="reserva-seguimiento__pasos">
          <li :class="{activo:etapaActual>=0,actual:etapaActual===0}"><span>1</span><strong>Reserva y pago</strong></li>
          <li :class="{activo:etapaActual>=1,actual:etapaActual===1}"><span>2</span><strong>Autorización</strong></li>
          <li :class="{activo:etapaActual>=2,actual:etapaActual===2}"><span>3</span><strong>PIN de depósito</strong></li>
          <li :class="{activo:etapaActual>=3,actual:etapaActual===3}"><span>4</span><strong>Depósito confirmado</strong></li>
          <li :class="{activo:etapaActual>=4,actual:etapaActual===4}"><span>5</span><strong>Retiro</strong></li>
        </ol>
        <p v-if="!detalle.condiciones?.pagoConfirmado && detalle.reserva?.modalidadPago==='WebTarjeta'" role="status">
          La solicitud de pago está en revisión. Al confirmarse, el administrador podrá gestionar el PIN de depósito cuando esté autorizado el WhatsApp.
        </p>
        <p v-else-if="!depositoConfirmado && !pinEntregado" role="status">
          {{ depositoDisponible ? 'Esperando que el administrador emita y entregue el PIN de depósito.' : 'Completa la autorización pendiente para continuar con el depósito.' }}
        </p>
        <p v-else-if="!depositoConfirmado" role="status">El PIN de depósito ya fue entregado al teléfono autorizado. Esperando el depósito en la estación SYNCHRO.</p>
        <p v-else-if="!esPersonal && !pinRetiroEntregado" role="status">
          El depósito está confirmado. Ahora se habilita la aceptación del destinatario; después de verificarla, el administrador podrá entregarle su PIN de retiro.
        </p>
        <p v-else-if="!esPersonal && pinRetiroEntregado" role="status">PIN de retiro entregado al teléfono autorizado del destinatario.</p>
        <p v-else role="status">Consulta la información de depósito y retiro de tus pertenencias.</p>
      </section>
      <section v-if="detalle.reserva" class="env-card reserva-resumen">
        <div class="reserva-resumen__encabezado">
          <h2>{{ detalle.reserva.servicio==='Personal'?'Pertenencias personales':'Encomienda' }}</h2>
          <span class="reserva-resumen__importe" v-if="detalle.pago?.importeHnl!=null">L {{ Number(detalle.pago.importeHnl).toFixed(2) }}</span>
        </div>
        <div class="reserva-resumen__datos">
          <div><small>Casillero</small><strong>{{ detalle.reserva.tipoCasillero }} · {{ detalle.tamano }}</strong></div>
          <div><small>Duración</small><strong>{{ detalle.reserva.minutos/60 }} {{ detalle.reserva.minutos===60?'hora':'horas' }}</strong></div>
          <div class="reserva-resumen__ubicacion"><small>Estación</small><strong>{{ detalle.reserva.ubicacion }}</strong></div>
        </div>
      </section>
      <section v-if="detalle.cancelacion" class="env-card" aria-label="Cancelación registrada">
        <h2>Reserva cancelada</h2><p>{{ fechaHonduras(detalle.cancelacion.fechaUtc) }} · {{ detalle.cancelacion.motivo }}</p>
        <p v-if="detalle.cancelacion.detalle">{{ detalle.cancelacion.detalle }}</p>
        <p v-if="detalle.pagoRequiereRevision">El pago confirmado está pendiente de revisión administrativa. No se ha realizado un reembolso.</p>
        <p class="env-help">La invitación y los PIN de esta reserva ya no permiten operar. Puedes crear otra reserva con los datos correctos.</p>
      </section>
      <section v-if="puedeCancelar" class="env-card">
        <button v-if="!cancelando" type="button" class="boton boton-secundario" :disabled="procesando" @click="cancelando=true">Cancelar reserva</button>
        <form v-else class="formulario" role="group" aria-label="Confirmar cancelación de reserva" @submit.prevent="cancelar">
          <h2>Confirmar cancelación</h2><p>Se invalidarán los PIN y autorizaciones pendientes de esta reserva. La cancelación se conservará en tu historial.</p>
          <label class="campo"><span>Motivo de cancelación</span><select v-model="motivoCancelacion" required :disabled="procesando"><option value="" disabled>Selecciona un motivo</option><option v-if="detalle.reserva?.servicio!=='Personal'">Número de destinatario incorrecto</option><option>Tamaño de casillero incorrecto</option><option>Ya no desea utilizar el casillero</option><option>Otro motivo</option></select></label>
          <label v-if="motivoCancelacion==='Otro motivo'" class="campo"><span>Explica el motivo</span><textarea v-model="detalleCancelacion" required maxlength="160" :disabled="procesando" /></label>
          <p v-if="detalle.pago?.estado==='ConfirmadoManual'">El pago quedará pendiente de revisión administrativa; no se reembolsa automáticamente.</p>
          <label class="env-check"><input v-model="confirmarCancelacion" type="checkbox" :disabled="procesando" />Confirmo que deseo cancelar esta reserva.</label>
          <div class="env-card-heading"><button class="boton boton-primario" :disabled="procesando||!confirmarCancelacion||!motivoCancelacion">Confirmar cancelación de reserva</button><button type="button" class="boton boton-secundario" :disabled="procesando" @click="cancelando=false;confirmarCancelacion=false">Volver sin cancelar</button></div>
        </form>
      </section>
      <section v-if="detalle.historica" class="env-card">
        <h2>Registro anterior</h2><dl class="env-facts"><div><dt>Creación</dt><dd>{{ fechaHonduras(detalle.creadaUtc) }}</dd></div><div><dt>Destinatario</dt><dd>{{ detalle.telefonoDestinatario }}</dd></div><div><dt>Depósito</dt><dd>{{ detalle.depositoUtc ? fechaHonduras(detalle.depositoUtc) : 'Pendiente' }}</dd></div><div><dt>Retiro confirmado</dt><dd>{{ detalle.retiradaConfirmada ? fechaHonduras(detalle.entregaUtc) : 'Retiro no confirmado' }}</dd></div></dl>
      </section>
      <template v-if="!detalle.historica && !detalle.cancelacion">
      <details class="reserva-acordeon" :open="!detalle.condiciones?.remitenteAutorizado">
        <summary><span>Personas y autorizaciones</span><span class="reserva-acordeon__estado">{{ detalle.condiciones?.remitenteAutorizado ? 'Comprobadas' : 'Acción pendiente' }}</span></summary>
        <div class="env-two-columns">
        <section
          v-for="rol in (detalle.reserva?.servicio==='Personal'?['remitente']:['remitente','destinatario'])"
          :key="rol"
          class="env-card"
        >
          <div class="env-card-heading">
            <h2>{{ detalle.reserva?.servicio==='Personal'?'Titular':rol === "remitente" ? "Remitente" : "Destinatario" }}</h2>
            <span class="env-status">{{
              nombreEstadoReserva(detalle[rol].consentimiento)
            }}</span>
          </div>
          <p class="env-phone">{{ detalle[rol].telefono }}</p>
          <p v-if="rol === 'remitente'" class="env-help">
            {{ detalle.remitente.alcance === 'EncomiendaAnterior'
              ? 'Autorización anterior válida para esta encomienda.'
              : 'Autorización de cuenta reutilizable para este teléfono hasta su revocación. Si cambias de número, debes verificarlo nuevamente.' }}
          </p>
          <p>
            Autorización de mensajes:
            {{ nombreEstadoReserva(detalle[rol].consentimiento) }}.
          </p>
          <p v-if="rol === 'destinatario'">
            <template v-if="!depositoConfirmado">
              La autorización del destinatario se habilitará cuando se confirme el depósito.
            </template>
            <template v-else>
              Aceptación de la encomienda:
              {{ detalle.destinatario.aceptaEncomienda
                ? "Comprobada por el administrador"
                : "Pendiente de comprobar por el administrador" }}.
            </template>
          </p>
          <template v-if="(rol === 'remitente' || depositoConfirmado) && detalle.estado !== 'Cancelada' && ['Pendiente', 'Revocado'].includes(detalle[rol].consentimiento)"
            ><p class="env-help">
              {{
                rol === "remitente"
                  ? "Envía SYNCHRO ACEPTO desde el teléfono registrado en tu cuenta."
                  : detalle.estado === "Depositada" ? "El destinatario puede aceptar la encomienda por WhatsApp desde su número después del depósito." : "La autorización del destinatario se habilitará después del depósito confirmado."
              }}
              Abrir el enlace no confirma el consentimiento.
            </p>
            <a
              v-if="rol === 'remitente' && enlaceValido(detalle[rol].enlaceWhatsApp)"
              :href="detalle[rol].enlaceWhatsApp"
              class="boton boton-secundario"
              target="_blank"
              rel="noopener noreferrer"
              >{{
                rol === "remitente"
                  ? "Autorizar por WhatsApp"
                  : "Enlace de aceptación del destinatario"
              }}</a
            >
            <p v-if="rol === 'remitente' && !detalle[rol].enlaceWhatsApp" class="env-help">
              El número oficial todavía no está configurado en el servidor.
            </p></template>
            <a
              v-if="
                rol === 'destinatario' && depositoConfirmado &&
                enlaceValido(detalle.compartir?.enlaceWhatsApp)
              "
              :href="detalle.compartir.enlaceWhatsApp"
              class="boton boton-secundario"
              target="_blank"
              rel="noopener noreferrer"
            >
              Compartir con destinatario
            </a>
            <template v-if="rol === 'destinatario' && depositoConfirmado && detalle.compartir?.mensaje">
              <label class="campo"><span>Invitación para el destinatario</span><textarea :value="detalle.compartir.mensaje" readonly rows="7" /></label>
              <button type="button" class="boton boton-secundario" @click="copiarMensaje">Copiar mensaje</button>
              <p class="env-help">Si WhatsApp no está disponible, copia el mensaje y compártelo manualmente. Abrir, copiar o compartir no confirma la aceptación.</p>
            </template>
          <p v-if="detalle[rol].verificadoUtc" class="env-help">
            Comprobado el {{ fechaHonduras(detalle[rol].verificadoUtc) }}.
          </p>
        </section>
      </div>
      </details>
      <details class="reserva-acordeon" :open="!detalle.condiciones?.pagoConfirmado && detalle.reserva?.modalidadPago==='WebTarjeta'">
        <summary><span>Pago de la reserva</span><span class="reserva-acordeon__estado">{{ nombreEstadoReserva(detalle.pago.estado) }}</span></summary>
      <section class="env-card reserva-acordeon__contenido">
        <div class="env-card-heading">
          <h2>Pago de la reserva</h2>
          <span class="env-status">{{
            nombreEstadoReserva(detalle.pago.estado)
          }}</span>
        </div>
        <dl class="env-facts">
          <div>
            <dt>Importe</dt>
            <dd>
              {{
                detalle.pago.importeHnl == null
                  ? "Pendiente de importe comprobado"
                  : "L " + Number(detalle.pago.importeHnl).toFixed(2)
              }}
            </dd>
          </div>
          <div>
            <dt>Método</dt>
            <dd>{{ detalle.pago.metodo || "Pendiente" }}</dd>
          </div>
          <div>
            <dt>Confirmación</dt>
            <dd>
              {{
                detalle.pago.fechaUtc
                  ? fechaHonduras(detalle.pago.fechaUtc)
                  : "Pendiente de verificación manual"
              }}
            </dd>
          </div>
        </dl>
        <p v-if="detalle.reserva?.modalidadPago==='WebTarjeta'" class="env-help">
          <strong>Origen: Web · Pagador: remitente.</strong>
          Tu solicitud de pago fue enviada a revisión. El administrador confirmará la operación antes de habilitar el siguiente paso. No se ha efectuado ningún cargo bancario.
        </p>
        <p v-else-if="detalle.reserva?.modalidadPago==='Tablet'" class="env-help">
          <strong>Origen: Tablet · Pagador: destinatario.</strong>
          El depósito puede confirmarse primero. El destinatario elegirá cómo pagar en la Tablet y el administrador deberá aprobar el pago antes de permitir el retiro.
        </p>
        <p v-else class="env-help">
          El administrador comprueba los pagos manualmente. No hay pasarela bancaria conectada.
        </p>
        <p v-if="!esPersonal&&detalle.condiciones.pagoConfirmado" class="env-help">
          El pago ya está confirmado; no se cobrará una segunda vez al retirar.
        </p>
      </section>
      </details>
      <section id="solicitar-pin-deposito" class="env-card reserva-pin">
        <h2>Tu PIN de depósito</h2>
        <div v-if="!depositoConfirmado" class="reserva-pin__accion" role="status">
          <template v-if="solicitudPinRegistrada">
            <strong>PIN solicitado</strong>
            <p>Tu solicitud está registrada. La emisión y entrega corresponden al personal autorizado de SYNCHRO.</p>
          </template>
          <template v-else-if="pinDepositoEmitido">
            <strong>PIN en preparación</strong>
            <p>Consulta tu WhatsApp autorizado para recibir el PIN de depósito.</p>
          </template>
          <template v-else-if="puedeSolicitarPin">
            <strong>Pago y WhatsApp confirmados</strong>
            <p>Ya puedes solicitar tu PIN para depositar en la estación.</p>
            <button type="button" class="boton boton-primario" :disabled="procesando" @click="solicitarPinDeposito">
              {{ procesando?'Registrando solicitud…':'Solicitar PIN de depósito' }}
            </button>
          </template>
          <template v-else-if="detalle.condiciones?.pagoConfirmado&&!detalle.condiciones?.remitenteAutorizado">
            <strong>Autoriza tu WhatsApp</strong>
            <p>Antes de solicitar el PIN, debe comprobarse tu autorización para recibir mensajes.</p>
          </template>
          <template v-else>
            <strong>Solicitud aún no disponible</strong>
            <p>Se habilitará cuando se cumplan las condiciones de tu reserva.</p>
          </template>
        </div>
        <ol class="env-checklist">
          <li>
            {{
              detalle.condiciones.remitenteAutorizado
                ? "Consentimiento de WhatsApp comprobado."
                : detalle.reserva?.servicio==='Personal' ? "Espera la comprobación manual de tu consentimiento." : "Espera la comprobación manual del consentimiento del remitente."
            }}
          </li>
          <li>
            {{
              detalle.condiciones.pagoConfirmado
                ? "Pago manual confirmado."
                : "El pago está pendiente de confirmación."
            }}
          </li>
          <li>
            {{
              pinEntregado
                ? "PIN de depósito entregado al número autorizado."
                : "Espera la entrega manual de tu PIN de depósito."
            }}
          </li>
        </ol>
        <p class="env-help">
          Usa tu PIN de depósito en la terminal SYNCHRO. El estado cambiará cuando el servidor confirme que la encomienda o tus pertenencias quedaron depositadas.
        </p>
      </section>
      <details class="reserva-acordeon" :open="depositoConfirmado && !detalle.retiradaConfirmada">
        <summary><span>Depósito y retiro</span><span class="reserva-acordeon__estado">{{ depositoConfirmado ? 'Depósito confirmado' : 'Pendiente' }}</span></summary>
      <section class="env-card reserva-acordeon__contenido">
        <h2>Depósito y retiro</h2>
        <p v-if="!esPersonal && !depositoConfirmado" class="env-help">El envío de la invitación y la entrega del PIN del destinatario permanecen deshabilitados hasta que la tablet confirme el depósito.</p>
        <p v-else-if="!esPersonal && depositoConfirmado" class="env-help">Depósito confirmado. Ya puedes compartir la invitación de WhatsApp con el destinatario. Su aceptación debe ser comprobada antes de que el administrador emita el PIN de retiro.</p>
        <p v-if="detalle.estado === 'Depositada'">
          El depósito está confirmado. El administrador puede emitir y
          entregar el PIN de retiro al teléfono autorizado.
        </p>
        <p v-else-if="detalle.retiradaConfirmada">
          La entrega está registrada y el PIN de retiro fue consumido.
        </p>
        <p v-else>
          El depósito y la entrega aparecerán aquí cuando sean confirmados por
          el servidor.
        </p>
        <ul>
          <li v-for="p in detalle.pines" :key="p.id">
            PIN de {{ p.finalidad.toLowerCase() }}:
            {{ nombreEstadoReserva(p.estado) }} · Vigencia hasta
            {{ fechaHonduras(p.venceUtc) }}.
          </li>
        </ul>
        <p class="env-help">
          Los valores de los PIN no se muestran en este historial. La autorización de retiro solo se habilita después de confirmar el depósito. La identidad y el retiro se registran durante la operación.
        </p>
      </section>
      </details>
      </template>
      <details class="reserva-acordeon">
        <summary><span>Historial de movimientos</span><span class="reserva-acordeon__estado">{{ detalle.seguimiento?.length || 0 }} eventos</span></summary>
      <section class="env-card reserva-acordeon__contenido">
        <h2>Seguimiento de tu reserva</h2>
        <ol class="env-timeline">
          <li v-for="h in detalle.seguimiento" :key="h.tipo + h.fechaUtc + h.descripcion">
            <time>{{ fechaHonduras(h.fechaUtc) }}</time>
            <div>
              <p>{{ h.descripcion }}</p>
            </div>
          </li>
        </ol>
        <p class="env-help">Eventos registrados por el servidor, en orden cronológico. El retiro solo aparece cuando está confirmado.</p>
      </section>
      </details>
    </template>
  </section>
</template>

<style scoped>

.reserva-cabecera{align-items:center;}
.reserva-cabecera__etiqueta,.reserva-seguimiento__etiqueta{font-size:10px;letter-spacing:.11em;font-weight:850;color:var(--accent-text);}
.reserva-seguimiento {display:grid;gap:clamp(12px,1.8vw,19px);padding:clamp(14px,2vw,23px);}
.reserva-seguimiento__encabezado{display:flex;justify-content:space-between;align-items:center;gap:15px;}
.reserva-seguimiento__encabezado>div{display:grid;gap:5px;}
.reserva-seguimiento__encabezado h2{font-size:clamp(18px,2.4vw,23px);}
.reserva-seguimiento__contador{white-space:nowrap;font-size:12px;font-weight:800;background:var(--surface-accent);padding:6px 10px;border-radius:99px;color:var(--text-strong);}
.reserva-seguimiento__pasos{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:clamp(3px,1.2vw,10px);list-style:none;padding:0;margin:0;}
.reserva-seguimiento__pasos li{display:flex;flex-direction:column;align-items:center;justify-content:flex-start;gap:8px;text-align:center;color:var(--text-muted);font-size:clamp(9px,1.3vw,11px);line-height:1.3;min-width:0;}
.reserva-seguimiento__pasos li span{width:clamp(25px,4.4vw,35px);height:clamp(25px,4.4vw,35px);flex:0 0 auto;display:grid;place-items:center;border:1px solid var(--border-strong);background:var(--surface-soft);color:var(--text-strong);border-radius:50%;font-weight:800;}
.reserva-seguimiento__pasos li.activo span{border-color:#00b9c7;background:var(--surface-accent);}
.reserva-seguimiento__pasos li.actual span{background:#00b9c7;border-color:#00b9c7;color:#01305c;box-shadow:0 0 0 4px rgb(0 185 199 / 15%);}
.reserva-seguimiento__pasos li.activo strong{color:var(--text-strong);}
.reserva-seguimiento>p[role="status"]{font-size:clamp(12px,1.5vw,14px);line-height:1.5;margin:0;background:var(--surface-accent);border-radius:12px;padding:12px 13px;color:var(--text-strong);}
.reserva-resumen{display:grid;gap:15px;padding:clamp(15px,2vw,23px);}
.reserva-resumen__encabezado{display:flex;align-items:center;justify-content:space-between;gap:10px;flex-wrap:wrap;}
.reserva-resumen__encabezado h2{font-size:clamp(17px,2.2vw,22px);}
.reserva-resumen__importe{font-size:clamp(17px,2.4vw,22px);font-weight:850;color:var(--text-strong);}
.reserva-resumen__datos{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:13px;}
.reserva-resumen__datos>div{display:grid;min-width:0;gap:4px;}
.reserva-resumen__datos small{color:var(--text-muted);font-size:11px;}
.reserva-resumen__datos strong{color:var(--text-strong);font-size:clamp(12px,1.6vw,15px);overflow-wrap:anywhere;}
.reserva-resumen__ubicacion{grid-column:1/-1;padding-top:10px;border-top:1px solid var(--border);}
.reserva-acordeon{border-radius:15px;border:1px solid var(--border);background:var(--surface);overflow:hidden;}
.reserva-acordeon>summary{display:flex;align-items:center;justify-content:space-between;gap:12px;cursor:pointer;list-style:none;padding:clamp(13px,2vw,18px);font-weight:800;color:var(--text-strong);font-size:clamp(13px,1.8vw,17px);}
.reserva-acordeon>summary::-webkit-details-marker{display:none;}
.reserva-acordeon>summary::before{content:'+';display:grid;place-items:center;flex:0 0 23px;width:23px;height:23px;border-radius:7px;background:var(--surface-accent);font-weight:900;color:var(--accent-text);}
.reserva-acordeon[open]>summary::before{content:'−';}
.reserva-acordeon>summary>span:first-child{margin-right:auto;}
.reserva-acordeon__estado{font-size:11px;font-weight:650;color:var(--text-muted);text-align:right;}
.reserva-acordeon>summary:focus-visible{outline:3px solid var(--ring);outline-offset:-3px;}
.reserva-acordeon>.env-two-columns,.reserva-acordeon>.reserva-acordeon__contenido{margin:0 12px 12px;}
.reserva-acordeon>.env-two-columns .env-card{min-width:0;}
.reserva-acordeon__contenido{border:0;background:var(--surface-soft);box-shadow:none;padding:15px;}
.reserva-pin{display:grid;gap:11px;}
.reserva-pin .env-checklist{margin:0;}
.reserva-pin__accion{display:grid;justify-items:start;gap:9px;padding:14px;border:1px solid var(--border);background:var(--surface-accent);border-radius:13px;}
.reserva-pin__accion strong{font-size:14px;color:var(--text-strong);}
.reserva-pin__accion p{font-size:12px;line-height:1.5;color:var(--text-muted);}
.reserva-pin__accion button{min-height:48px;padding:11px 18px;font-weight:800;}
#solicitar-pin-deposito{scroll-margin-top:80px;}
@media(max-width:600px){
 .reserva-seguimiento__pasos{grid-template-columns:repeat(5,minmax(0,1fr));gap:3px;}
 .reserva-seguimiento__pasos li{font-size:9px;}
 .reserva-seguimiento__pasos li strong{overflow-wrap:anywhere;}
 .reserva-acordeon__estado{font-size:10px;}
}
</style>
