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
        { path: "casilleros", name: "casilleros", component: () => import("../views/CasillerosView.vue") },
        { path: "reservas", name: "reservas", component: () => import("../views/ModuloPendienteView.vue"), props: { titulo: "Reservas", descripcion: "Este módulo se conectará a las reservas del backend en la siguiente etapa." } },
        { path: "operaciones", name: "operaciones", component: () => import("../views/ModuloPendienteView.vue"), props: { titulo: "Operaciones", descripcion: "El historial y la supervisión de depósitos y retiros todavía no están integrados en este panel." } },
        { path: "reportes", name: "reportes", component: () => import("../views/ModuloPendienteView.vue"), props: { titulo: "Reportes", descripcion: "Este módulo aún no presenta reportes del backend." } },
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