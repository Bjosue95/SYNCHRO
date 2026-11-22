// SYNCHRO - Catalogo visual, exclusivo del desarrollo local.
import { buscarPantalla } from "./pantallas.js";
import { infoCarga } from "../services/catalogoEspaciosCarga.js";

const cargaCelular = Object.freeze({
  dispositivo: "Celular",
  modalidad: "Inalambrica",
  espacio: "W2"
});

const ejemplos = {
  tamano: { objeto: "compras" },
  casillero: { objeto: "compras", tamano: "M" },
  "rostro-guardar": { objeto: "compras", tamano: "M", casillero: "M03" },
  whatsapp: { casillero: "M03" },
  deposito: { casillero: "M03" },
  "carga-espacios-inalambrica": { dispositivo: "Celular", modalidad: "Inalambrica" },
  "carga-espacios-cable": { dispositivo: "Celular", modalidad: "Cable" },
  "carga-espacios-laptop": { dispositivo: "Laptop" },
  "carga-rostro": cargaCelular,
  "carga-crear-pin": cargaCelular,
  "carga-confirmar-pin": cargaCelular,
  "carga-pin-confirmado": cargaCelular,
  "carga-colocar": cargaCelular,
  "carga-colocacion-confirmada": cargaCelular,
  "retiro-carga-pago": { metodo: "Efectivo" },
  "retiro-carga-espera": { metodo: "Efectivo" },
  "retiro-carga-resumen": { metodo: "Efectivo" },
  "retiro-carga-completado": { metodo: "Efectivo" }
};

export function registrarRutasVistaPrevia(router) {
  if (!import.meta.env.DEV || import.meta.env.MODE === "demo") return;

  router.addRoute({
    path: "/vista-previa",
    name: "catalogo-vistas-previas",
    component: () => import("./views/CatalogoView.vue")
  });

  router.addRoute({
    path: "/vista-previa/:pantalla",
    name: "pantalla-vista-previa",
    component: () => import("./views/PantallaView.vue"),
    beforeEnter(to) {
      // La lista admitida se obtiene del catalogo real para evitar
      // incorporar rutas sueltas de la aplicacion operativa.
      const id = String(to.params.pantalla ?? "");
      if (!buscarPantalla(id)) return "/vista-previa";

      const esperado = { ...(ejemplos[id] || {}) };
      // En un recorrido animado, respetar el espacio elegido en las
      // tarjetas de muestra sin permitir una combinacion incompatible.
      if (Object.prototype.hasOwnProperty.call(esperado, "espacio")) {
        const info = infoCarga(to.query.espacio);
        if (info &&
            info.dispositivo === to.query.dispositivo &&
            info.modalidad === to.query.modalidad) {
          esperado.espacio = to.query.espacio;
          esperado.dispositivo = to.query.dispositivo;
          esperado.modalidad = to.query.modalidad;
        }
      }
      // Mantener Tarjeta/NFC al navegar desde la vista previa de
      // seleccion de pago, sin sobrescribirlos por el valor inicial.
      if ("metodo" in esperado &&
          ["Efectivo", "Tarjeta", "NFC"].includes(to.query.metodo)) {
        esperado.metodo = to.query.metodo;
      }
      const falta = Object.keys(esperado).some(
        campo => to.query[campo] !== esperado[campo]
      );

      if (falta) {
        return {
          path: to.path,
          query: { ...to.query, ...esperado },
          replace: true
        };
      }

      return true;
    }
  });
}
