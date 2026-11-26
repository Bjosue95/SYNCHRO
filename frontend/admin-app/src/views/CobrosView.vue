<script setup>
import { onMounted, onBeforeUnmount, ref } from "vue";
import { RouterLink } from "vue-router";
import { apiAdmin } from "../services/adminApi";
import AdminIcon from "../components/AdminIcon.vue";

const cobros = ref([]);
const cargando = ref(false);
const procesando = ref("");
const error = ref("");
const aviso = ref("");
const metodos = ref({});
const comprobados = ref({});
const cobrosCarga = ref([]);
const cargandoCarga = ref(false);
const procesandoCarga = ref("");
const comprobadosCarga = ref({});
const cobrosReservas=ref([]);
const cargandoReservas=ref(false);
const procesandoReserva=ref(null);
const comprobadosReservas=ref({});
const importesReservas=ref({});
const justificacionesReservas=ref({});
const reservaConfirmada=ref(null);

function dinero(valor) {
  const numero = Number(valor);
  if (!Number.isFinite(numero)) return "—";
  return new Intl.NumberFormat("es-HN", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  }).format(numero);
}

function fechaLocal(valor) {
  if (!valor) return "—";
  const fecha = new Date(valor);
  return Number.isNaN(fecha.getTime())
    ? "—"
    : fecha.toLocaleString("es-HN");
}

async function cargar() {
  if (procesando.value) return;

  cargando.value = true;
  error.value = "";

  try {
    const datos = await apiAdmin(
      "/api/admin/whatsapp-manual/cobros/retiro/pendientes"
    );

    if (!Array.isArray(datos?.cobros)) {
      throw new Error("El backend no devolvió la lista de cobros.");
    }

    cobros.value = datos.cobros;
  } catch (e) {
    cobros.value = [];
    error.value = e?.message || "No se pudieron consultar los cobros.";
  } finally {
    cargando.value = false;
  }
}

async function confirmar(item) {
  const id = item?.cobroId;
  const metodo = metodos.value[id];

  if (!["Efectivo", "Tarjeta", "NFC"].includes(metodo)) {
    error.value = "Selecciona un método de pago válido.";
    return;
  }

  if (comprobados.value[id] !== true) {
    error.value = "Primero debes comprobar el pago simulado.";
    return;
  }

  const aceptar = window.confirm(
    `¿Confirmas que comprobaste el pago simulado de L ${dinero(item.importeHnl)} ` +
    `para el casillero ${item.codigoCasillero} mediante ${metodo}?`
  );

  if (!aceptar) return;

  error.value = "";
  aviso.value = "";
  procesando.value = id;

  try {
    const csrf = await apiAdmin("/api/admin/csrf-token");

    if (!csrf?.csrfToken) {
      throw new Error("No se obtuvo el token de seguridad.");
    }

    const respuesta = await apiAdmin(
      "/api/admin/whatsapp-manual/cobros/retiro/confirmar",
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": csrf.csrfToken
        },
        body: JSON.stringify({
          cobroId: id,
          metodoPago: metodo,
          pagoComprobado: true
        })
      }
    );

    if (respuesta?.pagoConfirmado !== true ||
        respuesta?.retiroConfirmado !== false) {
      throw new Error(
        "El servidor no confirmó el estado esperado del cobro."
      );
    }

    delete comprobados.value[id];
    delete metodos.value[id];

    aviso.value =
      `Pago simulado confirmado para ${item.codigoCasillero}. ` +
      "El casillero continúa ocupado hasta finalizar el retiro en la tablet.";

    const datos = await apiAdmin(
      "/api/admin/whatsapp-manual/cobros/retiro/pendientes"
    );

    if (!Array.isArray(datos?.cobros)) {
      throw new Error("No se pudo actualizar el listado.");
    }

    cobros.value = datos.cobros;
  } catch (e) {
    error.value = e?.message || "No se pudo confirmar el cobro.";
  } finally {
    procesando.value = "";
  }
}

async function cargarCarga(forzar = false) {
  if (procesandoCarga.value && !forzar) return;
  cargandoCarga.value = true;
  try {
    const datos = await apiAdmin("/api/admin/carga/cobros/pendientes");
    if (!Array.isArray(datos?.cobros)) {
      throw new Error("El backend no devolvió los cobros de carga.");
    }
    cobrosCarga.value = datos.cobros;
  } catch (e) {
    cobrosCarga.value = [];
    error.value = e?.message || "No se pudieron consultar los cobros de carga.";
  } finally {
    cargandoCarga.value = false;
  }
}

async function cargarReservas() {
  if(procesandoReserva.value)return;
  cargandoReservas.value=true;
  try {
    const datos=await apiAdmin("/api/admin/encomiendas/cobros/pendientes");
    if(!Array.isArray(datos?.cobros))throw new Error("La respuesta de cobros de reservas no es válida.");
    cobrosReservas.value=datos.cobros;
    for(const item of datos.cobros){
      // Siempre sincronizar la tarifa real devuelta por el servidor.
      if(item.importeHnl!=null)
        importesReservas.value[item.reservaId]=String(item.importeHnl);
      else if(importesReservas.value[item.reservaId]===undefined)
        importesReservas.value[item.reservaId]="";
    }
  }catch(e) {
    cobrosReservas.value=[];
    error.value=e?.message||"No se pudieron consultar los cobros de reservas.";
  }finally{cargandoReservas.value=false;}
}
async function confirmarReserva(item) {
  const id=item?.reservaId;
  const monto=Number(importesReservas.value[id]);
  if(!Number.isInteger(id)||id<=0||!Number.isFinite(monto)||monto<=0||monto>100000 ||
    Math.round(monto*100)!==monto*100){
    error.value="Introduce un importe válido para la reserva.";
    return;
  }
  if(item.importeHnl!=null && monto!==Number(item.importeHnl)){
    error.value="El importe debe coincidir con la tarifa configurada.";
    return;
  }
  if(item.requiereImporteManual&&!String(justificacionesReservas.value[id]||"").trim()){
    error.value="Indica por qué se utiliza un importe manual.";
    return;
  }
  if(comprobadosReservas.value[id]!==true){
    error.value="Debes confirmar expresamente la operación de prueba.";
    return;
  }
  if(!window.confirm("¿Confirmas el pago de prueba de L "+dinero(monto)+" para la reserva #"+id+
    " desde "+item.origen+"? No se ha recibido dinero real."))return;
  procesandoReserva.value=id;
  error.value="";aviso.value="";reservaConfirmada.value=null;
  try{
    const csrf=await apiAdmin("/api/admin/csrf-token");
    if(!csrf?.csrfToken)throw new Error("No se obtuvo el token de seguridad.");
    const resultado=await apiAdmin("/api/admin/encomiendas/cobros/"+id+"/confirmar",{
      method:"POST",
      headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken},
      body:JSON.stringify({
        importeHnl:monto,
        justificacionImporte:item.requiereImporteManual?String(justificacionesReservas.value[id]).trim():null,
        comprobado:true
      })
    });
    if(resultado?.estado!=="ConfirmadoManual")throw new Error("El servidor no confirmó la reserva.");
    delete comprobadosReservas.value[id];
    reservaConfirmada.value=id;
    aviso.value="Pago de prueba confirmado para la reserva #"+id+". Origen: "+item.origen+
      ". No se procesó dinero real.";
  }catch(e){error.value=e?.message||"No se pudo confirmar el cobro de reserva.";}
  finally{procesandoReserva.value=null;await cargarReservas();}
}
async function actualizarTodo() {
  await Promise.all([cargar(), cargarCarga(), cargarReservas()]);
}
function actualizarAlVolver(){
  if(document.visibilityState==="visible"&&!procesandoReserva.value&&!procesando.value&&!procesandoCarga.value)void actualizarTodo();
}

async function confirmarCarga(item) {
  const id = item?.sesionId;
  if (!id || !["Efectivo", "Tarjeta", "NFC"].includes(item.metodoPago)) {
    error.value = "El cobro no tiene un método de pago válido.";
    return;
  }
  if (comprobadosCarga.value[id] !== true) {
    error.value = "Primero debes comprobar el pago simulado.";
    return;
  }
  const aceptar = window.confirm(
    "¿Confirmas que comprobaste el pago simulado de L " +
    dinero(item.importeHnl) + " por carga en " + item.codigoEspacio +
    " mediante " + item.metodoPago + "?"
  );
  if (!aceptar) return;

  error.value = "";
  aviso.value = "";
  procesandoCarga.value = id;
  try {
    const csrf = await apiAdmin("/api/admin/csrf-token");
    if (!csrf?.csrfToken) {
      throw new Error("No se obtuvo el token de seguridad.");
    }
    const resultado = await apiAdmin(
      "/api/admin/carga/cobros/confirmar",
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": csrf.csrfToken
        },
        body: JSON.stringify({
          sesionId: id,
          metodoPago: item.metodoPago,
          pagoComprobado: true
        })
      }
    );
    if (resultado?.pagoConfirmado !== true ||
        resultado?.pagoSimulado !== true ||
        resultado?.pagoRealProcesado !== false ||
        resultado?.retiroConfirmado !== false ||
        resultado?.codigoEspacio !== item.codigoEspacio) {
      throw new Error("El servidor no confirmó el pago de carga esperado.");
    }
    delete comprobadosCarga.value[id];
    aviso.value = "Pago simulado de carga confirmado para " +
      item.codigoEspacio +
      ". La tablet continuará con el resumen y la recogida.";
    await cargarCarga(true);
  } catch (e) {
    error.value = e?.message || "No se pudo confirmar el pago de carga.";
  } finally {
    procesandoCarga.value = "";
  }
}

onMounted(()=>{
  void actualizarTodo();
  document.addEventListener("visibilitychange",actualizarAlVolver);
  window.addEventListener("focus",actualizarAlVolver);
});
onBeforeUnmount(()=>{
  document.removeEventListener("visibilitychange",actualizarAlVolver);
  window.removeEventListener("focus",actualizarAlVolver);
});
</script>

<template>
  <div class="cobros-view">
    <div class="page-heading spread-heading">
      <div>
        <p class="eyebrow">OPERACIONES · SYNCHRO</p>
        <h1>Cobros pendientes</h1>
        <p>
          Comprueba los pagos simulados de resguardo personal y carga
          de dispositivos. El retiro se completa desde la tablet.
        </p>
      </div>

      <button
        type="button"
        class="button-secondary"
        :disabled="cargando || cargandoCarga || cargandoReservas || !!procesando || !!procesandoCarga || !!procesandoReserva"
        @click="actualizarTodo"
      >
        <AdminIcon name="refresh" :size="17" />
        {{ cargando || cargandoCarga || cargandoReservas ? "Actualizando..." : "Actualizar cobros" }}
      </button>
    </div>

    <p v-if="error" class="inline-error" role="alert">
      {{ error }}
    </p>

    <p v-if="aviso" class="cobros-aviso" role="status">
      {{ aviso }}
    </p>
    <div v-if="reservaConfirmada" class="cobros-siguiente">
      <p>El pago está confirmado. Continúa en la ficha de la reserva para comprobar el consentimiento y emitir el PIN de depósito.</p>
      <RouterLink :to="{path:'/admin/encomiendas',query:{encomienda:reservaConfirmada}}" class="cobro-boton">Abrir reserva #{{ reservaConfirmada }} y gestionar PIN</RouterLink>
    </div>

    <section class="panel cobros-panel">
      <div class="cobros-encabezado">
        <div>
          <h2>Resguardo personal</h2>
          <p>Solamente operaciones pendientes del backend.</p>
        </div>
        <strong class="cobros-total">{{ cobros.length }}</strong>
      </div>

      <div v-if="cargando" class="empty-state" role="status">
        Consultando cobros pendientes...
      </div>

      <div v-else-if="cobros.length === 0" class="empty-state">
        No hay cobros pendientes de confirmación.
      </div>

      <article
        v-for="item in cobros"
        :key="item.cobroId"
        class="cobro-tarjeta"
      >
        <div class="cobro-cabecera">
          <div>
            <span class="cobro-etiqueta">CASILLERO</span>
            <h3>{{ item.codigoCasillero }}</h3>
          </div>
          <span class="cobro-estado">Pendiente</span>
        </div>

        <div class="cobro-datos">
          <div>
            <small>Importe por comprobar</small>
            <strong class="cobro-importe">
              L {{ dinero(item.importeHnl) }}
            </strong>
          </div>
          <div>
            <small>Tiempo utilizado</small>
            <strong>{{ item.minutosUtilizados }} min</strong>
          </div>
          <div>
            <small>Orden creada</small>
            <strong>{{ fechaLocal(item.creadoUtc) }}</strong>
          </div>
        </div>

        <p class="cobro-tarifa">
          {{ item.descripcionTarifa }}
        </p>

        <div class="cobro-confirmacion">
          <label :for="'metodo-' + item.cobroId">
            Método de pago comprobado
          </label>

          <select
            :id="'metodo-' + item.cobroId"
            v-model="metodos[item.cobroId]"
            :disabled="!!procesando"
          >
            <option value="">Seleccionar método</option>
            <option value="Efectivo">Efectivo (simulado)</option>
            <option value="Tarjeta">Tarjeta (simulado)</option>
            <option value="NFC">NFC del celular (simulado)</option>
          </select>

          <label class="cobro-check">
            <input
              v-model="comprobados[item.cobroId]"
              type="checkbox"
              :disabled="!!procesando"
            />
            <span>
              Confirmo que he comprobado el pago simulado
              correspondiente a esta orden.
            </span>
          </label>

          <button
            type="button"
            class="cobro-boton"
            :disabled="
              !!procesando ||
              cargando ||
              !metodos[item.cobroId] ||
              comprobados[item.cobroId] !== true
            "
            @click="confirmar(item)"
          >
            <AdminIcon name="check" :size="18" />
            {{
              procesando === item.cobroId
                ? "Confirmando..."
                : "Confirmar pago simulado"
            }}
          </button>
        </div>
      </article>
    </section>

    <section class="panel cobros-panel cobros-carga-panel">
      <div class="cobros-encabezado">
        <div>
          <h2>Carga de dispositivos</h2>
          <p>W1/W2 inalámbrico, cable y laptop. Solo pagos simulados pendientes de aprobación.</p>
        </div>
        <strong class="cobros-total">{{ cobrosCarga.length }}</strong>
      </div>

      <div v-if="cargandoCarga" class="empty-state" role="status">
        Consultando cobros de carga...
      </div>
      <div v-else-if="cobrosCarga.length === 0" class="empty-state">
        No hay pagos de carga esperando verificación.
      </div>

      <article
        v-for="item in cobrosCarga"
        :key="item.sesionId"
        class="cobro-tarjeta"
      >
        <div class="cobro-cabecera">
          <div>
            <span class="cobro-etiqueta">ESPACIO DE CARGA</span>
            <h3>{{ item.codigoEspacio }}</h3>
          </div>
          <span class="cobro-estado">Pendiente</span>
        </div>

        <div class="cobro-datos">
          <div>
            <small>Importe por comprobar</small>
            <strong class="cobro-importe">
              L {{ dinero(item.importeHnl) }}
            </strong>
          </div>
          <div>
            <small>Tiempo utilizado</small>
            <strong>{{ item.minutosUtilizados }} min</strong>
          </div>
          <div>
            <small>Dispositivo y modalidad</small>
            <strong>{{ item.dispositivo }} · {{ item.modalidad }}</strong>
          </div>
          <div>
            <small>Orden creada</small>
            <strong>{{ fechaLocal(item.creadoUtc) }}</strong>
          </div>
        </div>

        <p class="cobro-tarifa">{{ item.descripcionTarifa }}</p>

        <div class="cobro-confirmacion">
          <label>Método elegido en la tablet</label>
          <strong class="cobro-metodo-carga">
            {{ item.metodoPago === "NFC" ? "NFC del celular" : item.metodoPago }}
            (simulado)
          </strong>

          <label class="cobro-check">
            <input
              v-model="comprobadosCarga[item.sesionId]"
              type="checkbox"
              :disabled="!!procesandoCarga"
            />
            <span>
              Confirmo que he comprobado este pago simulado de carga
              y que corresponde al importe y método mostrados.
            </span>
          </label>

          <button
            type="button"
            class="cobro-boton"
            :disabled="
              !!procesandoCarga ||
              !!procesando ||
              cargandoCarga ||
              comprobadosCarga[item.sesionId] !== true
            "
            @click="confirmarCarga(item)"
          >
            <AdminIcon name="check" :size="18" />
            {{
              procesandoCarga === item.sesionId
                ? "Confirmando..."
                : "Confirmar pago de carga"
            }}
          </button>
        </div>
      </article>
    </section>

    <section class="panel cobros-panel cobros-carga-panel" aria-labelledby="cobros-reservas-titulo">
      <div class="cobros-encabezado">
        <div>
          <h2 id="cobros-reservas-titulo">Reservas web y encomiendas</h2>
          <p>Solicitudes del cliente web o del destinatario en Tablet. Confirma primero el consentimiento de WhatsApp del remitente en Gestión de encomiendas. No se realizan movimientos bancarios reales.</p>
        </div>
        <strong class="cobros-total">{{ cobrosReservas.length }}</strong>
      </div>
      <div v-if="cargandoReservas" class="empty-state" role="status">Consultando pagos pendientes de reservas…</div>
      <div v-else-if="!cobrosReservas.length" class="empty-state">
        No hay solicitudes de pago de reservas pendientes.
      </div>
      <article v-for="item in cobrosReservas" :key="'reserva-'+item.reservaId" class="cobro-tarjeta">
        <div class="cobro-cabecera">
          <div>
            <span class="cobro-etiqueta">RESERVA #{{ item.reservaId }} · ORIGEN {{ item.origen.toUpperCase() }}</span>
            <h3>{{ item.codigoCasillero }}</h3>
          </div>
          <span class="cobro-estado">Pendiente</span>
        </div>
        <div class="cobro-datos">
          <div><small>Total por confirmar (HNL)</small>
            <strong class="cobro-importe">{{ item.importeHnl==null?'Por definir':'L '+dinero(item.importeHnl) }}</strong>
          </div>
          <div><small>Quién solicita el pago</small><strong>{{ item.pagador==='Remitente'?'Remitente':'Destinatario' }}</strong></div>
          <div><small>Origen y modalidad</small><strong>{{ item.origen }} · {{ item.metodoPago }}</strong></div>
          <div><small>Registro</small><strong>{{ fechaLocal(item.creadoUtc) }}</strong></div>
        </div>
        <div class="cobro-confirmacion">
          <p v-if="item.importeHnl!=null" class="cobro-tarifa">Total calculado desde la tarifa de la reserva. Debe coincidir con lo mostrado al usuario.</p>
          <p v-else class="cobro-tarifa">La reserva no tiene una tarifa configurada. Debes indicar y justificar el importe manual antes de confirmar.</p>
          <label :for="'reserva-importe-'+item.reservaId">Importe verificado (L)</label>
          <input :id="'reserva-importe-'+item.reservaId" v-model="importesReservas[item.reservaId]"
            type="number" min="0.01" max="100000" step="0.01"
            :readonly="!item.requiereImporteManual"
            :disabled="!!procesandoReserva" />
          <template v-if="item.requiereImporteManual">
            <label :for="'reserva-motivo-'+item.reservaId">Justificación de tarifa manual</label>
            <input :id="'reserva-motivo-'+item.reservaId"
              v-model="justificacionesReservas[item.reservaId]" type="text" maxlength="250"
              placeholder="Describe el importe utilizado" :disabled="!!procesandoReserva" />
          </template>
          <label class="cobro-check">
            <input type="checkbox" v-model="comprobadosReservas[item.reservaId]"
              :disabled="!!procesandoReserva" />
            <span>Confirmo que revisé esta operación de pago de prueba. No representa un cobro bancario real.</span>
          </label>
          <RouterLink :to="{path:'/admin/encomiendas',query:{encomienda:item.reservaId}}" class="cobro-enlace-reserva">Abrir ficha y comprobar consentimiento</RouterLink>
          <button type="button" class="cobro-boton"
            :disabled="!!procesandoReserva || comprobadosReservas[item.reservaId]!==true || cargandoReservas"
            @click="confirmarReserva(item)">
            <AdminIcon name="check" :size="18" />
            {{ procesandoReserva===item.reservaId?'Confirmando...':'Confirmar pago de reserva' }}
          </button>
        </div>
      </article>
    </section>
  </div>
</template>

<style scoped>
.cobros-siguiente {display:grid;gap:12px;padding:18px;margin:14px 0;border:1px solid #a6d5dc;border-radius:13px;background:#edf9fa;color:#01305c;}
.cobros-siguiente p {margin:0;}
.cobros-siguiente .cobro-boton {text-decoration:none;justify-self:start;}
.cobro-enlace-reserva {display:inline-block;padding:10px 0;color:#006f87;font-size:14px;font-weight:750;text-decoration:underline;text-underline-offset:3px;}

.cobros-carga-panel { margin-top: 24px; }
.cobros-carga-panel input[type="number"],
.cobros-carga-panel input[type="text"] {
  display: block;
  width: 100%;
  min-height: 46px;
  padding: 10px 12px;
  border-radius: 10px;
  border: 1px solid #c3d6e0;
  background: #fff;
  color: #01305c;
  font: inherit;
}
.cobros-carga-panel input:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 2px;
}

.cobro-metodo-carga {
  display: block;
  padding: 12px 14px;
  border-radius: 10px;
  background: #e4f7fa;
  font-weight: 800;
  color: #01305c;
}

.cobros-view {
  color: #01305c;
}

.cobros-panel {
  padding: clamp(18px, 2.5vw, 28px);
}

.cobros-encabezado,
.cobro-cabecera {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
}

.cobros-encabezado {
  padding-bottom: 20px;
  border-bottom: 1px solid #dce8ee;
}

.cobros-encabezado h2 {
  margin: 0 0 6px;
  font-size: 20px;
}

.cobros-encabezado p {
  margin: 0;
  color: #627987;
}

.cobros-total {
  padding: 10px 16px;
  background: #e4f7fa;
  color: #01305c;
  border-radius: 12px;
  font-size: 24px;
}

.cobro-tarjeta {
  margin-top: 18px;
  padding: clamp(18px, 2vw, 26px);
  background: #fff;
  border: 1px solid #dce8ee;
  border-radius: 16px;
  box-shadow: 0 5px 18px rgba(1,48,92,.045);
}

.cobro-etiqueta {
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 1.4px;
  color: #667f8e;
}

.cobro-cabecera h3 {
  margin: 5px 0 0;
  font-size: 25px;
}

.cobro-estado {
  padding: 8px 12px;
  border-radius: 100px;
  background: #fff3dd;
  color: #87590d;
  font-size: 12px;
  font-weight: 800;
}

.cobro-datos {
  display: grid;
  grid-template-columns: repeat(3,minmax(0,1fr));
  gap: 16px;
  padding: 22px 0;
}

.cobro-datos > div {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-width: 0;
}

.cobro-datos small {
  color: #647b89;
  font-size: 12px;
}

.cobro-datos strong {
  font-size: 15px;
  overflow-wrap: anywhere;
}

.cobro-datos .cobro-importe {
  font-size: 26px;
  color: #008b9e;
  font-variant-numeric: tabular-nums;
}

.cobro-tarifa {
  padding: 12px 14px;
  background: #f2f7fa;
  border-radius: 10px;
  color: #526b7a;
  font-size: 14px;
}

.cobro-confirmacion {
  display: grid;
  gap: 13px;
  margin-top: 22px;
}

.cobro-confirmacion > label:first-child {
  font-weight: 700;
}

.cobro-confirmacion select {
  width: 100%;
  min-height: 46px;
  padding: 11px 13px;
  border: 1px solid #b5cbd3;
  border-radius: 10px;
  background: #fff;
  color: #01305c;
  font: inherit;
}

.cobro-check {
  display: flex;
  align-items: flex-start;
  gap: 11px;
  padding: 15px;
  border-radius: 11px;
  background: #f2f7fa;
  font-size: 14px;
  line-height: 1.5;
  cursor: pointer;
}

.cobro-check input {
  width: 18px;
  height: 18px;
  margin-top: 2px;
  flex-shrink: 0;
  accent-color: #008b9e;
}

.cobro-boton {
  display: inline-flex;
  justify-content: center;
  align-items: center;
  gap: 9px;
  min-height: 47px;
  padding: 12px 18px;
  border: 0;
  border-radius: 11px;
  background: #00b9c7;
  color: #01305c;
  font: inherit;
  font-weight: 800;
  cursor: pointer;
}

.cobro-boton:disabled {
  opacity: .5;
  cursor: not-allowed;
}

.cobro-boton:focus-visible,
.cobro-confirmacion select:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

.cobros-aviso {
  padding: 14px 16px;
  border-radius: 11px;
  background: #e2f8ee;
  color: #12673a;
}

@media (max-width: 680px) {
  .cobros-encabezado {
    align-items: flex-start;
  }

  .cobro-datos {
    grid-template-columns: minmax(0,1fr);
  }

  .cobro-boton {
    width: 100%;
  }
}
</style>