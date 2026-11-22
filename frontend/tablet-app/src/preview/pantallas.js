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
  },
  {
    id: "carga-dispositivo",
    titulo: "Qué deseas cargar",
    grupo: "Carga",
    componente: cargar(() => import("../views/CargaDispositivoView.vue"))
  },
  {
    id: "carga-modalidad",
    titulo: "Modalidad de carga",
    grupo: "Carga",
    componente: cargar(() => import("../views/ModalidadCargaView.vue"))
  },
  {
    id: "carga-espacios-inalambrica",
    titulo: "Espacios inalámbricos",
    grupo: "Carga",
    componente: cargar(() => import("../views/EspaciosCargaView.vue"))
  },
  {
    id: "carga-espacios-cable",
    titulo: "Espacios por cable",
    grupo: "Carga",
    componente: cargar(() => import("../views/EspaciosCargaView.vue"))
  },
  {
    id: "carga-espacios-laptop",
    titulo: "Espacios para laptop",
    grupo: "Carga",
    componente: cargar(() => import("../views/EspaciosCargaView.vue"))
  },
  {
    id: "carga-rostro",
    titulo: "Registrar rostro de carga",
    grupo: "Carga",
    componente: cargar(() => import("../views/VerificacionFacialCargaView.vue"))
  },
  {
    id: "carga-crear-pin",
    titulo: "Crear PIN de carga",
    grupo: "Carga",
    componente: cargar(() => import("../views/CrearPinCargaView.vue"))
  },
  {
    id: "carga-confirmar-pin",
    titulo: "Confirmar PIN de carga",
    grupo: "Carga",
    componente: cargar(() => import("../views/CrearPinCargaView.vue"))
  },
  {
    id: "carga-pin-confirmado",
    titulo: "PIN confirmado (transición)",
    grupo: "Carga",
    componente: cargar(() => import("../views/ConfirmacionPinCargaView.vue"))
  },
  {
    id: "carga-colocar",
    titulo: "Colocar dispositivo · reloj",
    grupo: "Carga",
    componente: cargar(() => import("../views/ColocarDispositivoCargaView.vue"))
  },
  {
    id: "carga-colocacion-confirmada",
    titulo: "Colocación confirmada",
    grupo: "Carga",
    componente: cargar(() => import("../views/ColocarDispositivoCargaView.vue"))
  },
  {
    id: "retiro-carga-rostro",
    titulo: "Verificación facial de retiro",
    grupo: "Retiro de dispositivos",
    componente: cargar(() => import("../views/RetiroCargaRostroView.vue"))
  },
  {
    id: "retiro-carga-pin",
    titulo: "Verificar PIN de retiro",
    grupo: "Retiro de dispositivos",
    componente: cargar(() => import("../views/RetiroCargaPinView.vue"))
  },
  {
    id: "retiro-carga-pago",
    titulo: "Seleccionar forma de pago",
    grupo: "Retiro de dispositivos",
    componente: cargar(() => import("../views/SeleccionMetodoPagoView.vue"))
  },
  {
    id: "retiro-carga-espera",
    titulo: "Esperando pago del administrador",
    grupo: "Retiro de dispositivos",
    componente: cargar(() => import("../views/RetiroCargaPagoView.vue"))
  },
  {
    id: "retiro-carga-resumen",
    titulo: "Resumen de consumo",
    grupo: "Retiro de dispositivos",
    componente: cargar(() => import("../views/RetiroCargaPagoView.vue"))
  },
  {
    id: "retiro-carga-completado",
    titulo: "Retiro completado",
    grupo: "Retiro de dispositivos",
    componente: cargar(() => import("../views/RetiroCargaPagoView.vue"))
  }
];

export function buscarPantalla(id) {
  return pantallas.find(p => p.id === id) || null;
}