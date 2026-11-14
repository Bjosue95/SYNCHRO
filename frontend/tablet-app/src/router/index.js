import { createRouter, createWebHistory } from "vue-router";

import BienvenidaView from "../views/BienvenidaView.vue";
import MenuView from "../views/MenuView.vue";
import SeleccionTamanoView from "../views/SeleccionTamanoView.vue";
import SeleccionCasilleroView from "../views/SeleccionCasilleroView.vue";
import GuardarView from "../views/GuardarView.vue";

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),

  routes: [
    {
      path: "/",
      component: BienvenidaView,
    },
    {
      path: "/menu",
      component: MenuView,
    },
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
      path: "/:pathMatch(.*)*",
      redirect: "/menu",
    },
  ],
});

export default router;

