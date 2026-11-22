/*
 * SYNCHRO - Integracion de sesiones de carga
 *
 * No guarda PIN, descriptor facial ni token en
 * localStorage, sessionStorage o URL.
 *
 * Los endpoints permanecen deshabilitados hasta
 * configurar la autorizacion del backend.
 */

let preparacion = null;

async function solicitarCsrf() {
  const respuesta = await fetch(
    "/api/terminales/guardar/whatsapp/csrf-token",
    {
      credentials: "same-origin",
      cache: "no-store"
    }
  );

  if (!respuesta.ok) {
    throw new Error("No se obtuvo el token de seguridad.");
  }

  const datos = await respuesta.json();

  if (typeof datos?.csrfToken !== "string" ||
      !datos.csrfToken) {
    throw new Error("El token CSRF no es valido.");
  }

  return datos.csrfToken;
}

async function solicitar(url, cuerpo, consentimiento = false) {
  const csrf = await solicitarCsrf();

  const cabeceras = {
    "Content-Type": "application/json",
    "X-CSRF-TOKEN": csrf
  };

  if (consentimiento) {
    cabeceras["X-SYNCHRO-CONSENTIMIENTO"] = "ACEPTO";
  }

  const respuesta = await fetch(url, {
    method: "POST",
    credentials: "same-origin",
    cache: "no-store",
    headers: cabeceras,
    body: JSON.stringify(cuerpo)
  });

  const datos = await respuesta.json().catch(() => null);

  if (!respuesta.ok) {
    const causas = {
      400: "Solicitud invalida o token CSRF incorrecto.",
      401: "La terminal no tiene una sesion autenticada.",
      403: "Acceso denegado o fallo de seguridad CSRF.",
      404: "Endpoint deshabilitado o inexistente.",
      409: "Conflicto con una sesion o espacio de carga.",
      413: "La solicitud excede el tamano permitido.",
      429: "Demasiadas solicitudes. Intenta mas tarde.",
      503: "Servicio o base de datos temporalmente no disponible."
    };

    const detalle =
      typeof datos?.mensaje === "string"
        ? datos.mensaje
        : typeof datos?.title === "string"
          ? datos.title
          : causas[respuesta.status] ||
            "El backend rechazo la operacion.";

    throw new Error(
      `SYNCHRO HTTP ${respuesta.status}: ${detalle}`
    );
  }

  return datos;
}

function combinacionValida(espacio, dispositivo, modalidad) {
  return (
    dispositivo === "Celular" &&
    modalidad === "Inalambrica" &&
    /^W[12]$/.test(espacio)
  ) || (
    dispositivo === "Celular" &&
    modalidad === "Cable" &&
    /^C[1-4]$/.test(espacio)
  ) || (
    dispositivo === "Laptop" &&
    modalidad === "Laptop" &&
    /^L[12]$/.test(espacio)
  );
}

/*
 * Se llamara al confirmar el PIN de carga.
 * El rostro debe proceder de la captura V3 del flujo.
 */
export async function prepararCargaSegura({
  codigoEspacio,
  dispositivo,
  modalidad,
  pin,
  descriptor
}) {
  if (!combinacionValida(
    codigoEspacio,
    dispositivo,
    modalidad
  )) {
    throw new Error("Seleccion de carga invalida.");
  }

  if (typeof pin !== "string" || !/^[0-9]{6}$/.test(pin)) {
    throw new Error("El PIN debe contener seis digitos.");
  }

  if (!Array.isArray(descriptor) ||
      descriptor.length !== 1024 ||
      descriptor.some(v =>
        !Number.isFinite(v) || Math.abs(v) > 1000
      )) {
    throw new Error(
      "No se encontro una captura facial V3 valida."
    );
  }

  if (obtenerPreparacionCarga()) {
    throw new Error(
      "Ya existe una preparacion activa en esta tablet."
    );
  }

  const resultado = await solicitar(
    "/api/terminales/carga/preparar",
    {
      codigoEspacio,
      dispositivo,
      modalidad,
      pin,
      descriptor
    },
    true
  );

  if (resultado?.sesionPreparada !== true ||
      resultado?.rostroV3Registrado !== true ||
      resultado?.pinProtegido !== true ||
      resultado?.cargaIniciada !== false ||
      resultado?.codigoEspacio !== codigoEspacio ||
      typeof resultado?.tokenColocacion !== "string" ||
      !resultado.tokenColocacion) {
    throw new Error(
      "El backend no confirmo el registro de carga."
    );
  }

  const vence = Date.parse(resultado.venceUtc);

  if (!Number.isFinite(vence) || vence <= Date.now()) {
    throw new Error(
      "La autorizacion recibida no tiene vigencia valida."
    );
  }

  // Solo token temporal y datos no secretos, en memoria.
  preparacion = {
    token: resultado.tokenColocacion,
    codigoEspacio,
    dispositivo,
    modalidad,
    vence
  };

  return obtenerPreparacionCarga();
}

export function obtenerPreparacionCarga() {
  if (!preparacion) return null;

  if (Date.now() >= preparacion.vence) {
    preparacion = null;
    return null;
  }

  // Nunca exponer el token al template, ruta o almacenamiento.
  return {
    codigoEspacio: preparacion.codigoEspacio,
    dispositivo: preparacion.dispositivo,
    modalidad: preparacion.modalidad,
    vence: preparacion.vence
  };
}

/*
 * Se llamara exclusivamente al pulsar
 * "Ya coloque mi dispositivo".
 */
export async function confirmarColocacionCarga() {
  if (!obtenerPreparacionCarga()) {
    throw new Error(
      "La preparacion vencio o no existe. Reinicia el proceso."
    );
  }

  const resultado = await solicitar(
    "/api/terminales/carga/colocacion/confirmar",
    { tokenColocacion: preparacion.token }
  );

  if (resultado?.colocacionConfirmada !== true ||
      resultado?.sesionIniciada !== true ||
      resultado?.estadoEspacio !== "Ocupado" ||
      resultado?.codigoEspacio !== preparacion.codigoEspacio) {
    throw new Error(
      "El backend no confirmo el inicio de la carga."
    );
  }

  preparacion = null;
  return resultado;
}

export function limpiarPreparacionCarga() {
  preparacion = null;
}