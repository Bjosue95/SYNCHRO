import { createRouter, createWebHistory } from "vue-router";
import { registrarRutasVistaPrevia } from "../preview/rutas.js";

import BienvenidaView from "../views/BienvenidaView.vue";
import MenuView from "../views/MenuView.vue";
import SeleccionTamanoView from "../views/SeleccionTamanoView.vue";
import SeleccionCasilleroView from "../views/SeleccionCasilleroView.vue";
import GuardarView from "../views/GuardarView.vue";
import VerificacionFacialView from "../views/VerificacionFacialView.vue";
import VerificacionWhatsappView from "../views/VerificacionWhatsappView.vue";
import DepositoView from "../views/DepositoView.vue";

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),

  routes: [
    { path: "/", component: BienvenidaView },
    { path: "/menu", component: MenuView },

    {
      path: "/guardar",
      name: "guardar",
      component: GuardarView,
    },
    {
      path: "/guardar/tamano",
      name: "guardar-tamano",
      component: SeleccionTamanoView,
    },
    {
      path: "/guardar/casillero",
      name: "guardar-casillero",
      component: SeleccionCasilleroView,
    },
    {
      path: "/guardar/verificar-rostro",
      name: "guardar-verificar-rostro",
      component: VerificacionFacialView,
    },
    {
      path: "/guardar/verificar-whatsapp",
      name: "guardar-verificar-whatsapp",
      component: VerificacionWhatsappView,
    },
    {
      path: "/guardar/deposito",
      name: "guardar-deposito",
      component: DepositoView,
    },

    { path: "/retirar", name: "retirar", component: () => import("../views/RetirarView.vue") },
    { path: "/retirar/pertenencias", name: "retirar-pertenencias", redirect: "/retirar/rostro" },
    { path: "/retirar/rostro", name: "retirar-rostro", component: VerificacionFacialView },
    { path: "/retirar/pin", name: "retirar-pin", component: () => import("../views/RetiroPinView.vue") },
    { path: "/retirar/pago", name: "retirar-pago", component: () => import("../views/SeleccionMetodoPagoView.vue") },
    { path: "/retirar/pago/resumen", name: "retirar-pago-resumen", component: () => import("../views/RetiroPagoView.vue") },
    { path: "/:pathMatch(.*)*", redirect: "/menu" },
  ],
});

registrarRutasVistaPrevia(router);

export default router;