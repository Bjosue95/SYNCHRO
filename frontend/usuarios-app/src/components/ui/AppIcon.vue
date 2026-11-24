<script setup>
import { computed } from "vue";
import { useTheme } from "../../composables/useTheme.js";

// Una familia propia de SVG, en dos sprites compartidos por toda la web.
// Un icono nuevo se registra aquí y en ambos sprites, no en cada componente.
const props = defineProps({
  nombre: { type: String, required: true },
  tamano: { type: Number, default: 22 },
  etiqueta: { type: String, default: "" },
  temaForzado: { type: String, default: "", validator: valor => ["", "claro", "oscuro"].includes(valor) }
});
const { tema } = useTheme();
const conocidos = new Set(["inicio","encomienda","reservas","casillero","temperatura","carga","usuario","seguridad","buscar","whatsapp","sol","luna","salir","menu","flecha","calendario","reloj","ubicacion","check","volver","telefono","correo","facial","pin","alarma","sensores","braille","accesibilidad","control-acceso","trazabilidad","sensor-temperatura","frio","calor","laptop","carga-rapida","cotizacion","instagram","tiktok","enlace-externo","enviar","copiar","emergencia","comunidad","historial","guardar","retirar","autonomia","instituciones","instalaciones","registro","notificacion","privacidad","tecnologia","mapa","ayuda","alerta","cobro","solar","bateria-respaldo","eficiencia","energia","cifrado","integridad","resiliencia","ojo","ojo-oculto"]);
const icono = computed(() => conocidos.has(props.nombre) ? props.nombre : "ayuda");
const ruta = computed(() => `/iconos/${props.temaForzado || tema.value}/web-iconos.svg#${icono.value}`);
</script>

<template>
  <svg class="icono-ui" xmlns="http://www.w3.org/2000/svg"
    viewBox="0 0 128 128" :width="tamano" :height="tamano"
    :role="etiqueta ? 'img' : undefined" :aria-label="etiqueta || undefined"
    :aria-hidden="etiqueta ? undefined : 'true'" focusable="false">
    <use :href="ruta" />
  </svg>
</template>

<style scoped>
.icono-ui { display: block; flex: 0 0 auto; overflow: visible; }
</style>
