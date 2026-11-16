<script setup>
import WhatsAppManualAdminView from "./WhatsAppManualAdminView.vue";
</script>

<template>
  <div>
    <div class="page-heading">
      <div>
        <p class="eyebrow">OPERACIONES · VERIFICACIÓN MANUAL</p>
        <h1>WhatsApp y PIN</h1>
        <p>Confirma el mensaje del cliente y envía su PIN antes de habilitar el depósito.</p>
      </div>
    </div>
    <WhatsAppManualAdminView :integrado="true" />
  </div>
</template>