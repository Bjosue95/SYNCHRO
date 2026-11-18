<script setup>
defineProps({
  modelValue: {
    type: String,
    default: ""
  },
  oscuro: {
    type: Boolean,
    default: false
  }
});

const emitir = defineEmits(["update:modelValue"]);

const metodos = [
  { codigo: "Efectivo", nombre: "Efectivo" },
  { codigo: "Tarjeta", nombre: "Tarjeta" },
  { codigo: "NFC", nombre: "NFC" }
];
</script>

<template>
  <section
    class="selector-metodos"
    :class="{ 'tema-oscuro': oscuro }"
    data-preview-permitido
    role="group"
    aria-label="Selecciona cómo pagar"
  >
    <button
      v-for="metodo in metodos"
      :key="metodo.codigo"
      type="button"
      class="metodo"
      :class="{ seleccionado: modelValue === metodo.codigo }"
      :aria-pressed="modelValue === metodo.codigo"
      @click="emitir('update:modelValue', metodo.codigo)"
    >
      <span
        v-if="modelValue === metodo.codigo"
        class="marca-seleccion"
        aria-hidden="true"
      >
        <img
          src="/iconos/complementarios/66_seleccion.svg"
          class="icono-claro"
          alt=""
        />
        <img
          src="/iconos/complementarios/66_seleccion-oscuro.svg"
          class="icono-oscuro"
          alt=""
        />
      </span>

      <svg
        v-if="metodo.codigo === 'Efectivo'"
        viewBox="0 0 128 128"
        class="icono-pago"
        aria-hidden="true"
      >
        <rect
          x="14" y="32" width="100" height="64" rx="8"
          fill="none" stroke="currentColor" stroke-width="5"
        />
        <rect
          x="23" y="41" width="82" height="46" rx="4"
          fill="none" stroke="#00B9C7" stroke-width="4"
        />
        <circle
          cx="64" cy="64" r="16"
          fill="none" stroke="currentColor" stroke-width="5"
        />
        <path
          d="M64 50V78M73 56H60C51 56 52 64 61 64H67C77 64 77 73 67 73H55"
          fill="none" stroke="#00B9C7"
          stroke-width="4" stroke-linecap="round"
        />
      </svg>

      <svg
        v-else-if="metodo.codigo === 'Tarjeta'"
        viewBox="0 0 128 128"
        class="icono-pago"
        aria-hidden="true"
      >
        <rect
          x="16" y="29" width="96" height="70" rx="10"
          fill="none" stroke="currentColor" stroke-width="5"
        />
        <path
          d="M20 51H108"
          stroke="#00B9C7" stroke-width="10"
        />
        <rect
          x="30" y="68" width="24" height="14" rx="3"
          fill="none" stroke="currentColor" stroke-width="4"
        />
        <path
          d="M72 78H99"
          stroke="#00B9C7" stroke-width="5"
          stroke-linecap="round"
        />
      </svg>

      <svg
        v-else
        viewBox="0 0 128 128"
        class="icono-pago"
        aria-hidden="true"
      >
        <rect
          x="22" y="42" width="54" height="66" rx="8"
          fill="none" stroke="currentColor" stroke-width="5"
        />
        <path
          d="M40 95H57"
          stroke="#00B9C7" stroke-width="5"
          stroke-linecap="round"
        />
        <path
          d="M77 53Q91 64 77 75
             M85 43Q109 64 85 85
             M94 33Q129 64 94 95"
          fill="none" stroke="#00B9C7"
          stroke-width="5" stroke-linecap="round"
        />
      </svg>

      <strong>{{ metodo.nombre }}</strong>
    </button>
  </section>
</template>

<style scoped>
.selector-metodos,
.selector-metodos * {
  box-sizing: border-box;
}

.selector-metodos {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: clamp(12px, 1.8vw, 20px);
  width: 100%;
}

.metodo {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 11px;

  min-width: 0;
  min-height: clamp(172px, 20dvh, 230px);
  padding: 20px 10px;

  border: 2px solid #c9e3ea;
  border-radius: 18px;

  background: #ffffff;
  color: #01305c;
  font: inherit;
  cursor: pointer;
  touch-action: manipulation;

  transition:
    border-color 150ms ease,
    background-color 150ms ease,
    box-shadow 150ms ease;
}

.metodo strong {
  font-size: clamp(18px, 2vw, 23px);
  font-weight: 800;
}

.icono-pago {
  display: block;
  width: clamp(70px, 9vw, 100px);
  height: clamp(70px, 9vw, 100px);
}

.metodo.seleccionado {
  border-color: #00b9c7;
  background: #e9fbfd;
  box-shadow: 0 0 0 2px rgba(0, 185, 199, .13);
}

.marca-seleccion {
  position: absolute;
  top: 6px;
  right: 6px;
  width: 27px;
  height: 27px;
}

.marca-seleccion img {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: contain;
}

.icono-oscuro {
  opacity: 0;
}

.metodo:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

@media (hover: hover) {
  .metodo:hover {
    border-color: #00b9c7;
  }
}

.selector-metodos.tema-oscuro .metodo {
  background: #103956;
  color: #ffffff;
  border-color: #42647e;
}

.selector-metodos.tema-oscuro .metodo.seleccionado {
  background: #174b65;
  border-color: #00b9c7;
}

.selector-metodos.tema-oscuro .icono-claro {
  opacity: 0;
}

.selector-metodos.tema-oscuro .icono-oscuro {
  opacity: 1;
}

@media (max-width: 420px) {
  .metodo {
    min-height: 145px;
    border-radius: 14px;
    padding: 12px 5px;
  }

  .icono-pago {
    width: 57px;
    height: 57px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .metodo {
    transition: none;
  }
}
</style>