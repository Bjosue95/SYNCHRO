<template>
  <span class="marca-seleccion" aria-hidden="true">✓</span>
</template>

<style scoped>
/* Misma marca aprobada en las tarjetas de tamaño; no depende del tema. */
.marca-seleccion {
  display: grid;
  place-items: center;
  position: absolute;
  top: 9px;
  right: 9px;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background: #00b9c7;
  color: #01305c;
  font-size: 16px;
  font-weight: 800;
  line-height: 1;
  pointer-events: none;
}
</style>
