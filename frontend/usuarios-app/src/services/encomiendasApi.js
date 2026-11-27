import { solicitarCliente } from "./clientesApi.js";
const base = "/api/web/encomiendas";
const reservas = "/api/web/reservas";
export const encomiendasApi = Object.freeze({
  disponibilidad: () => solicitarCliente(`${reservas}/disponibilidad`),
  reservar: (datos) => solicitarCliente(reservas, { metodo: "POST", datos }),
  configuracion: () => solicitarCliente(`${base}/configuracion`),
  listar: (filtros = {}) => {
    const consulta = new URLSearchParams(Object.entries(filtros).filter(([, valor]) => valor !== '' && valor != null).map(([clave, valor]) => [clave, String(valor)]));
    return solicitarCliente(consulta.size ? `${reservas}?${consulta}` : reservas);
  },
  detalle: (id) => solicitarCliente(`${reservas}/${Number(id)}`),
  solicitarPinDeposito: (id) => solicitarCliente(`${reservas}/${Number(id)}/pin-deposito/solicitar`, { metodo: "POST" }),
  cancelar: (id, datos) => solicitarCliente(`${reservas}/${Number(id)}/cancelar`, { metodo: "POST", datos }),
  consentimientoCuenta: () => solicitarCliente(`${base}/consentimiento-cuenta`),
  revocarConsentimientoCuenta: () => solicitarCliente(`${base}/consentimiento-cuenta`, { metodo: "DELETE" }),
  crear: (datos) => solicitarCliente(base, { metodo: "POST", datos }),
  referencia: () => solicitarCliente(`${base}/referencia-facial`),
  revocarReferencia: () =>
    solicitarCliente(`${base}/referencia-facial`, { metodo: "DELETE" }),
  async registrarReferencia(captura, contrasena, versionConsentimiento) {
    const datos = {
      ...captura,
      descriptor: [...captura.descriptor],
      contrasena,
      versionConsentimiento,
    };
    try {
      return await solicitarCliente(`${base}/referencia-facial`, {
        metodo: "POST",
        datos,
        cabeceras: { "X-SYNCHRO-CONSENTIMIENTO": "ACEPTO" },
      });
    } finally {
      datos.descriptor.fill(0);
      datos.contrasena = "";
      datos.imagenPng = "";
    }
  },
  async autorizarDeposito(
    id,
    solicitudId,
    pin,
    captura,
    versionConsentimiento,
  ) {
    const copia = {
      ...captura,
      descriptor: [...captura.descriptor],
      versionConsentimiento,
    };
    try {
      return await solicitarCliente(
        `${base}/${Number(id)}/deposito/autorizar`,
        {
          metodo: "POST",
          datos: { solicitudId, pin, captura: copia },
          cabeceras: { "X-SYNCHRO-CONSENTIMIENTO": "ACEPTO" },
        },
      );
    } finally {
      copia.descriptor.fill(0);
      copia.imagenPng = "";
    }
  },
});
