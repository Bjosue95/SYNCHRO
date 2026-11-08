import { ref, onMounted, onUnmounted } from "vue";

export function useReloj() {
  const hora = ref("--:--");
  const fecha = ref("");

  let intervalo = null;

  function actualizarReloj() {
    const ahora = new Date();

    hora.value = new Intl.DateTimeFormat("es-HN", {
      hour: "2-digit",
      minute: "2-digit",
      hour12: false,
    }).format(ahora);

    const textoFecha = new Intl.DateTimeFormat("es-HN", {
      weekday: "long",
      day: "numeric",
      month: "long",
    }).format(ahora);

    fecha.value = textoFecha.charAt(0).toUpperCase() + textoFecha.slice(1);
  }

  onMounted(() => {
    actualizarReloj();
    intervalo = setInterval(actualizarReloj, 30000);
  });

  onUnmounted(() => {
    clearInterval(intervalo);
  });

  return { hora, fecha };
}
