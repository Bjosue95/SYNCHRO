<script setup lang="ts">
import { computed,onMounted,ref } from 'vue';
import { estadoSesion } from '../services/sesion.js';
import { encomiendasApi } from '../services/encomiendasApi.js';
import { fechaHonduras,nombreEstadoReserva } from '../services/formatos.js';
const consentimiento=ref<any>(null),error=ref(''),procesando=ref(false);
const perfil=computed<any>(()=>estadoSesion.cliente);
async function cargar(){try{consentimiento.value=await encomiendasApi.consentimientoCuenta();}catch(e){error.value=(e as Error).message;}}
async function revocar(){if(procesando.value)return;procesando.value=true;try{await encomiendasApi.revocarConsentimientoCuenta();await cargar();}catch(e){error.value=(e as Error).message;}finally{procesando.value=false;}}
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
  </section>
</template>
