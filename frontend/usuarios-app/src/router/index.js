import { createRouter, createWebHistory } from "vue-router";
import { comprobarSesion } from "../services/sesion.js";

const rutas = [
  {
    path: "/", name: "inicio",
    component: () => import("../views/InicioView.vue")
  },
  {
    path: "/acceso", name: "acceso",
    component: () => import("../views/AccesoView.vue")
  },
  {
    path: "/mi-cuenta", name: "cuenta",
    meta: { privada: true, titulo: "Mi espacio" },
    component: () => import("../views/PanelView.vue")
  },
  {
    path: "/reservar/encomienda", name: "encomienda",
    meta: { privada: true, titulo: "Nueva pre-reserva" },
    component: () => import("../views/PreReservaView.vue")
  },
  {
    path: "/consultar", name: "consultar",
    meta: { privada: true, titulo: "Consultar solicitud" },
    component: () => import("../views/ConsultarReservaView.vue")
  },
  { path: "/:pathMatch(.*)*", redirect: "/" }
];

export const router = createRouter({
  history: createWebHistory(),
  routes: rutas,
  scrollBehavior(to) {
    if (to.hash) return { el: to.hash, behavior: "smooth" };
    return { top: 0 };
  }
});

router.beforeEach(async to => {
  if (!to.meta.privada) return true;
  try {
    const cliente = await comprobarSesion();
    if (cliente) return true;
    return { name: "acceso", query: { volver: to.fullPath } };
  } catch {
    return { name: "acceso", query: { error: "servidor" } };
  }
});
