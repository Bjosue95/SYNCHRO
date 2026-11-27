<script setup>
import { computed,ref } from "vue";
import { useRouter } from "vue-router";
import RetiroMarco from "../components/retiro/RetiroMarco.vue";
import { iniciarRetiro } from "../services/retiroSesion.js";
const router=useRouter();
const codigo=ref("");
const codigoValido=computed(()=>/^[A-Z0-9-]{2,10}$/.test(codigo.value.trim().toUpperCase()));
function continuar(){if(!codigoValido.value)return;iniciarRetiro(codigo.value.trim().toUpperCase());router.push("/retirar/rostro");}
</script>
<template>
  <RetiroMarco :paso="1" volver="/retirar">
    <h1 class="retiro-titulo">Identifica tu casillero</h1>
    <p class="retiro-subtitulo">Escribe el cÃ³digo que figura en tu casillero o en la confirmaciÃ³n del depÃ³sito.</p>
    <form class="retiro-panel" @submit.prevent="continuar">
      <label class="etiqueta" for="codigo-retiro">CÃ³digo del casillero</label>
      <input id="codigo-retiro" v-model="codigo" maxlength="10" autocomplete="off" autocapitalize="characters" spellcheck="false" placeholder="Ejemplo: M08" class="codigo-entrada" required />
      <p class="retiro-aviso">El cÃ³digo todavÃ­a no identifica la operaciÃ³n en el servidor. Esta etapa prepara la interfaz del retiro.</p>
      <button type="submit" class="retiro-boton" :disabled="!codigoValido">Continuar <span aria-hidden="true">â†’</span></button>
    </form>
  </RetiroMarco>
</template>
<style scoped>
.etiqueta{font-weight:750;font-size:16px}.codigo-entrada{width:100%;height:60px;min-width:0;border:2px solid #b9d1dd;border-radius:12px;background:#fff;color:#01305c;padding:10px 18px;font:inherit;font-size:22px;font-weight:750;letter-spacing:.06em;text-transform:uppercase}
.codigo-entrada:focus-visible{outline:3px solid #00b9c7;outline-offset:2px}.codigo-entrada::placeholder{font-size:16px;letter-spacing:0;font-weight:400;text-transform:none}
</style>