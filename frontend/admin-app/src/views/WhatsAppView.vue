<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, ref, watch } from 'vue';
import { useRoute, RouterLink } from 'vue-router';
import { apiAdmin, operarEncomienda } from '../services/adminApi';
import { chatPinEncomienda, mensajePinEncomienda } from '../services/pinEncomiendaManual.js';
import WhatsAppManualAdminView from './WhatsAppManualAdminView.vue';
import AdminIcon from '../components/AdminIcon.vue';
import PanelEnvioPin from '../components/PanelEnvioPin.vue';

type Categoria = 'tablet' | 'remitente' | 'destinatario';
const route = useRoute();
const categoria = ref<Categoria>('tablet');
const tabletVista = ref<InstanceType<typeof WhatsAppManualAdminView> | null>(null);
const solicitudes = ref<any[]>([]);
const detalle = ref<any>(null);
const telefono = ref('');
const pinActual = ref<any>(null);
const copiado = ref(false);
const ocupado = ref(false);
const cargando = ref(false);
const actualizandoTodo = ref(false);
const tabletPendientes = ref<number | null>(null);
const encomiendasListas = ref(false);
const estadoActualizacion = ref('');
const error = ref('');
const aviso = ref('');
let reloj: ReturnType<typeof setInterval> | undefined;
let temporizadorPin: ReturnType<typeof setTimeout> | undefined;
let montado = true;

const rol = computed(() => categoria.value === 'remitente' ? 'Remitente' : 'Destinatario');
const finalidad = computed(() => categoria.value === 'remitente' ? 'deposito' : 'retiro');
const participante = computed(() => detalle.value?.[categoria.value] || null);
const pagoPermitePin = computed(() => detalle.value?.pago?.estado==='ConfirmadoManual'||detalle.value?.condiciones?.pagoDiferidoDestinatario===true);
const enPreparacion = (e: string) => e === 'Preparacion' || e === 'ListaParaDeposito';
type RolEncomienda = 'remitente' | 'destinatario';
function activosDe(tipo: RolEncomienda) {
  const rolBuscado = tipo === 'remitente' ? 'Remitente' : 'Destinatario';
  return solicitudes.value.filter(x =>
    x.servicio === 'Encomiendas' &&
    (x.claseReserva === 'Encomienda' || (tipo === 'remitente' && x.claseReserva === 'Personal')) &&
    (x.rol === rolBuscado || (tipo === 'remitente' && x.claseReserva === 'Personal' && x.rol === 'Titular')) &&
    (tipo === 'remitente' ? enPreparacion(x.estado) : x.estado === 'Depositada')
  );
}
const requiereAtencion = (x: any) =>
  x.consentimiento !== 'Confirmado' ||
  x.emision !== 'Emitido' ||
  x.entrega !== 'EnviadoManual';
const activos = computed(() =>
  categoria.value === 'tablet' ? [] : activosDe(categoria.value)
);
const pendientes = computed(() => activos.value.filter(requiereAtencion));
const solicitudesPinDeposito=computed(()=>solicitudes.value.filter((x:any)=>
  x.servicio==='Encomiendas'&&x.solicitudPinDeposito===true
  &&x.entrega!=='EnviadoManual'
  &&['Preparacion','ListaParaDeposito'].includes(x.estado)
  &&(x.pagoEstado==='ConfirmadoManual'||x.pagoDiferidoDestinatario===true)
  &&(x.rol==='Remitente'||x.rol==='Titular')));
const resumen = computed(() => ({
  pendientes: activos.value.filter(x => x.consentimiento !== 'Confirmado').length,
  porEnviar: activos.value.filter(x => x.consentimiento === 'Confirmado' && x.entrega !== 'EnviadoManual').length,
  enviados: activos.value.filter(x => x.entrega === 'EnviadoManual').length
}));
function conteoCategoria(tipo: Categoria): number | null {
  if (tipo === 'tablet') return tabletPendientes.value;
  if (!encomiendasListas.value) return null;
  return activosDe(tipo).filter(requiereAtencion).length;
}
const pinValido = computed(() => /^\d{6}$/.test(pinActual.value?.pin || ''));
const mensajePin = computed(() => pinValido.value
  ? mensajePinEncomienda(pinActual.value, detalle.value, new Date(pinActual.value.venceUtc).toLocaleString('es-HN', { timeZone:'America/Tegucigalpa' }))
  : '');
const enlacePin = computed(() => pinValido.value ? chatPinEncomienda(pinActual.value, detalle.value) : '');
const puedeConfirmar = computed(() => Boolean(participante.value?.texto &&
  telefono.value.trim() === participante.value.telefono && !ocupado.value));

function ocultarPin() {
  if (temporizadorPin) clearTimeout(temporizadorPin);
  temporizadorPin = undefined;
  if (pinActual.value?.pin) pinActual.value.pin = '';
}
function cerrarFicha() {
  ocultarPin(); pinActual.value = null; copiado.value = false;
  detalle.value = null; telefono.value = '';
  aviso.value = ''; error.value = '';
}
async function consultarTablet(): Promise<boolean> {
  try {
    const datos = await apiAdmin('/api/admin/whatsapp-manual/pendientes');
    if (!Array.isArray(datos?.operaciones))
      throw new Error('No se recibió la lista de solicitudes de Tablet.');
    if (montado) {
      // Solo cuentan las solicitudes que todavía requieren una acción del operador.
      tabletPendientes.value = datos.operaciones.filter((x: any) =>
        ['Pendiente', 'Reservado', 'PinEmitido'].includes(x.estado)
      ).length;
    }
    return true;
  } catch {
    if (montado) tabletPendientes.value = null;
    return false;
  }
}
async function actualizar(): Promise<boolean> {
  if (cargando.value || !montado) return false;
  cargando.value = true;
  try {
    const items: any[] = [];
    for (let pagina = 1; pagina <= 50; pagina++) {
      const params = new URLSearchParams({
        servicio: 'Encomiendas', pagina: String(pagina), tamanoPagina: '100'
      });
      const datos = await apiAdmin('/api/admin/whatsapp-pin/solicitudes?' + params);
      if (!Array.isArray(datos?.solicitudes))
        throw new Error('No se recibió la lista de encomiendas.');
      items.push(...datos.solicitudes);
      const paginasTotales = Number(datos.totalPaginas || 1);
      if (pagina >= paginasTotales) break;
      if (pagina === 50)
        throw new Error('Hay más de 5000 solicitudes; ajusta la consulta para obtener un recuento completo.');
    }
    if (montado) {
      solicitudes.value = items;
      encomiendasListas.value = true;
      error.value = '';
    }
    return true;
  } catch (e) {
    if (montado) {
      encomiendasListas.value = false;
      error.value = (e as Error).message;
    }
    return false;
  } finally {
    cargando.value = false;
  }
}
async function actualizarTodo(mostrarEstado = true) {
  if (!montado || actualizandoTodo.value) return;
  actualizandoTodo.value = true;
  if (mostrarEstado) estadoActualizacion.value = 'Consultando Tablet, Depósito web y Destinatario…';
  try {
    const [encomiendasOk, tabletOk] = await Promise.all([actualizar(), consultarTablet()]);
    if (categoria.value === 'tablet') await tabletVista.value?.actualizar();
    if (mostrarEstado && montado) {
      if (encomiendasOk && tabletOk) {
        const total = (conteoCategoria('tablet') ?? 0) +
          (conteoCategoria('remitente') ?? 0) +
          (conteoCategoria('destinatario') ?? 0);
        const hora = new Date().toLocaleTimeString('es-HN', { hour: '2-digit', minute: '2-digit' });
        estadoActualizacion.value =
          `Actualizado a las ${hora}. ${total} solicitud${total === 1 ? '' : 'es'} pendiente${total === 1 ? '' : 's'}.`;
      } else {
        estadoActualizacion.value = 'No se pudieron actualizar todas las categorías. Comprueba la conexión y tu sesión.';
      }
    }
  } finally {
    actualizandoTodo.value = false;
  }
}
function actualizarVista() { void actualizarTodo(); }
async function atenderPinSolicitado(id:number) {
  if(ocupado.value)return;
  cerrarFicha();
  categoria.value='remitente';
  await abrir(id);
}
function cambiarCategoria(nueva: Categoria) {
  if (categoria.value === nueva) return;
  cerrarFicha();
  categoria.value = nueva;
  // No se borran las solicitudes de la otra pestaña; los contadores son compartidos.
  void actualizarTodo(false);
}
async function abrir(id: number) {
  if (ocupado.value) return;
  cerrarFicha(); ocupado.value = true;
  try {
    const datos = await apiAdmin(`/api/admin/encomiendas/operativas/${id}`);
    if ((datos.reserva?.servicio === 'Personal' && categoria.value === 'destinatario') ||
      (categoria.value === 'remitente' && !enPreparacion(datos.estado)) ||
      (categoria.value === 'destinatario' && datos.estado !== 'Depositada'))
      throw new Error('Esta autorización no está disponible en esta etapa.');
    detalle.value = datos;
    telefono.value = participante.value?.telefono || '';
  } catch (e) { error.value = (e as Error).message; }
  finally { ocupado.value = false; }
}
async function recargar() {
  if (!detalle.value) return;
  detalle.value = await apiAdmin(`/api/admin/encomiendas/operativas/${detalle.value.id}`);
  await actualizar();
}
async function ejecutar(accion: () => Promise<void>) {
  if (ocupado.value) return;
  ocupado.value = true; error.value = ''; aviso.value = '';
  try { await accion(); }
  catch (e) { error.value = (e as Error).message; }
  finally { ocupado.value = false; }
}
async function generarPin() {
  if (!detalle.value || participante.value?.consentimiento !== 'Confirmado') return;
  if (!pagoPermitePin.value)
    throw new Error('Primero debe confirmarse el pago del remitente.');
  if (categoria.value === 'destinatario' && (detalle.value.estado !== 'Depositada' || !detalle.value.usoId))
    throw new Error('El destinatario solo puede autorizarse después del depósito.');
  const anterior = detalle.value.pines?.find((p: any) => p.finalidad === (categoria.value === 'remitente' ? 'Deposito' : 'Retiro'));
  if (anterior && !window.confirm('Se invalidará el PIN anterior. ¿Confirmas que necesitas reemplazarlo?')) return;
  const pin = await operarEncomienda(`/${detalle.value.id}/pines/${finalidad.value}/emitir`, { reemplazar: Boolean(anterior) });
  ocultarPin(); pinActual.value = { ...pin }; copiado.value = false;
  temporizadorPin = setTimeout(ocultarPin, 60000);
  await recargar();
  aviso.value = 'PIN generado. Cópialo y envíalo manualmente al teléfono autorizado.';
}
async function confirmarMensaje() {
  if (!detalle.value || !puedeConfirmar.value) return;
  // El operador declara que comprobó en WhatsApp el texto y número mostrados.
  // La referencia es INTERNA; no se inventa un identificador de Meta/WhatsApp.
  await ejecutar(async () => {
    await operarEncomienda(`/${detalle.value.id}/consentimientos/${categoria.value}/confirmar`, {
      telefono: telefono.value.trim(), codigo: participante.value.codigo || '',
      textoMensaje: participante.value.texto,
      mensajeReferencia: 'DeclaracionManual:' + crypto.randomUUID(),
      mensajeComprobado: true,
      aceptaEncomienda: categoria.value === 'destinatario'
    });
    await recargar();
    aviso.value = 'Confirmación manual registrada por el administrador.';
    if (pagoPermitePin.value) await generarPin();
  });
}
function emitir() { return ejecutar(generarPin); }
async function copiar() {
  if (!mensajePin.value) return;
  try {
    await navigator.clipboard.writeText(mensajePin.value);
    copiado.value = true;
    aviso.value = 'Mensaje copiado. Envíalo manualmente al número autorizado.';
  } catch { error.value = 'No se pudo copiar el mensaje.'; }
}
async function enviado() {
  if (!pinActual.value || !copiado.value || !detalle.value) return;
  if (!window.confirm(`¿Ya enviaste el PIN al número ${pinActual.value.telefono}?`)) return;
  await ejecutar(async () => {
    await operarEncomienda(`/${detalle.value.id}/pines/entrega`, {
      pinId: pinActual.value.id, telefono: pinActual.value.telefono,
      mensajeReferencia: '', envioComprobado: true
    });
    ocultarPin(); pinActual.value = null; copiado.value = false;
    await recargar();
    aviso.value = 'Envío manual declarado y registrado.';
  });
}
async function abrirDesdeEnlace(id: number) {
  if (id <= 0 || !Number.isSafeInteger(id)) return;
  try {
    const datos = await apiAdmin(`/api/admin/encomiendas/operativas/${id}`);
    categoria.value = datos.estado === 'Depositada' ? 'destinatario' : 'remitente';
    await actualizar(); await abrir(id);
  } catch (e) { error.value = (e as Error).message; }
}
function ocultarAlSalir() { if (document.hidden) ocultarPin(); }
onMounted(() => {
  document.addEventListener('visibilitychange', ocultarAlSalir);
  const id = Number(route.query.encomienda);
  void actualizarTodo(false);
  if (Number.isSafeInteger(id) && id > 0) void abrirDesdeEnlace(id);
  reloj = setInterval(() => {
    if (!detalle.value && document.visibilityState === 'visible') void actualizarTodo(false);
  }, 10000);
});
onBeforeUnmount(() => {
  montado = false;
  document.removeEventListener('visibilitychange', ocultarAlSalir);
  if (reloj) clearInterval(reloj);
  ocultarPin();
});
watch(() => route.query.encomienda, valor => {
  const id = Number(valor);
  if (Number.isSafeInteger(id) && id > 0) void abrirDesdeEnlace(id);
});
</script>

<template>
  <div class="centro-whatsapp wa-shell">
    <header class="page-heading spread-heading wa-hero">
      <div class="wa-hero-copy">
        <p class="eyebrow">SYNCHRO · OPERACIONES</p>
        <h1>WhatsApp y PIN</h1>
        <p>Gestiona las autorizaciones y el envío manual de PIN para cada servicio.</p>
      </div>
      <button class="button-secondary wa-refresh" type="button"
        :disabled="actualizandoTodo || cargando || ocupado" @click="actualizarVista">
        <AdminIcon name="refresh" :size="17" />
        {{ actualizandoTodo ? 'Actualizando...' : 'Actualizar' }}
      </button>
    </header>

    <section v-if="solicitudesPinDeposito.length" class="wa-solicitudes-pin" aria-label="Solicitudes de PIN de depósito desde la web">
      <h2>PIN de depósito solicitados desde la web</h2>
      <p>Gestiona los PIN solicitados para reservas pagadas o con pago diferido del destinatario. Registra la entrega real por WhatsApp.</p>
      <div class="wa-solicitudes-pin__lista">
        <div v-for="item in solicitudesPinDeposito" :key="item.encomiendaId" class="wa-solicitudes-pin__fila">
          <div><strong>Reserva #{{ item.encomiendaId }}</strong><small>{{ item.claseReserva==='Personal'?'Pertenencias personales':'Encomienda' }} · {{ item.telefono }}</small></div>
          <button type="button" class="primario" :disabled="ocupado" @click="atenderPinSolicitado(item.encomiendaId)">Atender solicitud</button>
        </div>
      </div>
    </section>
    <nav class="categorias wa-tabs" aria-label="Tipo de solicitud">
      <button v-for="cat in [{id:'tablet',nombre:'Tablet'},{id:'remitente',nombre:'Depósito web'},{id:'destinatario',nombre:'Destinatario'}]"
        :key="cat.id" type="button" :class="{activo:categoria===cat.id}"
        :aria-current="categoria===cat.id ? 'page' : undefined"
        @click="cambiarCategoria(cat.id as Categoria)">
          <span>{{ cat.nombre }}</span>
          <span class="wa-tab-counter" :class="{ 'con-pendientes': (conteoCategoria(cat.id as Categoria) ?? 0) > 0 }"
            aria-hidden="true">{{ conteoCategoria(cat.id as Categoria) ?? '…' }}</span>
        </button>
    </nav>

    <p v-if="estadoActualizacion" class="wa-refresh-feedback" role="status" aria-live="polite">{{ estadoActualizacion }}</p>
    <div class="wa-surface">
    <WhatsAppManualAdminView v-if="categoria === 'tablet'" ref="tabletVista" :integrado="true"
      @actualizado="consultarTablet" />
    <section v-else class="wa-workspace seccion-encomiendas">
      <section class="resumen-operaciones" aria-label="Indicadores">
        <article class="indicador"><span>Solicitudes pendientes</span><strong>{{ resumen.pendientes }}</strong><small>Por verificar</small></article>
        <article class="indicador"><span>PIN pendientes de envío</span><strong>{{ resumen.porEnviar }}</strong><small>Por completar</small></article>
        <article class="indicador"><span>PIN enviados</span><strong>{{ resumen.enviados }}</strong><small>{{ categoria==='remitente'?'En espera de depósito':'En espera de retiro' }}</small></article>
      </section>

      <p v-if="error" class="alerta" role="alert">{{ error }}</p>
      <p v-if="aviso" class="aviso" role="status">{{ aviso }}</p>

      <template v-if="!detalle">
        <div class="barra-solicitudes">
          <strong>Sesión: Administrador · {{ categoria === 'remitente' ? 'Depósitos web (titulares y remitentes)' : 'Destinatarios' }}</strong>
        </div>
        <p class="ayuda">{{ categoria === 'remitente'
          ? 'Comprueba el mensaje de aceptación del remitente antes de generar su PIN de depósito.'
          : 'El destinatario solamente se autoriza después del depósito confirmado.' }}</p>
        <p v-if="cargando" class="tarjeta" role="status">Consultando solicitudes…</p>
        <p v-else-if="pendientes.length === 0" class="tarjeta">No hay autorizaciones pendientes.</p>
        <article v-for="item in pendientes" :key="item.operacionId+'-'+item.rol" class="tarjeta">
          <div class="encabezado">
            <h2>{{ categoria === 'remitente' ? 'PIN para depósito' : 'Autorizar retiro' }}</h2>
            <span class="estado">{{ item.solicitudPinDeposito ? 'PIN solicitado en la web' : item.consentimiento !== 'Confirmado' ? 'Pendiente' : 'PIN por enviar' }}</span>
          </div>
          <p><strong>Operación interna:</strong> #{{ item.encomiendaId }}</p>
          <p v-if="item.claseReserva==='Personal'"><strong>Servicio:</strong> Pertenencias personales</p>
          <p><strong>WhatsApp:</strong> {{ item.telefono || 'Sin comprobar' }}</p>
          <button class="primario" type="button" :disabled="ocupado" @click="abrir(item.encomiendaId)">Atender solicitud</button>
        </article>
      </template>

      <article v-else class="tarjeta ficha">
        <p v-if="detalle.historial?.some((h:any)=>h.accion==='PinDepositoSolicitado') && !detalle.pines?.some((p:any)=>p.finalidad==='Deposito'&&p.estado==='Entregado')" class="aviso" role="status">
          El titular solicitó el PIN de depósito desde la web. Emítelo y registra su entrega después de enviar el mensaje al número autorizado.
        </p>
        <div class="encabezado">
          <h2>{{ categoria === 'remitente' ? 'Autorizar depósito' : 'Autorizar retiro' }}</h2>
          <button type="button" class="secundario" :disabled="ocupado" @click="cerrarFicha();actualizar()">Volver</button>
        </div>
        <p><strong>Operación interna:</strong> #{{ detalle.id }} · {{ detalle.estado }}</p>
        <template v-if="participante?.consentimiento !== 'Confirmado'">
          <p class="info">Comprueba personalmente en WhatsApp que este mensaje llegó desde el número registrado. El sistema no comprueba WhatsApp automáticamente.</p>
          <p class="texto-esperado"><strong>Mensaje que debes haber recibido:</strong> {{ participante?.texto }}</p>
          <form class="formulario" @submit.prevent="confirmarMensaje">
            <label>Teléfono que envió el mensaje
              <input v-model.trim="telefono" type="tel" autocomplete="off" maxlength="16" required :disabled="ocupado" />
            </label>
            <p v-if="categoria==='destinatario'" class="info">Al confirmar declaras también que el destinatario aceptó recibir la encomienda.</p>
            <button class="primario" type="submit" :disabled="!puedeConfirmar">
              {{ pagoPermitePin ? 'Confirmar mensaje y generar PIN' : 'Confirmar mensaje recibido' }}
            </button>
          </form>
        </template>
        <template v-else-if="!pinActual">
          <p v-if="!pagoPermitePin" class="info">
            Mensaje confirmado. Primero registra el pago del remitente.
            <RouterLink :to="{path:'/admin/encomiendas',query:{encomienda:detalle.id}}">Gestionar pago</RouterLink>
          </p>
          <button v-else class="primario" type="button" :disabled="ocupado" @click="emitir">
            Generar PIN de {{ categoria === 'remitente' ? 'depósito' : 'retiro' }}
          </button>
        </template>
        <PanelEnvioPin v-if="pinActual"
          :titulo="categoria === 'remitente' ? 'PIN de depósito' : 'PIN de retiro'"
          :pin="pinValido ? pinActual.pin : ''" :telefono="pinActual.telefono"
          :mensaje="mensajePin" :enlace="enlacePin" :ocupado="ocupado" :copiado="copiado"
          @copiar="copiar" @enviado="enviado" @reemplazar="emitir" />

      </article>
    </section>
    </div>
  </div>
</template>

<style scoped>
/* Componente unico: tres flujos y una identidad visual compartida. */
.wa-shell {
  --wa-canvas: #eef5f8;
  --wa-card: #ffffff;
  --wa-text: #12344f;
  --wa-muted: #526c7c;
  --wa-border: #d5e5ec;
  --wa-field: #f9fcfd;
  --wa-outline: #00b9c7;
  width: 100%; min-width: 0; color: var(--wa-text);
}
.wa-shell .wa-hero { margin-bottom: 20px; }
.wa-shell .wa-hero-copy { min-width: 0; }
.wa-shell .wa-hero h1 { margin: 0 0 7px; }
.wa-shell .wa-hero .eyebrow { margin: 0 0 10px; }
.wa-shell .wa-refresh { flex-shrink: 0; white-space: nowrap; }
.wa-shell .wa-refresh :deep(svg) { flex-shrink: 0; }

.wa-shell .wa-tabs {
  display: flex; gap: 10px; flex-wrap: wrap;
  margin: 0 0 19px; padding: 6px;
  background: var(--wa-card); border: 1px solid var(--wa-border);
  border-radius: 16px; width: fit-content; max-width: 100%;
}
.wa-shell .wa-tabs button {
  min-height: 45px; padding: 10px 22px; border: 1px solid transparent;
  border-radius: 11px; background: transparent; color: var(--wa-text);
  font-size: 14px; font-weight: 750; cursor: pointer;
  display: inline-flex; align-items: center; justify-content: center; gap: 9px;
  transition: background-color .15s ease, color .15s ease;
}
.wa-shell .wa-tabs button:hover:not(.activo) { background: var(--wa-canvas); }
.wa-shell .wa-tabs button.activo { color: #01305c; background: #00b9c7; box-shadow: 0 3px 9px #00b9c71e; }
.wa-shell .wa-tab-counter {
  display: inline-flex; align-items: center; justify-content: center;
  min-width: 25px; height: 25px; padding: 0 6px;
  border-radius: 999px; background: var(--wa-canvas);
  border: 1px solid var(--wa-border); color: var(--wa-muted);
  font-size: 12px; font-weight: 800; font-variant-numeric: tabular-nums;
}
.wa-shell .wa-tab-counter.con-pendientes {
  background: #01305c; border-color: #01305c; color: #fff;
}
.wa-shell .wa-refresh-feedback {
  margin: -5px 0 15px; font-size: 13px; line-height: 1.4;
  color: var(--wa-muted);
}

/* El MISMO contenedor para Tablet, Remitente y Destinatario. */
.wa-shell .wa-surface {
  min-width: 0; padding: clamp(16px, 2.6vw, 27px);
  border: 1px solid var(--wa-border); border-radius: 19px;
  background: var(--wa-canvas);
}
.wa-shell .wa-workspace,
.wa-shell :deep(.manual-admin) {
  width: 100%; max-width: none; min-height: 0; height: auto;
  margin: 0; padding: 0; border: 0; border-radius: 0;
  background: transparent; color: var(--wa-text);
  font-family: inherit;
}
.wa-shell .wa-workspace { min-width:0; }

/* Resumen de tres indicadores exactamente igual en los tres flujos. */
.wa-shell .resumen-operaciones,
.wa-shell :deep(.manual-admin .resumen-operaciones) {
  display: grid; grid-template-columns: repeat(3,minmax(0,1fr));
  gap: 15px; margin: 0 0 24px; padding: 0;
}
.wa-shell .indicador,
.wa-shell :deep(.manual-admin .resumen-operaciones .indicador) {
  display: flex; flex-direction: column; gap: 7px;
  min-width: 0; min-height: 150px; padding: 21px;
  border-radius: 16px; border: 1px solid var(--wa-border);
  background: var(--wa-card); box-shadow: 0 6px 22px rgba(1,48,92,.055);
  color: var(--wa-text);
}
.wa-shell .indicador span,
.wa-shell :deep(.manual-admin .resumen-operaciones .indicador span) {
  color: var(--wa-muted); font-weight: 750; font-size: 13px;
}
.wa-shell .indicador strong,
.wa-shell :deep(.manual-admin .resumen-operaciones .indicador strong) {
  color: var(--wa-text); font-size: clamp(31px, 3.2vw, 41px);
  font-weight: 800; line-height: 1.15; font-variant-numeric: tabular-nums;
}
.wa-shell .indicador small,
.wa-shell :deep(.manual-admin .resumen-operaciones .indicador small) {
  color: var(--wa-muted); font-size: 12px;
}

/* Misma estructura de lista, fichas y botones. */
.wa-shell .tarjeta,
.wa-shell :deep(.manual-admin .tarjeta) {
  width: 100%; max-width: none; min-width: 0;
  padding: clamp(17px, 2vw, 23px); margin: 0 0 14px;
  border: 1px solid var(--wa-border); border-radius: 16px;
  background: var(--wa-card); color: var(--wa-text);
  box-shadow: 0 5px 20px rgba(1,48,92,.045);
  overflow-wrap: anywhere;
}
.wa-shell .barra-solicitudes, .wa-shell .encabezado, .wa-shell .acciones,
.wa-shell :deep(.manual-admin .acciones),
.wa-shell :deep(.manual-admin .cabecera-tarjeta) {
  display: flex; align-items: center; justify-content: space-between;
  flex-wrap: wrap; gap: 12px;
}
.wa-shell .barra-solicitudes { margin-bottom: 15px; }
.wa-shell .encabezado { padding-bottom: 14px; border-bottom: 1px solid var(--wa-border); }
.wa-shell .encabezado h2,
.wa-shell :deep(.manual-admin .cabecera-tarjeta h2) {
  margin:0; color: var(--wa-text); font-size: 18px; font-weight: 780;
}
.wa-shell :deep(.manual-admin .cabecera-tarjeta),
.wa-shell :deep(.manual-admin .datos) { border-color: var(--wa-border); }
.wa-shell :deep(.manual-admin .datos),
.wa-shell :deep(.manual-admin .ayuda),
.wa-shell :deep(.manual-admin .tarjeta p),
.wa-shell .ayuda, .wa-shell .tarjeta p { color: var(--wa-text); }
.wa-shell .ayuda { font-size: 14px; line-height: 1.55; }
.wa-shell .tarjeta p { line-height: 1.6; }
.wa-shell .estado,
.wa-shell :deep(.manual-admin .estado) {
  padding: 6px 12px; border-radius: 100px;
  background: #e7f3f7; color: #285c70;
  font-size: 12px; font-weight: 750;
}

.wa-shell .primario, .wa-shell .secundario,
.wa-shell :deep(.manual-admin button) {
  display: inline-flex; justify-content: center; align-items: center;
  min-height: 45px; max-width:100%; padding: 10px 17px;
  border: 1px solid transparent; border-radius: 11px;
  font: inherit; font-size: 13px; font-weight: 750;
  text-decoration: none; cursor: pointer;
}
.wa-shell .primario,
.wa-shell :deep(.manual-admin button:not(.secundario)) {
  background: #00b9c7; color: #01305c;
  border-color: #00b9c7;
}
.wa-shell .secundario,
.wa-shell :deep(.manual-admin button.secundario) {
  background: var(--wa-card); color: var(--wa-text);
  border: 1px solid var(--wa-border);
}
.wa-shell button:disabled,
.wa-shell :deep(.manual-admin button:disabled) { opacity:.55; cursor:not-allowed; }
.wa-shell .formulario { display:grid; gap: 14px; margin: 18px 0; }
.wa-shell .formulario label,
.wa-shell :deep(.manual-admin label) { display:grid; gap: 7px; font-weight: 700; color: var(--wa-text); }
.wa-shell .formulario input, .wa-shell .pin-panel textarea,
.wa-shell :deep(.manual-admin input:not([type="checkbox"])),
.wa-shell :deep(.manual-admin textarea) {
  box-sizing:border-box; display:block; width:100%; min-width:0;
  padding: 12px 14px; border:1px solid var(--wa-border); border-radius:11px;
  background:var(--wa-field); color:var(--wa-text); font:inherit;
}
.wa-shell .info, .wa-shell .texto-esperado,
.wa-shell :deep(.manual-admin .pin) {
  padding: 13px; border: 1px solid var(--wa-border); border-radius: 12px;
  background: var(--wa-card); color:var(--wa-text); line-height: 1.55;
}
.wa-shell .alerta, .wa-shell .aviso,
.wa-shell :deep(.manual-admin .alerta),
.wa-shell :deep(.manual-admin .mensaje) { padding: 13px 15px; border-radius: 11px; }
.wa-shell .alerta, .wa-shell :deep(.manual-admin .alerta) { background:#ffeded; color:#8d2733; }
.wa-shell .aviso, .wa-shell :deep(.manual-admin .mensaje) { background:#e2f8ee; color:#12673a; }
.wa-shell .numero-pin { font-size: 2.1rem; letter-spacing:.16em; font-weight:800; }
.wa-shell .pin-panel { padding-top:17px; margin-top:19px; border-top:1px solid var(--wa-border); }
.wa-shell .pin-panel textarea { resize:vertical; }
.wa-shell .acciones { justify-content: flex-start; margin: 14px 0; }

/* El tema cambia en el contenedor común, jamás por pestaña. */
:global(:root[data-theme="oscuro"]) .wa-shell {
  --wa-canvas: #0d202e; --wa-card:#172e3e; --wa-text:#eaf5f9;
  --wa-muted:#b5cbd6; --wa-border:#385364; --wa-field:#122837;
}
@media (prefers-color-scheme: dark) {
  :global(:root:not([data-theme="claro"])) .wa-shell {
    --wa-canvas: #0d202e; --wa-card:#172e3e; --wa-text:#eaf5f9;
    --wa-muted:#b5cbd6; --wa-border:#385364; --wa-field:#122837;
  }
}
@media (max-width:850px) {
  .wa-shell .resumen-operaciones,
  .wa-shell :deep(.manual-admin .resumen-operaciones) { grid-template-columns: repeat(2,minmax(0,1fr)); }
}
@media (max-width:590px) {
  .wa-shell .wa-hero { align-items: flex-start; }
  .wa-shell .wa-tabs { width:100%; }
  .wa-shell .wa-tabs button { flex:1; padding:9px 8px; }
  .wa-shell .wa-surface { padding:14px; }
  .wa-shell .resumen-operaciones,
  .wa-shell :deep(.manual-admin .resumen-operaciones) { grid-template-columns:1fr; }
  .wa-shell .indicador,
  .wa-shell :deep(.manual-admin .resumen-operaciones .indicador) { min-height:110px; }
  .wa-shell .acciones,
  .wa-shell :deep(.manual-admin .acciones) { align-items:stretch; }
  .wa-shell .acciones > *,
  .wa-shell :deep(.manual-admin .acciones > button) { width:100%; }
}
@media (prefers-reduced-motion: reduce) {
  .wa-shell .wa-tabs button { transition: none; }
}

.wa-solicitudes-pin{background:var(--wa-card);border:1px solid #00b9c7;border-radius:16px;padding:clamp(14px,2vw,21px);margin:0 0 18px;color:var(--wa-text);}
.wa-solicitudes-pin h2{font-size:clamp(16px,2vw,20px);margin:0 0 6px;}
.wa-solicitudes-pin p{font-size:13px;line-height:1.5;margin:0 0 14px;color:var(--wa-muted);}
.wa-solicitudes-pin__lista{display:grid;gap:9px;}
.wa-solicitudes-pin__fila{display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:10px;background:var(--wa-canvas);padding:11px;border-radius:12px;}
.wa-solicitudes-pin__fila>div{display:grid;gap:4px;min-width:0;}
.wa-solicitudes-pin__fila small{font-size:12px;color:var(--wa-muted);overflow-wrap:anywhere;}
@media(max-width:480px){.wa-solicitudes-pin__fila button{width:100%;}}
/* Tablet utiliza la misma geometría de ficha, formularios y acciones que Depósito web. */
.wa-shell :deep(.wa-tablet-integrada .barra-solicitudes) {display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:12px;margin-bottom:15px;}
.wa-shell :deep(.wa-tablet-integrada .cabecera-tarjeta) {padding-bottom:14px;margin-bottom:0;border-bottom:1px solid var(--wa-border);}
.wa-shell :deep(.wa-tablet-integrada .formulario) {display:grid;gap:14px;margin:18px 0;}
.wa-shell :deep(.wa-tablet-integrada .formulario input) {box-sizing:border-box;display:block;width:100%;min-width:0;padding:12px 14px;border:1px solid var(--wa-border);border-radius:11px;background:var(--wa-field);color:var(--wa-text);font:inherit;}
.wa-shell :deep(.wa-tablet-integrada .info),.wa-shell :deep(.wa-tablet-integrada .texto-esperado) {padding:13px;border:1px solid var(--wa-border);border-radius:12px;background:var(--wa-card);color:var(--wa-text);line-height:1.55;}
.wa-shell :deep(.wa-tablet-integrada .ayuda) {font-size:14px;line-height:1.55;}
.wa-shell :deep(.wa-tablet-integrada .pin-panel textarea) {min-height:0;}
.wa-shell :deep(.wa-tablet-integrada .pin-panel .acciones) {justify-content:flex-start;}
</style>
