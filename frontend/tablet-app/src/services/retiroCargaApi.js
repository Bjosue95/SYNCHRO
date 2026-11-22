/*
 * SYNCHRO: validacion de rostro V3 y PIN
 * para sesiones de carga activas.
 *
 * No confirma cobros y no libera espacios.
 */

import {
  obtenerEspacioRetiroCarga,
  obtenerTokenFacialRetiroCarga,
  obtenerTokenCobroRetiroCarga,
  guardarRostroRetiroCarga,
  guardarPinValidadoRetiroCarga
} from "./retiroCargaSesion.js";

async function csrf() {
  const respuesta = await fetch(
    "/api/terminales/guardar/whatsapp/csrf-token",
    {
      credentials: "same-origin",
      cache: "no-store"
    }
  );

  if (!respuesta.ok) {
    throw new Error(
      `No se pudo obtener CSRF (HTTP ${respuesta.status}).`
    );
  }

  const datos = await respuesta.json();

  if (typeof datos?.csrfToken !== "string" ||
      !datos.csrfToken) {
    throw new Error("Token CSRF no disponible.");
  }

  return datos.csrfToken;
}

async function publicar(ruta, cuerpo, consentimiento) {
  const tokenCsrf = await csrf();

  const cabeceras = {
    "Content-Type": "application/json",
    "X-CSRF-TOKEN": tokenCsrf
  };

  if (consentimiento) {
    cabeceras["X-SYNCHRO-CONSENTIMIENTO"] = "ACEPTO";
  }

  const respuesta = await fetch(ruta, {
    method: "POST",
    credentials: "same-origin",
    cache: "no-store",
    headers: cabeceras,
    body: JSON.stringify(cuerpo)
  });

  const datos = await respuesta.json().catch(() => null);

  if (!respuesta.ok) {
    const detalle =
      datos?.mensaje ||
      datos?.title ||
      "El servidor rechazo la solicitud.";

    throw new Error(
      `SYNCHRO HTTP ${respuesta.status}: ${detalle}`
    );
  }

  return datos;
}

export async function verificarRostroV3RetiroCarga(descriptor) {
  if (!Array.isArray(descriptor) || descriptor.length !== 1024 ||
      descriptor.some(v => !Number.isFinite(v) || Math.abs(v) > 1000)) {
    throw new Error("Descriptor facial V3 invalido.");
  }
  const resultado = await publicar(
    "/api/terminales/carga/retiro/identificar-v3",
    { descriptor }, true
  );
  if (resultado?.rostroValidado !== true ||
      resultado?.pinRequerido !== true ||
      resultado?.retiroConfirmado !== false ||
      typeof resultado?.tokenFacial !== "string" || !resultado.tokenFacial) {
    throw new Error("El backend no confirmo la verificacion facial.");
  }
  guardarRostroRetiroCarga(resultado.tokenFacial);
  return { rostroValidado: true, pinRequerido: true };
}
export async function verificarPinRetiroCarga(pin) {
  if (typeof pin !== "string" ||
      !/^[0-9]{6}$/.test(pin)) {
    throw new Error("Introduce los seis digitos del PIN.");
  }

  const tokenFacial = obtenerTokenFacialRetiroCarga();

  if (!tokenFacial) {
    throw new Error(
      "La autorizacion facial vencio. Repite el escaneo."
    );
  }

  const resultado = await publicar(
    "/api/terminales/carga/retiro/pin",
    {
      pin,
      tokenFacial
    },
    false
  );

  if (resultado?.pinValidado !== true ||
      resultado?.rostroValidado !== true ||
      resultado?.pagoPendiente !== true ||
      resultado?.retiroConfirmado !== false ||
      typeof resultado?.tokenCobro !== "string") {
    throw new Error(
      "El backend no confirmo la validacion del PIN."
    );
  }

  if (!/^(?:W[12]|C[1-4]|L[12])$/.test(resultado?.codigoEspacio)) {
    throw new Error("El backend no identifico el espacio.");
  }
  guardarPinValidadoRetiroCarga(resultado.tokenCobro, resultado.codigoEspacio);

  return {
    pinValidado: true,
    pagoPendiente: true,
    retiroConfirmado: false
  };
}

// Continuacion del retiro de CARGA. El token nunca pasa por URL.
export async function prepararCobroRetiroCarga() {
  const tokenCobro = obtenerTokenCobroRetiroCarga();
  if (!tokenCobro) {
    throw new Error("La autorizacion del PIN vencio. Repite rostro y PIN.");
  }
  const datos = await publicar(
    "/api/terminales/carga/retiro/cobro/preparar",
    { tokenCobro }, false
  );
  if (!/^(W[12]|C[1-4]|L[12])$/.test(datos?.codigoEspacio) ||
      !Number.isFinite(datos?.importeHnl) || datos.importeHnl <= 0 ||
      !Number.isInteger(datos?.minutosUtilizados) ||
      datos.minutosUtilizados < 1 ||
      !["Pendiente", "ConfirmadoSimulado"].includes(datos?.estadoCobro)) {
    throw new Error("El backend no devolvio una cotizacion valida.");
  }
  return datos;
}

export async function confirmarPagoSimuladoRetiroCarga(metodoPago) {
  const tokenCobro = obtenerTokenCobroRetiroCarga();
  if (!tokenCobro) {
    throw new Error("La autorizacion del PIN vencio. Repite rostro y PIN.");
  }
  if (!["Efectivo", "Tarjeta", "NFC"].includes(metodoPago)) {
    throw new Error("Selecciona un metodo de pago.");
  }
  const datos = await publicar(
    "/api/terminales/carga/retiro/cobro/confirmar-simulado",
    { tokenCobro, metodoPago }, false
  );
  if (datos?.pagoSimulado !== true || datos?.pagoRealProcesado !== false ||
      datos?.retiroConfirmado !== false) {
    throw new Error("No se pudo confirmar el pago simulado.");
  }
  return datos;
}

export async function confirmarRecogidaRetiroCarga() {
  const tokenCobro = obtenerTokenCobroRetiroCarga();
  if (!tokenCobro) {
    throw new Error("La autorizacion del PIN vencio. Repite rostro y PIN.");
  }
  const datos = await publicar(
    "/api/terminales/carga/retiro/cobro/confirmar-recogida",
    { tokenCobro, dispositivoRetirado: true }, false
  );
  if (datos?.retiroConfirmado !== true ||
      datos?.espacioDisponible !== true || datos?.pagoSimulado !== true) {
    throw new Error("No se confirmo la finalizacion del retiro.");
  }
  return datos;
}
