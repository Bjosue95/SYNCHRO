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
    path: "/bienvenida", name: "bienvenida",
    meta: { privada: true, titulo: "Mi espacio" },
    component: () => import("../views/BienvenidaView.vue")
  },
  {
    path: "/mis-reservas", alias: "/mi-cuenta", name: "cuenta",
    meta: { privada: true, titulo: "Mis reservas" },
    component: () => import("../views/PanelView.vue")
  },
  {
    path: "/reservas/:id(\\d+)", alias: "/encomiendas/:id(\\d+)", name: "detalle-encomienda",
    meta: { privada: true, titulo: "Detalle de reserva" },
    component: () => import("../views/EncomiendaDetalleView.vue")
  },
  {
    path: "/reservas/:id(\\d+)/solicitar-pin", name: "solicitar-pin",
    meta: { privada: true, titulo: "PIN de depósito" },
    component: () => import("../views/SolicitarPinView.vue")
  },
  {
    path: "/reservas/:id(\\d+)/pago-pendiente", name: "pago-pendiente",
    meta: { privada: true, titulo: "Confirmación del pago" },
    component: () => import("../views/PagoPendienteView.vue")
  },
  {
    path: "/reservar", alias: "/reservar/encomienda", name: "encomienda",
    meta: { privada: true, titulo: "Reservar casillero" },
    component: () => import("../views/PreReservaView.vue")
  },
  {
    path: "/mi-perfil", name: "perfil",
    meta: { privada: true, titulo: "Mi perfil" },
    component: () => import("../views/PerfilView.vue")
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
  async scrollBehavior(to) {
    if (to.hash) {
      const behavior = window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth";
      if (to.hash === "#inicio") return { top: 0, behavior };
      const id = decodeURIComponent(to.hash.slice(1));
      // La transición out-in puede mantener la sección destino fuera del DOM al cambiar de ruta.
      const el = document.getElementById(id) || await new Promise(resolve => {
        const observador = new MutationObserver(() => {
          const destino = document.getElementById(id);
          if (!destino) return;
          clearTimeout(limite);
          observador.disconnect();
          resolve(destino);
        });
        const limite = setTimeout(() => { observador.disconnect(); resolve(null); }, 1500);
        observador.observe(document.body, { childList: true, subtree: true });
      });
      // Medir al terminar la entrada evita descontar el translateY de la vista.
      const vista = el?.closest(".vista-pagina-enter-active");
      if (vista) await Promise.allSettled(vista.getAnimations().map(animacion => animacion.finished));
      if (!el || router.currentRoute.value.fullPath !== to.fullPath) return false;
      const altoCabecera = document.querySelector(".cabecera-publica")?.getBoundingClientRect().height || 0;
      return { el, top: altoCabecera + 16, behavior };
    }
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
