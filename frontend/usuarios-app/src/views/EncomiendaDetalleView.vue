<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, ref } from "vue";
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
const pinEntregado = computed(() =>
  detalle.value?.pines?.some(
    (p: any) => p.finalidad === "Deposito" && p.estado === "Entregado",
  ),
);
const puedeCancelar=computed(()=>detalle.value&&['Preparacion','ListaParaDeposito','Vencida','PendienteConsentimiento','Pendiente'].includes(detalle.value.estado)&&!detalle.value.depositoUtc);
async function cancelar() {
  if(procesando.value||!confirmarCancelacion.value||!motivoCancelacion.value)return;
  procesando.value=true;error.value="";
  try {
    await encomiendasApi.cancelar(detalle.value.id,{motivo:motivoCancelacion.value,detalle:detalleCancelacion.value,confirmada:true,version:detalle.value.version});
    cancelando.value=false;confirmarCancelacion.value=false;
    aviso.value="Reserva cancelada. Se conserva en tu historial. Tu autorización general de WhatsApp permanece vigente.";
    await cargar();
  } catch(e){error.value=(e as Error).message;await cargar();}
  finally{procesando.value=false;}
}
async function cargar() {
  if (cargando.value) return;
  cargando.value = true;
  try {
    const d = await encomiendasApi.detalle(route.params.id);
    if (montada) {
      detalle.value = d;
      error.value = "";
    }
  } catch (e) {
    if (montada) error.value = (e as Error).message;
  } finally {
    cargando.value = false;
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
onMounted(async () => {
  await cargar();
  reloj = setInterval(() => {
    if (!document.hidden && !procesando.value) void cargar();
  }, 15000);
});
onBeforeUnmount(() => {
  montada = false;
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
      <header class="env-heading">
        <div>
          <h1>Reserva #{{ detalle.id }}</h1>
          <p>
            {{ detalle.tamano }} ·
            <span class="env-status">{{
              detalle.estado==='Retirada'&&!detalle.retiradaConfirmada?'Retiro pendiente de confirmar':nombreEstadoReserva(detalle.estado)
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
      <section v-if="detalle.reserva" class="env-card"><h2>{{ detalle.reserva.servicio==='Personal'?'Pertenencias personales':'Encomienda' }}</h2><p>{{ detalle.reserva.tipoCasillero }} · {{ nombreEstadoReserva(detalle.tamano) }} · {{ detalle.reserva.minutos }} minutos</p><p>{{ detalle.reserva.ubicacion }}</p></section>
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
      <template v-if="!detalle.historica">
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
            Aceptación de la encomienda:
            {{
              detalle.destinatario.aceptaEncomienda
                ? "Comprobada por el administrador"
                : "Pendiente de comprobar"
            }}.
          </p>
          <template v-if="detalle.estado !== 'Cancelada' && ['Pendiente', 'Revocado'].includes(detalle[rol].consentimiento)"
            ><p class="env-help">
              {{
                rol === "remitente"
                  ? "Envía SYNCHRO ACEPTO desde el teléfono registrado en tu cuenta."
                  : "Comparte la invitación para que el destinatario envíe su aceptación desde su propio número al WhatsApp oficial. No necesita cuenta ni iniciar sesión."
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
            <p v-if="!detalle[rol].enlaceWhatsApp" class="env-help">
              El número oficial todavía no está configurado en el servidor.
            </p></template>
            <a
              v-if="
                rol === 'destinatario' &&
                enlaceValido(detalle.compartir?.enlaceWhatsApp)
              "
              :href="detalle.compartir.enlaceWhatsApp"
              class="boton boton-secundario"
              target="_blank"
              rel="noopener noreferrer"
            >
              Compartir con destinatario
            </a>
            <template v-if="rol === 'destinatario' && detalle.compartir?.mensaje">
              <label class="campo"><span>Invitación para el destinatario</span><textarea :value="detalle.compartir.mensaje" readonly rows="7" /></label>
              <button type="button" class="boton boton-secundario" @click="copiarMensaje">Copiar mensaje</button>
              <p class="env-help">Si WhatsApp no está disponible, copia el mensaje y compártelo manualmente. Abrir, copiar o compartir no confirma la aceptación.</p>
            </template>
          <p v-if="detalle[rol].verificadoUtc" class="env-help">
            Comprobado el {{ fechaHonduras(detalle[rol].verificadoUtc) }}.
          </p>
        </section>
      </div>
      <section class="env-card">
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
        <p class="env-help">
          El administrador registra el pago efectivamente comprobado. No hay
          pasarela conectada. El destinatario no paga al retirar una encomienda
          ya pagada.
        </p>
      </section>
      <section class="env-card">
        <h2>Tu PIN de depósito</h2>
        <ol class="env-checklist">
          <li>
            {{
              detalle.condiciones.consentimientosConfirmados
                ? "Consentimiento de WhatsApp comprobado."
                : detalle.reserva?.servicio==='Personal' ? "Espera la comprobación manual de tu consentimiento." : "Espera la comprobación manual de los dos consentimientos."
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
          Usa el PIN de depósito en la tablet cuando esté integrada. La tablet hará una captura demostrativa al depositar y otra al retirar, sin comparar rostros. No debes registrar ni verificar tu rostro en la web.
        </p>
      </section>
      <section class="env-card">
        <h2>Depósito y retiro</h2>
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
          Los valores de los PIN no se muestran en este historial. La fotografía
          del destinatario constituye evidencia de retiro, sin afirmar una
          coincidencia biométrica.
        </p>
      </section>
      </template>
      <section class="env-card">
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
    </template>
  </section>
</template>
