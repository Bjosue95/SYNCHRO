import { tokenReservaValido, referenciaReservaValida } from "./validaciones.js";

export class ErrorApi extends Error {
  constructor(mensaje, estado, codigo = "respuesta") {
    super(mensaje);
    this.name = "ErrorApi";
    this.estado = estado;
    this.codigo = codigo;
  }
}

async function interpretar(respuesta) {
  let datos = null;
  try {
    datos = await respuesta.json();
  } catch {
    // La respuesta puede estar vacía (401, 403, 503).
  }
  if (!respuesta.ok) {
    const mensaje = datos?.mensaje || datos?.title ||
      (respuesta.status === 401 ? "Tu sesión ha terminado. Inicia sesión nuevamente." :
        respuesta.status === 403 ? "No tienes autorización para esta operación." :
          respuesta.status === 404 ? "No se encontró el registro solicitado." :
            respuesta.status === 409 ? "Esta operación ya no está disponible." :
              respuesta.status === 429 ? "Hay muchas solicitudes en este momento. Espera un minuto e inténtalo nuevamente." :
              respuesta.status === 503 ? "El servicio no está disponible en este momento." :
                "No se pudo completar la solicitud.");
    throw new ErrorApi(mensaje, respuesta.status);
  }
  return datos;
}

async function solicitar(ruta, { metodo = "GET", datos, cabeceras = {} } = {}) {
  const headers = { Accept: "application/json", ...cabeceras };
  if (metodo !== "GET" && metodo !== "HEAD") {
    // Los tokens antifalsificación están asociados a la identidad.
    // Antes de autenticar: token anónimo. Después: token del cliente actual.
    const accesoPublico = ruta === "/api/web/clientes/acceso/registro" ||
      ruta === "/api/web/clientes/acceso/login";
    const csrf = await obtenerCsrf({ autenticado: !accesoPublico });
    headers["X-CSRF-TOKEN"] = csrf;
    if (datos !== undefined) headers["Content-Type"] = "application/json";
  }

  let respuesta;
  try {
    respuesta = await fetch(ruta, {
      method: metodo,
      credentials: "same-origin",
      cache: "no-store",
      headers,
      ...(datos !== undefined ? { body: JSON.stringify(datos) } : {})
    });
  } catch {
    throw new ErrorApi("No hay conexión con el backend de SYNCHRO.", 0, "conexion");
  }
  return interpretar(respuesta);
}

export async function obtenerCsrf({ autenticado = false } = {}) {
  // El endpoint privado emite un token vinculado a la cookie Clientes.
  // No utilizar el endpoint anónimo para logout ni para pre-reservas.
  const rutaCsrf = autenticado
    ? "/api/web/clientes/csrf-token"
    : "/api/web/clientes/acceso/csrf-token";
  let respuesta;
  try {
    respuesta = await fetch(rutaCsrf, {
      method: "GET",
      credentials: "same-origin",
      cache: "no-store",
      headers: { Accept: "application/json" }
    });
  } catch {
    throw new ErrorApi("No hay conexión segura con el backend.", 0, "conexion");
  }
  const datos = await interpretar(respuesta);
  if (!datos?.csrfToken) throw new ErrorApi("No fue posible verificar la operación.", 0, "csrf");
  return datos.csrfToken;
}

export const apiClientes = Object.freeze({
  registrar: ({ nombreUsuario, contrasena, telefono }) =>
    solicitar("/api/web/clientes/acceso/registro", {
      metodo: "POST", datos: { nombreUsuario, contrasena, telefono }
    }),
  ingresar: ({ nombreUsuario, contrasena }) =>
    solicitar("/api/web/clientes/acceso/login", {
      metodo: "POST", datos: { nombreUsuario, contrasena }
    }),
  sesion: () => solicitar("/api/web/clientes/mi-sesion"),
  salir: () => solicitar("/api/web/clientes/logout", { metodo: "POST" }),

  crearPreReserva: ({ telefonoDestinatario, tamanoSolicitado }) =>
    solicitar("/api/web/encomiendas/pre-reservas", {
      metodo: "POST", datos: { telefonoDestinatario, tamanoSolicitado }
    }),

  consultarPreReserva: (id, tokenGestion) => {
    if (!referenciaReservaValida(id) || !tokenReservaValido(tokenGestion)) {
      throw new ErrorApi("Referencia o clave de consulta inválida.", 400);
    }
    // Credencial exclusivamente en cabecera: nunca en URL ni en almacenamiento.
    return solicitar("/api/web/encomiendas/pre-reservas/" + Number(id), {
      cabeceras: { "X-SYNCHRO-RESERVA": tokenGestion.trim() }
    });
  },

  iniciarConsentimiento: (id, tokenGestion) => {
    if (!referenciaReservaValida(id) || !tokenReservaValido(tokenGestion)) {
      throw new ErrorApi("Referencia o clave de consulta inválida.", 400);
    }
    return solicitar(
      "/api/web/encomiendas/pre-reservas/" + Number(id) +
        "/consentimiento/iniciar", {
        metodo: "POST",
        cabeceras: { "X-SYNCHRO-RESERVA": tokenGestion.trim() }
      }
    );
  }
});

// Compartir transporte, cookies y CSRF autenticado con la gestión operativa.
export { solicitar as solicitarCliente };
