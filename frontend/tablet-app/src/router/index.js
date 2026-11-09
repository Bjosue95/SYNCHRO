import { createRouter, createWebHistory } from "vue-router";

import BienvenidaView from "../views/BienvenidaView.vue";
import MenuView from "../views/MenuView.vue";
import RetirarView from "../views/RetirarView.vue";
import RetirarPertenenciasView from "../views/RetirarPertenenciasView.vue";
import GuardarView from "../views/GuardarView.vue";
import PertenenciasView from "../views/PertenenciasView.vue";
import SeleccionTamanoView from "../views/SeleccionTamanoView.vue";
import SeleccionCasilleroView from "../views/SeleccionCasilleroView.vue";
import VinculacionTerminalView from "../views/VinculacionTerminalView.vue";
import ReservaExistenteTerminalView from "../views/ReservaExistenteTerminalView.vue";
import EncomiendaVisitanteTerminalView from "../views/EncomiendaVisitanteTerminalView.vue";

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),

  routes: [
    { path: "/terminal/encomiendas/reserva", name: "terminal-reserva-encomienda", component: ReservaExistenteTerminalView },
    { path: "/guardar/encomienda-visitante", name: "terminal-envio-visitante", component: EncomiendaVisitanteTerminalView },
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
      path: "/retirar",
      name: "retirar",
      component: RetirarView,
    },
    {
      path: "/retirar/pertenencias",
      name: "retirar-pertenencias",
      component: RetirarPertenenciasView,
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
