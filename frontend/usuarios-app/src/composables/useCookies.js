import { computed, readonly, ref } from "vue";
import { CLAVE_COOKIES, leerPreferencias, crearPreferencias, persistirPreferencias } from "../services/preferenciasCookies.js";

function almacenLocal() {
  try { return window.localStorage; } catch { return null; }
}
const preferencias = ref(leerPreferencias(almacenLocal()));
const configurando = ref(false);
const avisoVisible = computed(() => preferencias.value === null || configurando.value);
const mapaPermitido = computed(() => preferencias.value?.mapa === true);

function guardarPreferencias(mapa) {
  const datos = crearPreferencias(mapa);
  persistirPreferencias(almacenLocal(), datos);
  preferencias.value = datos;
  configurando.value = false;
}
function abrirPreferencias() { configurando.value = true; }
function cerrarPreferencias() { configurando.value = false; }

if (typeof window !== "undefined") {
  window.addEventListener("storage", evento => {
    if (evento.key === CLAVE_COOKIES || evento.key === null)
      preferencias.value = leerPreferencias(almacenLocal());
  });
}

export function useCookies() {
  return { preferencias: readonly(preferencias), configurando: readonly(configurando),
    avisoVisible, mapaPermitido, guardarPreferencias, abrirPreferencias, cerrarPreferencias };
}
