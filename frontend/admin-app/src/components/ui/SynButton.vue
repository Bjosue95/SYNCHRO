<script setup lang="ts">
type Variante = "primario" | "secundario";

const props = withDefaults(
  defineProps<{
    variante?: Variante;
    cargando?: boolean;
    disabled?: boolean;
    tipo?: "button" | "submit" | "reset";
  }>(),
  {
    variante: "primario",
    cargando: false,
    disabled: false,
    tipo: "button"
  }
);
</script>

<template>
  <button
    class="syn-button"
    :data-variante="props.variante"
    :type="props.tipo"
    :disabled="props.disabled || props.cargando"
    :aria-busy="props.cargando ? 'true' : undefined"
  >
    <span
      v-if="props.cargando"
      class="syn-button-spinner"
      aria-hidden="true"
    ></span>

    <span><slot /></span>

    <span
      v-if="props.cargando"
      class="syn-sr-only"
      role="status"
    >
      Procesando
    </span>
  </button>
</template>

<style scoped>
.syn-button {
  display: inline-flex;
  justify-content: center;
  align-items: center;
  gap: 10px;
  min-height: 44px;
  padding: 10px 18px;
  border: 1px solid transparent;
  border-radius: 12px;
  background: var(--syn-ui-primary);
  color: var(--syn-ui-on-primary);
  font: inherit;
  font-size: 14px;
  font-weight: 700;
  line-height: 1.4;
  cursor: pointer;
  transition:
    background-color 180ms ease,
    border-color 180ms ease,
    transform 180ms ease;
}

.syn-button[data-variante="secundario"] {
  background: var(--syn-ui-surface);
  color: var(--syn-ui-text);
  border-color: var(--syn-ui-border);
}

.syn-button:hover:not(:disabled) {
  filter: brightness(0.94);
}

.syn-button:active:not(:disabled) {
  transform: scale(0.98);
}

.syn-button:focus-visible {
  outline: 3px solid var(--syn-ui-focus);
  outline-offset: 3px;
}

.syn-button:disabled {
  cursor: not-allowed;
  opacity: 0.65;
}

.syn-button-spinner {
  width: 16px;
  height: 16px;
  border: 2px solid currentColor;
  border-right-color: transparent;
  border-radius: 50%;
  animation: syn-girar 800ms linear infinite;
}

.syn-sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}

@keyframes syn-girar {
  to { transform: rotate(360deg); }
}

@media (prefers-reduced-motion: reduce) {
  .syn-button {
    transition: none;
  }

  .syn-button-spinner {
    animation: none;
  }
}
</style>
