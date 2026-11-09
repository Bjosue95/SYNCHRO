<script setup>
import { ref } from "vue";
import { RouterLink } from "vue-router";
import AppHeader from "../components/AppHeader.vue";
import HelpDialog from "../components/HelpDialog.vue";
import { useTema } from "../composables/useTema";

const { oscuro, cambiarTema } = useTema();
const centroAyuda = ref(null);
function abrirAyuda() { centroAyuda.value?.abrir(); }
</script>

<template>
  <main class="pantalla" :class="{ 'tema-oscuro': oscuro }">
    <AppHeader :oscuro="oscuro" @cambiar-tema="cambiarTema" @abrir-ayuda="abrirAyuda" />

    <RouterLink class="regresar" to="/menu">Volver al menú</RouterLink>

    <section class="contenido">
      <h1>Retirar</h1>
      <p class="descripcion">Selecciona qué deseas retirar de SYNCHRO.</p>

      <div class="opciones">
        <RouterLink to="/retirar/pertenencias" class="tarjeta tarjeta-activa">
          <span class="titulo-tarjeta">Pertenencias personales</span>
          <span class="detalle">Retira tus objetos con tu número de reserva y PIN.</span>
          <span class="indicador">Continuar <span aria-hidden="true">›</span></span>
        </RouterLink>

        <div class="tarjeta tarjeta-inactiva" aria-disabled="true">
          <span class="titulo-tarjeta">Encomiendas</span>
          <span class="detalle">El retiro con verificación facial y PIN se habilitará después.</span>
          <span class="proximamente">Próximamente</span>
        </div>
      </div>
    </section>

    <HelpDialog ref="centroAyuda" :oscuro="oscuro" />
  </main>
</template>

<style scoped>
.pantalla {
  display: flex; flex-direction: column; gap: 18px;
  min-height: 100dvh; padding: 20px 26px;
  background: #f2f7fa; color: #01305c;
  transition: background-color 150ms ease, color 150ms ease;
}
.regresar {
  display: inline-flex; align-items: center; align-self: flex-start; justify-content: center;
  min-height: 56px; padding: 10px 20px; border-radius: 14px;
  background: #e4f2f6; color: inherit; text-decoration: none; font-weight: 600;
}
.contenido { width: min(100%, 840px); margin: 0 auto; }
h1 { font-size: clamp(25px, 4vw, 36px); margin: 0; }
.descripcion { margin: 12px 0 24px; line-height: 1.5; color: #5b7282; }
.opciones { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 18px; }
.tarjeta {
  display: flex; flex-direction: column; gap: 12px; min-height: 220px;
  padding: 26px; border: 1px solid #dfeaf0; border-radius: 20px;
  background: #fff; color: inherit; text-decoration: none;
  box-shadow: 0 3px 12px #01305c0a;
  transition: background-color 150ms ease, border-color 150ms ease, color 150ms ease;
}
.tarjeta-activa:hover { border-color: #00b9c7; }
.titulo-tarjeta { font-size: clamp(19px, 3vw, 24px); font-weight: 700; }
.detalle { line-height: 1.6; color: #5b7282; }
.indicador { margin-top: auto; color: #007a8b; font-weight: 700; }
.proximamente { margin-top: auto; color: #5b7282; font-weight: 600; }
.tarjeta-inactiva { opacity: .75; }
.tema-oscuro { background: #061f36; color: #fff; }
.tema-oscuro .regresar { background: #204766; }
.tema-oscuro .tarjeta { background: #103956; border-color: #315573; }
.tema-oscuro .descripcion, .tema-oscuro .detalle, .tema-oscuro .proximamente { color: #c6dfea; }
.tema-oscuro .indicador { color: #5ec6e8; }
.regresar:focus-visible, .tarjeta-activa:focus-visible { outline: 3px solid #00b9c7; outline-offset: 3px; }
@media (max-width: 640px) { .opciones { grid-template-columns: 1fr; } .tarjeta { min-height: 160px; } }
@media (prefers-reduced-motion: reduce) { .pantalla, .tarjeta { transition: none; } }
</style>
