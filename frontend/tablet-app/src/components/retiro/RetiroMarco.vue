<script setup>
import { RouterLink } from "vue-router";
import { useTema } from "../../composables/useTema";

defineProps({
  paso: {
    type: Number,
    default: 0
  },
  volver: {
    type: String,
    default: "/menu"
  }
});

const { oscuro } = useTema();
</script>

<template>
  <main
    class="retiro-superficie"
    :class="{ 'tema-oscuro': oscuro }"
  >
    <div class="retiro-limite">

      <RouterLink :to="volver" class="retiro-atras">
        <svg
          viewBox="0 0 24 24"
          width="22"
          height="22"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <path d="M19 12H5" />
          <path d="m12 19-7-7 7-7" />
        </svg>

        <span>Volver</span>
      </RouterLink>

      <div class="retiro-contenido">

        <nav
          v-if="paso > 0"
          class="retiro-pasos"
          aria-label="Progreso del retiro"
        >
          <div class="retiro-indicadores">
            <span
              v-for="n in 3"
              :key="n"
              :class="{ activo: n <= paso }"
            ></span>
          </div>

          <span class="retiro-etapa">
            Paso {{ paso }} de 3
          </span>
        </nav>

        <slot />

      </div>
    </div>
  </main>
</template>

<style scoped>
.retiro-superficie {
  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;
  overflow-y: auto;

  background: #f2f7fa;
  color: #01305c;
}

.retiro-limite {
  display: flex;
  flex-direction: column;
  gap: 12px;

  width: min(100%, 1060px);
  min-height: 100%;
  margin: 0 auto;

  padding:
    clamp(12px, 2vh, 24px)
    clamp(14px, 2.5vw, 30px);
}

.retiro-atras {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 10px;

  align-self: flex-start;
  flex-shrink: 0;

  min-height: 50px;
  padding: 9px 18px;

  border: 1.5px solid #aed5e0;
  border-radius: 13px;

  background: #e5f3f7;
  color: inherit;

  font-size: 16px;
  font-weight: 700;
  text-decoration: none;
}

.retiro-atras:hover {
  background: #d6edf3;
}

.retiro-atras:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

.retiro-contenido {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: flex-start;

  flex: 1;
  gap: clamp(12px, 2vh, 22px);

  width: 100%;
  min-width: 0;
  padding: clamp(16px, 4vh, 38px) 0 22px;
}

.retiro-pasos {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 14px;

  color: #526f81;
  font-size: 13px;
  font-weight: 700;
}

.retiro-indicadores {
  display: flex;
  align-items: center;
  gap: 7px;
}

.retiro-indicadores span {
  width: 34px;
  height: 7px;

  border-radius: 20px;
  background: #d4e3eb;
}

.retiro-indicadores span.activo {
  background: #00b9c7;
}

:deep(.retiro-titulo) {
  margin: 0;

  color: inherit;
  font-size: clamp(25px, 3vw, 37px);
  font-weight: 750;
  line-height: 1.2;
  letter-spacing: -.025em;
  text-align: center;
}

:deep(.retiro-subtitulo) {
  max-width: 570px;
  margin: 0;

  color: #587387;
  font-size: clamp(14px, 1.5vw, 17px);
  line-height: 1.5;
  text-align: center;
}

:deep(.retiro-panel) {
  display: flex;
  flex-direction: column;
  align-items: stretch;
  gap: 17px;

  width: min(100%, 590px);
  padding: clamp(20px, 3vw, 32px);

  border: 1px solid #d7e6ee;
  border-radius: 20px;
  background: #fff;

  box-shadow: 0 7px 22px rgba(1,48,92,.055);
}

:deep(.retiro-boton) {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;

  min-height: 56px;
  width: 100%;
  padding: 12px 22px;

  border: 2px solid #00a6b4;
  border-radius: 13px;

  background: #00b9c7;
  color: #01305c;

  font: inherit;
  font-size: 17px;
  font-weight: 800;

  cursor: pointer;
  touch-action: manipulation;
}

:deep(.retiro-boton:hover:not(:disabled)) {
  background: #5ec6e8;
}

:deep(.retiro-boton:disabled) {
  background: #d9e7ec;
  border-color: #c0d3dc;
  color: #587181;
  cursor: not-allowed;
}

:deep(.retiro-boton:focus-visible) {
  outline: 3px solid #01305c;
  outline-offset: 3px;
}

:deep(.retiro-aviso) {
  margin: 0;
  color: #587387;
  font-size: 13px;
  line-height: 1.5;
  text-align: center;
}

:deep(.retiro-error) {
  margin: 0;
  padding: 12px;
  border-radius: 9px;
  background: #fff0ed;
  color: #852d25;
  font-size: 14px;
}

.retiro-superficie.tema-oscuro {
  background: #061f36;
  color: #fff;
}

.tema-oscuro .retiro-atras {
  background: #204766;
  border-color: #42647e;
}

.tema-oscuro :deep(.retiro-panel) {
  background: #103956;
  border-color: #315573;
}

.tema-oscuro :deep(.retiro-subtitulo),
.tema-oscuro :deep(.retiro-aviso),
.tema-oscuro .retiro-pasos {
  color: #d4e2ea;
}

.tema-oscuro :deep(.retiro-boton:disabled) {
  background: #294961;
  color: #d5e6ef;
  border-color: #42647e;
}

@media (min-width: 800px) and (min-height: 670px) {
  .retiro-contenido {
    justify-content: center;
    padding-top: 0;
  }
}

@media (max-height: 600px) {
  .retiro-limite {
    padding: 8px 14px;
    gap: 8px;
  }

  .retiro-contenido {
    gap: 10px;
    padding: 8px 0;
  }

  .retiro-atras {
    min-height: 42px;
    padding: 6px 14px;
  }

  :deep(.retiro-panel) {
    gap: 10px;
    padding: 16px;
  }
}
</style>