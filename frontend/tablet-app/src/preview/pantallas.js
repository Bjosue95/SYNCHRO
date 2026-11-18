import { defineAsyncComponent, markRaw } from "vue";

const cargar = fn => markRaw(defineAsyncComponent(fn));

export const pantallas = [
  {
    id: "bienvenida",
    titulo: "Bienvenida",
    grupo: "Inicio",
    componente: cargar(() => import("../views/BienvenidaView.vue"))
  },
  {
    id: "menu",
    titulo: "Menú principal",
    grupo: "Inicio",
    componente: cargar(() => import("../views/MenuView.vue"))
  },
  {
    id: "guardar",
    titulo: "Qué deseas guardar",
    grupo: "Depósito",
    componente: cargar(() => import("../views/GuardarView.vue"))
  },
  {
    id: "tamano",
    titulo: "Tamaño del casillero",
    grupo: "Depósito",
    componente: cargar(() => import("../views/SeleccionTamanoView.vue"))
  },
  {
    id: "casillero",
    titulo: "Seleccionar casillero",
    grupo: "Depósito",
    componente: cargar(() => import("../views/SeleccionCasilleroView.vue"))
  },
  {
    id: "rostro-guardar",
    titulo: "Verificación facial",
    grupo: "Depósito",
    componente: cargar(() => import("../views/VerificacionFacialView.vue"))
  },
  {
    id: "whatsapp",
    titulo: "Verificación WhatsApp",
    grupo: "Depósito",
    componente: cargar(() => import("../views/VerificacionWhatsappView.vue"))
  },
  {
    id: "deposito",
    titulo: "Colocar pertenencias",
    grupo: "Depósito",
    componente: cargar(() => import("../views/DepositoView.vue"))
  },
  {
    id: "retirar",
    titulo: "Qué deseas retirar",
    grupo: "Retiro",
    componente: cargar(() => import("../views/RetirarView.vue"))
  },
  {
    id: "rostro-retirar",
    titulo: "Verificación para retiro",
    grupo: "Retiro",
    componente: cargar(() => import("../views/VerificacionFacialView.vue"))
  },
  {
    id: "pin",
    titulo: "Introducir PIN",
    grupo: "Retiro",
    componente: cargar(() => import("../views/RetiroPinView.vue"))
  },
  {
    id: "pago-resumen",
    titulo: "Resumen de consumo",
    grupo: "Retiro",
    componente: cargar(() => import("../views/RetiroPagoView.vue"))
  },
  {
    id: "pago",
    titulo: "Seleccionar pago",
    grupo: "Retiro",
    componente: cargar(() => import("../views/SeleccionMetodoPagoView.vue"))
  }
];

export function buscarPantalla(id) {
  return pantallas.find(p => p.id === id) || null;
}