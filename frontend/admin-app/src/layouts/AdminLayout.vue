<script setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { RouterLink, RouterView, useRoute, useRouter } from "vue-router";
import { cerrarSesion, consultarSesion } from "../services/adminApi";
import AdminIcon from "../components/AdminIcon.vue";

const router = useRouter();
const route = useRoute();
const menuAbierto = ref(false);
const nombreUsuario = ref("");
const rol = ref("");
const roles = ref([]);
const saliendo = ref(false);
const error = ref("");

const grupos = [
  { titulo: "OPERACIONES", enlaces: [
    { ruta: "/admin", titulo: "Resumen", icono: "dashboard" },
    { ruta: "/admin/whatsapp", titulo: "WhatsApp y PIN", icono: "message" },
    { ruta: "/admin/cobros", titulo: "Cobros", icono: "check" },
    { ruta: "/admin/casilleros", titulo: "Casilleros", icono: "lockers" },
    { ruta: "/admin/terminales", titulo: "Terminales", icono: "lockers" },
    { ruta: "/admin/operaciones", titulo: "Movimientos", icono: "activity" },
  ] },
  { titulo: "GESTIÓN", enlaces: [
    { ruta: "/admin/reportes", titulo: "Reportes", icono: "report" },
    { ruta: "/admin/configuracion", titulo: "Configuración", icono: "settings" },
  ] },
];

const gruposVisibles = computed(()=>grupos.map(g=>({...g,enlaces:g.enlaces.filter(e=>{
  const a=roles.value.includes("Administrador"),o=roles.value.includes("Operador"),v=roles.value.includes("Auditor");
  return e.ruta==="/admin/configuracion"?a:e.ruta==="/admin/reportes"?a||v:a||o;
})})).filter(g=>g.enlaces.length));
const tituloActual = computed(() =>
  grupos.flatMap(x => x.enlaces).find(x => x.ruta === route.path)?.titulo ||
  (route.path === "/admin/encomiendas" ? "Ficha de encomienda" : "Administración")
);
watch(() => route.path, () => { menuAbierto.value = false; });
function cerrarConEscape(e) { if (e.key === "Escape") menuAbierto.value = false; }

onMounted(async () => {
  window.addEventListener("keydown", cerrarConEscape);
  try {
    const sesion = await consultarSesion();
    if (typeof sesion.usuario !== "string" || !sesion.usuario.trim()) throw new Error("Identidad de sesión no disponible.");
    nombreUsuario.value = sesion.usuario;
    roles.value = sesion.roles || [];
    rol.value = sesion?.rol || roles.value[0] || "";
  } catch { await router.replace("/login"); }
});
onBeforeUnmount(() => window.removeEventListener("keydown", cerrarConEscape));

async function salir() {
  if (saliendo.value) return;
  saliendo.value = true;
  error.value = "";
  try {
    await cerrarSesion();
    await router.replace("/login");
  } catch (e) { error.value = e?.message || "No fue posible cerrar la sesión."; }
  finally { saliendo.value = false; }
}
</script>

<template>
  <div class="admin-shell">
    <button v-if="menuAbierto" class="mobile-overlay" type="button" aria-label="Cerrar menú de navegación" @click="menuAbierto = false"></button>
    <aside id="navegacion-admin" class="admin-sidebar" :class="{ abierta: menuAbierto }" aria-label="Navegación administrativa">
      <RouterLink to="/admin" class="sidebar-brand" @click="menuAbierto = false">
        <img src="/assets/isotipo-fondo-oscuro.png" alt="" width="37" height="37" />
        <img src="/assets/nombre-blanco.png" alt="SYNCHRO" class="sidebar-wordmark" />
      </RouterLink>
      <div class="sidebar-product"><span class="product-stripe"></span>ADMINISTRACIÓN</div>
      <nav class="sidebar-navigation" aria-label="Secciones del administrador">
        <div v-for="grupo in gruposVisibles" :key="grupo.titulo" class="sidebar-group">
          <p class="sidebar-section-label">{{ grupo.titulo }}</p>
          <RouterLink v-for="enlace in grupo.enlaces" :key="enlace.ruta" :to="enlace.ruta"
            class="sidebar-link" :class="{ activo: route.path === enlace.ruta }"
            :aria-current="route.path === enlace.ruta ? 'page' : undefined" @click="menuAbierto = false">
            <AdminIcon :name="enlace.icono" :size="19" />
            <span>{{ enlace.titulo }}</span>
            <span v-if="enlace.pendiente" class="nav-pending" aria-label="En preparación">Pronto</span>
          </RouterLink>
        </div>
      </nav>
      <div class="sidebar-bottom">
        <div class="sidebar-user"><span class="user-avatar"><AdminIcon name="user" :size="19" /></span><span class="sidebar-identidad"><strong :title="nombreUsuario">{{ nombreUsuario || "Cargando usuario…" }}</strong><small>{{ rol }}</small></span></div>
        <button type="button" class="sidebar-exit" :disabled="saliendo" @click="salir"><AdminIcon name="logout" :size="18" />{{ saliendo ? 'Cerrando...' : 'Cerrar sesión' }}</button>
      </div>
    </aside>

    <div class="admin-workspace">
      <header class="admin-topbar">
        <div class="topbar-left">
          <button class="mobile-menu-button" type="button" :aria-label="menuAbierto ? 'Cerrar menú' : 'Abrir menú'" :aria-expanded="menuAbierto" aria-controls="navegacion-admin" @click="menuAbierto = !menuAbierto"><AdminIcon :name="menuAbierto ? 'close' : 'menu'" :size="22" /></button>
          <div class="breadcrumb"><span>Administración</span><AdminIcon name="chevron" :size="16" /><strong>{{ tituloActual }}</strong></div>
        </div>
        <span class="topbar-role"><span class="topbar-role-dot" aria-hidden="true"></span>{{ rol }}</span>
      </header>
      <main id="contenido-admin" class="admin-page" tabindex="-1">
        <p v-if="error" class="inline-error" role="alert">{{ error }}</p>
        <RouterView />
      </main>
    </div>
  </div>
</template>

<style scoped>
.sidebar-identidad { flex: 1; min-width: 0; }
.sidebar-identidad strong, .sidebar-identidad small { overflow-wrap: anywhere; }
</style>
