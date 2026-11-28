<script setup lang="ts">
import { computed,onMounted,ref,nextTick } from 'vue';
import { useRouter } from 'vue-router';
import { estadoSesion,eliminarCuenta } from '../services/sesion.js';
import AppIcon from '../components/ui/AppIcon.vue';
import SynButton from '../components/ui/SynButton.vue';
import { encomiendasApi } from '../services/encomiendasApi.js';
import { fechaHonduras,nombreEstadoReserva } from '../services/formatos.js';
const consentimiento=ref<any>(null),error=ref(''),procesando=ref(false);
const perfil=computed<any>(()=>estadoSesion.cliente);
async function cargar(){try{consentimiento.value=await encomiendasApi.consentimientoCuenta();}catch(e){error.value=(e as Error).message;}}
async function revocar(){if(procesando.value)return;procesando.value=true;try{await encomiendasApi.revocarConsentimientoCuenta();await cargar();}catch(e){error.value=(e as Error).message;}finally{procesando.value=false;}}
const router=useRouter();
const confirmar=ref(false), eliminando=ref(false), clave=ref(''), errorEliminar=ref('');
const entradaClave=ref<HTMLInputElement|null>(null), botonEliminar=ref<HTMLButtonElement|null>(null);
async function abrirEliminar(){confirmar.value=true;errorEliminar.value='';await nextTick();entradaClave.value?.focus();}
async function cancelarEliminar(){if(eliminando.value)return;confirmar.value=false;clave.value='';errorEliminar.value='';await nextTick();botonEliminar.value?.focus();}
async function confirmarEliminar(){
  if(eliminando.value||!clave.value)return;
  eliminando.value=true;errorEliminar.value='';
  try{
    await eliminarCuenta(clave.value);
    await router.replace({name:'acceso',query:{cuentaEliminada:'1'}});
  }catch(e){errorEliminar.value=(e as Error).message;await nextTick();entradaClave.value?.focus();}
  finally{clave.value='';eliminando.value=false;}
}
onMounted(cargar);
</script>
<template>
  <section class="env-page"><header class="env-heading"><div><h1>Mi perfil</h1><p>Tu cuenta y la autorización de WhatsApp.</p></div></header>
    <p v-if="error" class="alerta" role="alert">{{ error }}</p>
    <section class="env-card"><h2>Datos de mi cuenta</h2><dl class="env-facts"><div><dt>Usuario</dt><dd>{{ perfil?.nombreUsuario }}</dd></div><div><dt>Teléfono</dt><dd>{{ perfil?.telefono }}</dd></div></dl></section>
    <section v-if="consentimiento" class="env-card"><h2>WhatsApp</h2><p>{{ consentimiento.telefono }} · {{ nombreEstadoReserva(consentimiento.consentimiento) }}</p>
      <p>Envía SYNCHRO ACEPTO desde el teléfono de tu cuenta. El administrador comprobará el mensaje una sola vez; la autorización sirve para tus reservas mientras esté vigente. Cambiar de teléfono requiere otra comprobación.</p>
      <p v-if="consentimiento.verificadoUtc">Comprobado el {{ fechaHonduras(consentimiento.verificadoUtc) }}.</p>
      <a v-if="consentimiento.consentimiento!=='Confirmado'&&consentimiento.enlaceWhatsApp" :href="consentimiento.enlaceWhatsApp" target="_blank" rel="noopener noreferrer" class="boton boton-primario">Autorizar WhatsApp</a>
      <button v-if="consentimiento.consentimiento==='Confirmado'" class="boton boton-secundario" :disabled="procesando" @click="revocar">Revocar autorización de WhatsApp</button>
      <button class="boton boton-secundario" @click="cargar">Actualizar estado</button>
    </section>
    <section class="env-card perfil-eliminar" aria-labelledby="titulo-eliminar-cuenta">
      <div class="perfil-eliminar-cabecera">
        <span class="perfil-eliminar-icono"><AppIcon nombre="alerta" :tamano="25"/></span>
        <div><h2 id="titulo-eliminar-cuenta">Eliminar cuenta</h2><p>Elimina permanentemente tu cuenta de SYNCHRO si no tiene registros vinculados.</p></div>
      </div>
      <p>Si tienes reservas, movimientos u otros registros asociados, contacta con SYNCHRO para revisar la eliminación sin perder tu historial.</p>
      <button v-if="!confirmar" ref="botonEliminar" type="button" class="perfil-boton-eliminar" @click="abrirEliminar">Eliminar mi cuenta</button>
      <form v-else class="perfil-confirmacion" @submit.prevent="confirmarEliminar" :aria-busy="eliminando">
        <h3>¿Confirmas la eliminación de tu cuenta?</h3>
        <p>Esta acción no se puede deshacer. Confirma tu contraseña para continuar. Se cerrará tu sesión al completar la eliminación.</p>
        <div class="campo">
          <label for="eliminar-clave">Contraseña actual</label>
          <input ref="entradaClave" id="eliminar-clave" v-model="clave" type="password" autocomplete="current-password" maxlength="128" required :readonly="eliminando" placeholder="Tu contraseña"/>
        </div>
        <p v-if="errorEliminar" class="alerta" role="alert">{{ errorEliminar }}</p>
        <div class="perfil-eliminar-acciones">
          <SynButton variante="secundario" :disabled="eliminando" @click="cancelarEliminar">Cancelar</SynButton>
          <button type="submit" class="perfil-boton-eliminar perfil-boton-confirmar" :disabled="eliminando||!clave">{{ eliminando?'Eliminando...':'Eliminar definitivamente' }}</button>
        </div>
      </form>
    </section>
  </section>
</template>

<style scoped>
.perfil-eliminar { display:grid; gap:15px; }
.perfil-eliminar-cabecera { display:flex; gap:12px; align-items:flex-start; }
.perfil-eliminar-icono { display:grid; place-items:center; flex:0 0 44px; height:44px; border:1px solid var(--border); border-radius:12px; background:var(--surface-soft); }
.perfil-eliminar h2,.perfil-confirmacion h3 { margin:0 0 5px; }
.perfil-eliminar p { margin:0; line-height:1.6; color:var(--text-muted); }
.perfil-boton-eliminar { justify-self:start; display:inline-flex; align-items:center; justify-content:center; min-height:44px; padding:10px 16px; border-radius:12px; border:1px solid #b42318; color:#b42318; background:var(--surface); font:inherit; font-size:14px; font-weight:650; cursor:pointer; }
.perfil-boton-eliminar:hover:not(:disabled) { background:#b42318; color:#fff; }
.perfil-boton-confirmar { background:#b42318; color:#fff; }
.perfil-boton-confirmar:hover:not(:disabled) { background:#912018; }
.perfil-boton-eliminar:focus-visible { outline:3px solid var(--ring); outline-offset:3px; }
.perfil-boton-eliminar:disabled { opacity:.6; cursor:not-allowed; }
:global(:root[data-theme="oscuro"]) .perfil-boton-eliminar:not(.perfil-boton-confirmar) { color:#ffb4ab; border-color:#ffb4ab; }
:global(:root[data-theme="oscuro"]) .perfil-boton-eliminar:not(.perfil-boton-confirmar):hover { color:#fff; }
.perfil-confirmacion { display:grid; gap:12px; padding:16px; background:var(--surface-soft); border:1px solid var(--border); border-radius:14px; }
.perfil-confirmacion h3 { font-size:16px; }
.perfil-confirmacion .campo { max-width:420px; }
.perfil-confirmacion input { width:100%; box-sizing:border-box; }
.perfil-eliminar-acciones { display:flex; gap:10px; flex-wrap:wrap; }
@media(max-width:560px) { .perfil-eliminar-acciones>* { flex:1 1 100%; } }
</style>
