import { createRouter, createWebHistory } from "vue-router";

import BienvenidaView from "../views/BienvenidaView.vue";
import MenuView from "../views/MenuView.vue";
import GuardarView from "../views/GuardarView.vue";
import PertenenciasView from "../views/PertenenciasView.vue";
import SeleccionTamanoView from "../views/SeleccionTamanoView.vue";
import SeleccionCasilleroView from "../views/SeleccionCasilleroView.vue";
import VinculacionTerminalView from "../views/VinculacionTerminalView.vue";

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),

  routes: [
    {
      path: "/",
      name: "bienvenida",
      component: BienvenidaView,
    },

    {
      path: "/terminal/vinculacion",
      name: "vinculacion-terminal",
      component: VinculacionTerminalView,
    },

    {
      path: "/menu",
      name: "menu",
      component: MenuView,
    },
    {
      path: "/guardar",
      name: "guardar",
      component: GuardarView,
    },

    {
      path: "/guardar/pertenencias",
      name: "pertenencias",
      component: PertenenciasView,
    },

    {
      path: "/guardar/pertenencias/tamano",
      name: "seleccion-tamano",
      component: SeleccionTamanoView,
    },

    {
      path: "/guardar/pertenencias/casillero",
      name: "seleccion-casillero",
      component: SeleccionCasilleroView,
    },
  ],
});

export default router;
