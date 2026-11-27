import { createRouter, createWebHistory } from "vue-router";
import { consultarSesion } from "../services/adminApi";
import LoginView from "../views/LoginView.vue";
import AdminLayout from "../layouts/AdminLayout.vue";

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: "/", redirect: "/admin" },
    { path: "/login", name: "login", component: LoginView, meta: { publica: true } },
    {
      path: "/admin",
      component: AdminLayout,
      children: [
        { path: "", name: "resumen", component: () => import("../views/ResumenView.vue") },
        { path: "whatsapp", name: "whatsapp", component: () => import("../views/WhatsAppView.vue") },
        { path: "encomiendas", name: "encomiendas", component: () => import("../views/EncomiendasView.vue") },
        { path: "cobros", name: "cobros", component: () => import("../views/CobrosView.vue") },
        { path: "casilleros", name: "casilleros", component: () => import("../views/CasillerosView.vue") },
        { path: "terminales", name: "terminales", component: () => import("../views/TerminalesView.vue") },
        { path: "operaciones", name: "operaciones", component: () => import("../views/MovimientosView.vue") },
        { path: "reportes", name: "reportes", component: () => import("../views/ReportesView.vue") },
        { path: "configuracion", name: "configuracion", component: () => import("../views/ModuloPendienteView.vue"), props: { titulo: "Configuración", descripcion: "Las opciones administrativas se habilitarán después de conectar sus permisos y endpoints." } },
      ],
    },
    { path: "/:pathMatch(.*)*", redirect: "/admin" },
  ],
});

router.beforeEach(async (to) => {
  if (to.meta.publica) return true;
  try {
    await consultarSesion();
    return true;
  } catch {
    return { name: "login", query: { volver: to.fullPath } };
  }
});

export default router;
