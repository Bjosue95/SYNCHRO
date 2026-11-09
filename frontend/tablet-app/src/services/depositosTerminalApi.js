/*
 * SYNCHRO
 * Depósito protegido de pertenencias personales
 */

export async function registrarDepositoPersonal(usoId, tokenDeposito) {
  // 1. Validar los datos de la reserva.
  if (!Number.isInteger(usoId) || usoId <= 0) {
    throw new Error("Identificador de reserva inválido.");
  }

  if (
    typeof tokenDeposito !== "string" ||
    !/^[0-9a-fA-F]{64}$/.test(tokenDeposito)
  ) {
    throw new Error("Token de depósito inválido.");
  }

  // 2. Obtener el token CSRF de la terminal.
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

  // 3. Solicitar el depósito a ASP.NET Core.
  const respuesta = await fetch("/api/terminales/depositos", {
    method: "POST",
    credentials: "same-origin",
    cache: "no-store",
    headers: {
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": csrfToken,
    },
    body: JSON.stringify({
      usoId,
      tokenDeposito,
    }),
  });

  // 4. Comprobar el resultado.
  if (!respuesta.ok) {
    const datos = await respuesta.json().catch(() => null);

    throw new Error(
      datos?.mensaje ||
        (respuesta.status === 401
          ? "La sesión de la terminal ha vencido."
          : `No se pudo registrar el depósito (${respuesta.status}).`),
    );
  }

  // 5. Devolver la confirmación de SQL Server.
  return await respuesta.json();
}
