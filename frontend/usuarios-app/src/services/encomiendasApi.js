import { solicitarCliente } from "./clientesApi.js";
const base = "/api/web/encomiendas";
export const encomiendasApi = Object.freeze({
  configuracion: () => solicitarCliente(`${base}/configuracion`),
  listar: () => solicitarCliente(base),
  detalle: (id) => solicitarCliente(`${base}/${Number(id)}`),
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
