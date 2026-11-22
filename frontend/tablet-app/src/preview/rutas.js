// SYNCHRO - Catalogo visual, exclusivo del desarrollo local.
import { buscarPantalla } from "./pantallas.js";

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

      const esperado = ejemplos[id] || {};
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
