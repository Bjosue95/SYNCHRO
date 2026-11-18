// SYNCHRO - Catalogo visual, exclusivo del desarrollo local.
export function registrarRutasVistaPrevia(router) {
  if (!import.meta.env.DEV || import.meta.env.MODE === "demo") {
    return;
  }

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
      const permitidas = new Set([
        "bienvenida",
        "menu",
        "guardar",
        "tamano",
        "casillero",
        "rostro-guardar",
        "whatsapp",
        "deposito",
        "retirar",
        "rostro-retirar",
        "pin",
        "pago-resumen",
        "pago"
      ]);

      if (!permitidas.has(to.params.pantalla)) {
        return "/vista-previa";
      }

      const ejemplos = {
        tamano: {
          objeto: "compras"
        },
        casillero: {
          objeto: "compras",
          tamano: "M"
        },
        "rostro-guardar": {
          objeto: "compras",
          tamano: "M",
          casillero: "M03"
        },
        whatsapp: {
          casillero: "M03"
        },
        deposito: {
          casillero: "M03"
        }
      };

      const esperado = ejemplos[to.params.pantalla] || {};

      const falta = Object.keys(esperado).some(
        campo => to.query[campo] !== esperado[campo]
      );

      if (falta) {
        return {
          path: to.path,
          query: esperado,
          replace: true
        };
      }

      return true;
    }
  });
}