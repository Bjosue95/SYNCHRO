// El modo demo es explicito y solo existe en el servidor de desarrollo.
// Nunca sustituye silenciosamente un fallo de la API por una aprobacion ficticia.
export const esDemo = import.meta.env.DEV && import.meta.env.MODE === "demo";
export class ErrorTablet extends Error {
  constructor(mensaje, estado) {
    super(mensaje);
    this.estado = estado;
  }
}
export async function solicitar(
  ruta,
  {
    metodo = "GET",
    datos,
    encabezados = {},
    csrf = "/api/terminales/encomiendas/csrf-token",
    signal,
  } = {},
) {
  if (esDemo)
    return (await import("./demo/tabletDemo.js")).solicitarDemo(ruta, {
      metodo,
      datos,
      encabezados,
    });
  const opciones = {
    method: metodo,
    credentials: "same-origin",
    cache: "no-store",
    headers: { ...encabezados },
    signal,
  };
  if (metodo !== "GET") {
    const respuesta = await fetch(csrf, {
      credentials: "same-origin",
      cache: "no-store",
      signal,
    });
    if (!respuesta.ok)
      throw new ErrorTablet(
        respuesta.status === 401
          ? "Vincula la terminal para continuar."
          : "No se pudo preparar la operación. Intenta de nuevo.",
        respuesta.status,
      );
    const { csrfToken } = await respuesta.json();
    if (!csrfToken)
      throw new ErrorTablet("No se pudo proteger la operación.", 503);
    opciones.headers["X-CSRF-TOKEN"] = csrfToken;
  }
  if (datos instanceof Blob) {
    opciones.headers["Content-Type"] = "application/octet-stream";
    opciones.body = datos;
  } else if (datos !== undefined) {
    opciones.headers["Content-Type"] = "application/json";
    opciones.body = JSON.stringify(datos);
  }
  const respuesta = await fetch(ruta, opciones);
  const cuerpo = await respuesta.json().catch(() => null);
  if (!respuesta.ok) {
    const mensajes = {
      401: "La sesión venció. Verifica tu acceso antes de continuar.",
      403: "No se pudo verificar tu identidad. Revisa la captura y tu sesión.",
      404: "No encontramos una reserva disponible con esos datos.",
      409: "La operación no está disponible. Revisa el PIN y el estado de la reserva.",
      429: "Demasiados intentos. Espera un momento y vuelve a intentarlo.",
      503: "El servicio no está disponible en este momento. No se ha confirmado la operación.",
    };
    throw new ErrorTablet(
      mensajes[respuesta.status] || "No pudimos completar la operación.",
      respuesta.status,
    );
  }
  return cuerpo;
}
