// Fixtures de navegacion exclusivas del modo `vite --mode demo`.
// No son el backend, no acreditan identidad y nunca llaman a servicios externos.
const credencialEjemplo = "B".repeat(64);
const clonar = (x) => structuredClone(x);
function fallo(mensaje, estado = 409) {
  const e = new Error(mensaje);
  e.estado = estado;
  throw e;
}
export function crearEscenarioDemo() {
  const espacios = [];
  for (const [prefijo, clase, conector, temp] of [
    ["C", "CargaCelular", "USB-C", null],
    ["L", "CargaLaptop", "USB-C PD", null],
    ["P", "Personal", null, null],
    ["F", "Frio", null, 5],
    ["H", "Calor", null, 55],
  ])
    for (let i = 1; i <= 3; i++)
      espacios.push({
        configuracion: {
          codigo: `${prefijo}0${i}`,
          clase,
          conector:
            clase === "CargaCelular" && i === 2 ? "Inalambrico" : conector,
          habilitado: true,
          tarifas: { 30: 15, 60: 25, 90: 35 },
          tamano: "Mediano",
          temperaturaSimulada: temp,
        },
        estado: "Disponible",
        hardwareSimulado: true,
      });
  const operaciones = new Map();
  const precargada = {
    id: "00000000-0000-4000-8000-000000000001",
    codigo: "P01",
    clase: "Personal",
    estado: "Activa",
    minutos: 30,
    monto: 15,
    moneda: "HNL",
  };
  operaciones.set(precargada.id, {
    operacion: precargada,
    credencial: credencialEjemplo,
    pin: "654321",
    intentos: 0,
  });
  espacios.find((x) => x.configuracion.codigo === "P01").estado = "Ocupado";
  let sesion = false,
    depositada = false,
    retirada = false,
    intentosEncomienda = 0;
  const evidencias = new Map();
  return async function solicitarDemo(
    ruta,
    { metodo = "GET", datos, encabezados = {} } = {},
  ) {
    const path = new URL(ruta, "http://demo.local").pathname;
    if (path === "/api/servicios/compartimentos" && metodo === "GET")
      return clonar(espacios);
    if (path === "/api/destinatarios/login" && metodo === "POST") {
      if (datos?.verificacionId !== 12 || datos?.codigo !== "123456")
        fallo("Código de ejemplo incorrecto.", 401);
      sesion = true;
      return { autenticado: true };
    }
    if (path === "/api/destinatarios/mi-sesion") {
      if (!sesion)
        fallo("Primero verifica tu acceso con los datos de ejemplo.", 401);
      return { autenticado: true };
    }
    if (
      path === "/api/terminales/encomiendas/reservas/consultar" &&
      metodo === "POST"
    ) {
      if (
        datos?.referencia !== 1 ||
        datos?.tokenGestion !== "A".repeat(64) ||
        depositada
      )
        fallo("Reserva de ejemplo no disponible.", 404);
      return {
        referencia: 1,
        tamanoSolicitado: "Mediano",
        estado: "PendienteConsentimiento",
        puedeContinuarVerificacion: true,
        consentimientoConfirmado: true,
        mensaje: "Consentimiento de ejemplo confirmado.",
      };
    }
    if (
      path.includes("/encomiendas/") &&
      path.includes("/reservas/1/") &&
      encabezados["X-SYNCHRO-RESERVA"] !== "A".repeat(64)
    )
      fallo("Credencial de ejemplo incorrecta.", 404);
    if (
      path.endsWith("/evidencias-remitente") ||
      path.endsWith("/evidencias-destinatario")
    ) {
      const destinatario = path.endsWith("/evidencias-destinatario");
      if (destinatario && !sesion)
        fallo("Verifica el acceso del destinatario.", 401);
      if (!(datos instanceof Blob) || datos.type !== "image/png" || !datos.size)
        fallo("Se requiere una captura PNG de ejemplo.", 400);
      const evidenciaId = crypto.randomUUID(),
        venceUtc = new Date(Date.now() + 120000).toISOString();
      evidencias.set(evidenciaId, { destinatario, venceUtc });
      return { evidenciaId, venceUtc, simulado: true };
    }
    if (
      path.endsWith("/encomiendas/reservas/1/depositar") ||
      path.endsWith("/encomiendas/usos/17/retirar")
    ) {
      const retiro = path.endsWith("/retirar"),
        e = evidencias.get(datos?.evidenciaId);
      if (!e || e.destinatario !== retiro || new Date(e.venceUtc) <= new Date())
        fallo("Vuelve a capturar tu rostro.");
      if (retiro) {
        if (!sesion || retirada) fallo("Retiro de ejemplo no disponible.");
        if (intentosEncomienda >= 5) fallo("PIN de ejemplo bloqueado.");
        if (datos.pin !== "654321") {
          intentosEncomienda++;
          fallo("PIN incorrecto. Para esta demostración utiliza 654321.");
        }
        retirada = true;
      } else {
        if (depositada) fallo("La reserva ya fue utilizada.");
        if (datos.codigoCasillero !== "E01")
          fallo("Para esta demostración utiliza el casillero E01.");
        depositada = true;
        retirada = false;
      }
      evidencias.delete(datos.evidenciaId);
      return {
        usoId: 17,
        estado: retiro ? "Finalizado" : "Ocupado",
        hardwareSimulado: true,
      };
    }
    const match = path.match(
      /^\/api\/terminales\/servicios\/([A-Za-z0-9-]+)\/reservas(?:\/([a-f0-9-]+)(?:\/(pagar|iniciar|retirar|cancelar))?)?$/,
    );
    if (match && metodo === "POST") {
      const [, codigo, id, accion] = match,
        espacio = espacios.find((x) => x.configuracion.codigo === codigo);
      if (!espacio) fallo("Compartimento no disponible.", 404);
      if (!id) {
        if (
          espacio.estado !== "Disponible" ||
          !Object.hasOwn(espacio.configuracion.tarifas, datos?.minutos)
        )
          fallo("Ese espacio o duración no está disponible.");
        const nuevo = crypto.randomUUID(),
          credencial = Array.from(
            crypto.getRandomValues(new Uint8Array(32)),
            (n) => n.toString(16).padStart(2, "0"),
          ).join("");
        const operacion = {
          id: nuevo,
          codigo,
          clase: espacio.configuracion.clase,
          estado: "Reservada",
          minutos: datos.minutos,
          monto: espacio.configuracion.tarifas[datos.minutos],
          moneda: "HNL",
          venceReservaUtc: new Date(Date.now() + 600000).toISOString(),
          hardwareSimulado: true,
        };
        operaciones.set(nuevo, {
          operacion,
          credencial,
          pin: "654321",
          intentos: 0,
        });
        espacio.estado = "Reservado";
        return clonar({ operacion, credencial });
      }
      const r = operaciones.get(id);
      if (
        !r ||
        r.operacion.codigo !== codigo ||
        r.credencial !== encabezados["X-SYNCHRO-SERVICIO"]
      )
        fallo("Datos de reserva incorrectos.", 404);
      if (accion === "pagar") {
        if (r.pagoId === datos?.idOperacion)
          return clonar({
            operacion: r.operacion,
            pinRetiro: null,
            yaConfirmado: true,
          });
        if (r.operacion.estado !== "Reservada" || !datos?.idOperacion)
          fallo("No se puede confirmar ese pago.");
        r.pagoId = datos.idOperacion;
        r.operacion.estado = "Pagada";
        return clonar({
          operacion: r.operacion,
          pinRetiro: r.pin,
          yaConfirmado: false,
        });
      }
      if (accion === "iniciar") {
        if (
          r.operacion.estado !== "Pagada" ||
          (r.operacion.clase === "CargaLaptop" && !datos?.equipoCerrado)
        )
          fallo("Confirma el pago y guarda la laptop cerrada.");
        r.operacion.estado = "Activa";
        r.operacion.inicioUtc = new Date().toISOString();
        r.operacion.finContratadoUtc = new Date(
          Date.now() + r.operacion.minutos * 60000,
        ).toISOString();
        espacio.estado = "Ocupado";
        return clonar(r.operacion);
      }
      if (accion === "retirar") {
        if (
          !["Activa", "Agotada"].includes(r.operacion.estado) ||
          r.intentos >= 5
        )
          fallo("Retiro no disponible.");
        if (datos?.pin !== r.pin) {
          r.intentos++;
          fallo("PIN incorrecto. Revisa tus datos.");
        }
        r.operacion.estado = "Finalizada";
        espacio.estado = "Disponible";
        return clonar({
          autorizado: true,
          pinBloqueado: false,
          operacion: r.operacion,
        });
      }
      if (accion === "cancelar") {
        if (!["Reservada", "Pagada"].includes(r.operacion.estado))
          fallo("No se puede cancelar una operación activa.");
        r.operacion.estado = "Cancelada";
        espacio.estado = "Disponible";
        return clonar(r.operacion);
      }
    }
    fallo("Esta operación no está incluida en la demostración.", 404);
  };
}
export const solicitarDemo = crearEscenarioDemo();
