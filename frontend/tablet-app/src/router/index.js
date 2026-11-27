import { createRouter, createWebHistory } from "vue-router";
import { obtenerOrdenRetiroPagado } from "../services/retiroSesion.js";
import { limpiarRecogidaPendiente } from "../services/recogidaRetiroSesion.js";
import { iniciarRetiroCarga } from "../services/retiroCargaSesion.js";
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
    { path: "/terminal/vincular", name: "terminal-vincular", component: () => import("../views/AutorizarTerminalView.vue") },
    { path: "/guardar/reserva", name: "guardar-reserva", component: () => import("../views/ReservaWebElegirView.vue") },
    { path: "/guardar/reserva/tipo", name: "guardar-reserva-tipo", component: () => import("../views/ReservaWebElegirView.vue") },
    { path: "/guardar/reserva/captura", name: "guardar-reserva-captura", component: VerificacionFacialView },
    { path: "/guardar/reserva/pin", name: "guardar-reserva-pin", component: () => import("../views/ReservaPinView.vue") },
    { path: "/guardar/reserva/deposito", name: "guardar-reserva-deposito", component: DepositoView },
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
    { path: "/retirar/encomienda", name: "retirar-encomienda", component: () => import("../views/RetiroEncomiendaWebView.vue") },
    {
      path: "/retirar/dispositivo",
      name: "retirar-dispositivo",
      component: () => import("../views/RetirarDispositivoView.vue")
    },
    {
      path: "/retirar/dispositivo/rostro",
      name: "retirar-dispositivo-rostro",
      component: () => import("../views/RetiroCargaRostroView.vue")
    },
    {
      path: "/retirar/dispositivo/pin",
      name: "retirar-dispositivo-pin",
      component: () => import("../views/RetiroCargaPinView.vue")
    },
    {
      path: "/retirar/dispositivo/pago",
      name: "retirar-dispositivo-pago",
      component: () => import("../views/SeleccionMetodoPagoView.vue")
    },
    {
      path: "/retirar/dispositivo/pago/resumen",
      name: "retirar-dispositivo-pago-resumen",
      component: () => import("../views/RetiroCargaPagoView.vue")
    },
    { path: "/retirar/pertenencias", name: "retirar-pertenencias", redirect: "/retirar/rostro" },
    { path: "/retirar/rostro", name: "retirar-rostro", component: VerificacionFacialView },
    { path: "/retirar/pin", name: "retirar-pin", component: () => import("../views/RetiroPinView.vue") },
    { path: "/retirar/recoger", name: "retirar-recoger", component: DepositoView },
    { path: "/retirar/reserva/recoger", redirect: "/retirar/recoger" },
    { path: "/retirar/pago", name: "retirar-pago", component: () => import("../views/SeleccionMetodoPagoView.vue") },
    { path: "/retirar/pago/resumen", name: "retirar-pago-resumen", component: () => import("../views/RetiroPagoView.vue") },
    { path: "/cargar", name: "cargar-dispositivo", component: () => import("../views/CargaDispositivoView.vue") },
    { path: "/cargar/modalidad", name: "cargar-modalidad", component: () => import("../views/ModalidadCargaView.vue") },
    { path: "/cargar/espacios", name: "cargar-espacios", component: () => import("../views/EspaciosCargaView.vue") },
    { path: "/cargar/rostro", name: "cargar-rostro", component: () => import("../views/VerificacionFacialCargaView.vue") },
    { path: "/cargar/crear-pin", name: "cargar-crear-pin", component: () => import("../views/CrearPinCargaView.vue") },
    { path: "/cargar/pin-confirmado", name: "cargar-pin-confirmado", component: () => import("../views/ConfirmacionPinCargaView.vue") },
    { path: "/cargar/colocar", name: "cargar-colocar", component: () => import("../views/ColocarDispositivoCargaView.vue") },
    { path: "/cargar/condiciones", name: "cargar-condiciones", component: () => import("../views/CondicionesCargaView.vue") },
    { path: "/:pathMatch(.*)*", redirect: "/menu" },
  ],
});

// SYNCHRO-RETIRO-AUTO-GUARD-V1: el usuario no elige espacio.
router.beforeEach((to, from) => {
  if(to.path!=="/retirar/recoger"&&to.path!=="/retirar/reserva/recoger")limpiarRecogidaPendiente();
  // Una transición desde el PIN nunca debe provocar un regreso silencioso al rostro.
  if (from.path === "/retirar/pin" && to.path === "/retirar/pago" && !obtenerOrdenRetiroPagado()) return false;
  if (to.path === "/retirar/dispositivo") {
    iniciarRetiroCarga();
    return "/retirar/dispositivo/rostro";
  }
});
registrarRutasVistaPrevia(router);

export default router;

