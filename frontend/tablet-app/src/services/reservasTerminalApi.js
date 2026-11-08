/*
 * SYNCHRO
 * Reservas protegidas desde la terminal
 */

export async function reservarCasilleroTerminal(codigoCasillero) {
  // 1. Obtener el token CSRF de la terminal
  const respuestaCsrf = await fetch("/api/terminales/csrf-token", {
    credentials: "same-origin",
    cache: "no-store",
  });

  if (!respuestaCsrf.ok) {
    throw new Error(
      respuestaCsrf.status === 401
        ? "La sesión de la terminal ha vencido."
        : "No se pudo obtener el token de seguridad.",
    );
  }

  const { csrfToken } = await respuestaCsrf.json();

  if (!csrfToken) {
    throw new Error("No se recibió el token CSRF.");
  }

  // 2. Solicitar la reserva a ASP.NET Core
  const respuesta = await fetch("/api/terminales/reservas", {
    method: "POST",
    credentials: "same-origin",
    headers: {
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": csrfToken,
    },
    body: JSON.stringify({
      codigoCasillero,
    }),
  });

  if (!respuesta.ok) {
    const datos = await respuesta.json().catch(() => null);

    throw new Error(
      datos?.mensaje ||
        `No se pudo reservar el casillero (${respuesta.status}).`,
    );
  }

  // 3. Devolver la reserva confirmada por SQL Server
  return await respuesta.json();
}
