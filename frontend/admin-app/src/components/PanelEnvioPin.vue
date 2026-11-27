<script setup>
defineProps({
  titulo: {type:String,required:true},
  pin: {type:String,default:""},
  telefono: {type:String,default:""},
  mensaje: {type:String,default:""},
  enlace: {type:String,default:""},
  ocupado: Boolean,
  copiado: Boolean
});
defineEmits(["copiar","enviado","reemplazar"]);
</script>
<template>
  <section class="pin-panel" aria-label="Envío manual del PIN">
    <h3>{{ titulo }}</h3>
    <p v-if="pin && mensaje" class="numero-pin">{{ pin }}</p>
    <p v-else class="info">El PIN está oculto. Si no lo enviaste, genera un reemplazo.</p>
    <p>Enviar a: {{ telefono }}</p>
    <textarea v-if="mensaje" :value="mensaje" rows="4" readonly aria-label="Mensaje completo" />
    <div class="acciones">
      <button v-if="mensaje" class="primario" type="button" :disabled="ocupado" @click="$emit('copiar')">Copiar mensaje completo</button>
      <a v-if="enlace && mensaje" :href="enlace" target="_blank" rel="noopener noreferrer" class="secundario">Abrir WhatsApp</a>
      <button v-if="!mensaje && !copiado" class="secundario" type="button" :disabled="ocupado" @click="$emit('reemplazar')">Generar nuevo PIN</button>
      <button class="primario" type="button" :disabled="ocupado || !copiado" @click="$emit('enviado')">Ya envié el PIN por WhatsApp</button>
    </div>
    <p class="ayuda">El PIN se oculta después de 60 segundos. Marcar enviado es una declaración del operador.</p>
  </section>
</template>
<style scoped>
.pin-panel {padding-top:17px;margin-top:19px;border-top:1px solid var(--wa-border,var(--border));color:var(--wa-text,var(--ink));}
.pin-panel h3 {margin:0 0 12px;font-size:18px;}
.pin-panel p {line-height:1.6;}
.pin-panel .numero-pin {font-size:2.1rem;letter-spacing:.16em;font-weight:800;font-variant-numeric:tabular-nums;line-height:1.3;margin:12px 0;}
.pin-panel textarea {box-sizing:border-box;display:block;width:100%;min-width:0;min-height:0;padding:12px 14px;border:1px solid var(--wa-border,var(--border));border-radius:11px;background:var(--wa-field,var(--white));color:inherit;font:inherit;resize:vertical;}
.pin-panel .acciones {display:flex;align-items:center;justify-content:flex-start;flex-wrap:wrap;gap:12px;margin:14px 0;}
.pin-panel .primario,.pin-panel .secundario {display:inline-flex;align-items:center;justify-content:center;min-height:45px;max-width:100%;padding:10px 17px;border:1px solid transparent;border-radius:11px;font:inherit;font-size:13px;font-weight:750;text-decoration:none;cursor:pointer;}
.pin-panel .primario {background:#00b9c7;color:#01305c;border-color:#00b9c7;}
.pin-panel .secundario {background:var(--wa-card,var(--white));color:inherit;border-color:var(--wa-border,var(--border));}
.pin-panel button:disabled {opacity:.55;cursor:not-allowed;}
.pin-panel .info {padding:13px;border:1px solid var(--wa-border,var(--border));border-radius:12px;}
.pin-panel .ayuda {font-size:14px;line-height:1.55;}
@media(max-width:590px){.pin-panel .acciones{align-items:stretch;}.pin-panel .acciones>*{width:100%;}}
</style>
