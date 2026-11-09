<script setup>
import { computed, onUnmounted, ref } from "vue";
import { RouterLink } from "vue-router";
import AppHeader from "../components/AppHeader.vue";
import HelpDialog from "../components/HelpDialog.vue";
import { useTema } from "../composables/useTema";
import { retirarPertenenciasPersonales } from "../services/retirosPersonalesTerminalApi";

const { oscuro, cambiarTema } = useTema();
const centroAyuda = ref(null);
const usoId = ref("");
const pin = ref("");
const retirando = ref(false);
const resultado = ref(null);
const error = ref("");

const datosValidos = computed(() => {
  const numero = Number(usoId.value);
  return /^[1-9][0-9]*$/.test(usoId.value) &&
    Number.isSafeInteger(numero) && numero > 0 &&
    /^\d{6}$/.test(pin.value);
});

function abrirAyuda() { centroAyuda.value?.abrir(); }
onUnmounted(() => { pin.value = ""; });

async function confirmarRetiro() {
  if (retirando.value || resultado.value || !datosValidos.value) return;
  retirando.value = true;
  error.value = "";
  try {
    const confirmacion = await retirarPertenenciasPersonales(Number(usoId.value), pin.value);
    pin.value = "";
    resultado.value = confirmacion;
  } catch (e) {
    pin.value = "";
    error.value = e instanceof Error ? e.message : "No se pudo registrar el retiro.";
  } finally {
    retirando.value = false;
  }
}
</script>

<template>
  <main class="pantalla" :class="{ 'tema-oscuro': oscuro }">
    <AppHeader :oscuro="oscuro" @cambiar-tema="cambiarTema" @abrir-ayuda="abrirAyuda" />
    <RouterLink to="/retirar" class="regresar">Volver a Retirar</RouterLink>
    <section class="contenido">
      <h1>Retirar pertenencias personales</h1>
      <div class="panel">
        <template v-if="resultado">
          <p class="aviso" role="status">¡Retiro registrado correctamente!</p>
          <p>Casillero: <strong>{{ resultado.codigoCasillero }}</strong></p>
          <p>Estado: <strong>{{ resultado.estadoCasillero }}</strong></p>
          <p class="ayuda">En este prototipo, abre manualmente el casillero asignado y recoge tus pertenencias.</p>
          <RouterLink class="boton-principal" to="/menu">Finalizar</RouterLink>
        </template>

        <form v-else @submit.prevent="confirmarRetiro">
          <p class="ayuda">Introduce los datos que recibiste al depositar tus pertenencias.</p>

          <label for="retiro-uso">Número de reserva</label>
          <input id="retiro-uso" v-model.trim="usoId" type="text" inputmode="numeric"
            pattern="[1-9][0-9]*" maxlength="10" autocomplete="off" placeholder="Ejemplo: 17" required />

          <label for="retiro-pin">PIN de retiro (6 dígitos)</label>
          <input id="retiro-pin" v-model.trim="pin" type="password" inputmode="numeric"
            pattern="[0-9]{6}" maxlength="6" autocomplete="off" placeholder="6 dígitos" required />

          <p v-if="error" class="error" role="alert">{{ error }}</p>

          <button type="submit" class="boton-principal" :disabled="retirando || !datosValidos">
            {{ retirando ? "Verificando..." : "Confirmar retiro" }}
          </button>
        </form>
      </div>
    </section>
    <HelpDialog ref="centroAyuda" :oscuro="oscuro" />
  </main>
</template>

<style scoped>
.pantalla {
  display: flex; flex-direction: column; gap: 18px; min-height: 100dvh;
  padding: 20px 26px; background: #f2f7fa; color: #01305c;
  transition: background-color 150ms ease, color 150ms ease;
}
.regresar {
  display: inline-flex; align-items: center; align-self: flex-start; justify-content: center;
  min-height: 56px; padding: 10px 20px; border-radius: 14px;
  background: #e4f2f6; color: inherit; text-decoration: none; font-weight: 600;
}
.contenido { width: min(100%, 700px); margin: 0 auto; }
h1 { margin: 4px 0 22px; font-size: clamp(24px, 4vw, 34px); }
.panel { background: #fff; border: 1px solid #dfeaf0; padding: clamp(20px, 4vw, 32px); border-radius: 20px; }
form { display: flex; flex-direction: column; gap: 12px; }
label { font-weight: 700; margin-top: 8px; }
input {
  width: 100%; min-height: 56px; padding: 12px 16px;
  border: 1px solid #7898ad; border-radius: 12px;
  background: #fff; color: #01305c; font-size: 19px;
}
.ayuda { margin: 0 0 14px; line-height: 1.6; color: #5b7282; }
.aviso { color: #007a8b; font-size: 19px; font-weight: 700; }
.error { color: #c33636; margin: 8px 0; }
.boton-principal {
  display: inline-flex; justify-content: center; align-items: center; margin-top: 14px;
  min-height: 56px; width: 100%; padding: 12px 20px; border: 0; border-radius: 14px;
  background: #00b9c7; color: #01305c; text-decoration: none;
  font: inherit; font-weight: 700; font-size: 17px; cursor: pointer;
}
.boton-principal:disabled { opacity: .6; cursor: not-allowed; }
.tema-oscuro { background: #061f36; color: #fff; }
.tema-oscuro .regresar { background: #204766; }
.tema-oscuro .panel { background: #103956; border-color: #315573; }
.tema-oscuro .ayuda { color: #c6dfea; }
.tema-oscuro .input { background: #103956; }
.tema-oscuro input { background: #061f36; color: #fff; border-color: #5f8ca8; }
.tema-oscuro .aviso { color: #5ec6e8; }
.tema-oscuro .error { color: #ff9c9c; }
.regresar:focus-visible, input:focus-visible, .boton-principal:focus-visible { outline: 3px solid #00b9c7; outline-offset: 3px; }
@media (prefers-reduced-motion: reduce) { .pantalla { transition: none; } }
</style>
