// Flujo visual exclusivo de la vista previa local. No procesa pagos ni retiros.
export const METODOS_PAGO_DEMO = Object.freeze(["Efectivo", "Tarjeta", "NFC"]);

export function normalizarMetodoPagoDemo(valor) {
  return typeof valor === "string" && METODOS_PAGO_DEMO.includes(valor)
    ? valor
    : null;
}

export function nombreMetodoPagoDemo(metodo) {
  return metodo === "NFC" ? "NFC del celular" :
    normalizarMetodoPagoDemo(metodo) || "No disponible";
}

export function etiquetaLecturaDemo(metodo) {
  return {
    NFC: "Simular lectura NFC",
    Tarjeta: "Simular lectura de tarjeta",
    Efectivo: "Simular depósito en efectivo"
  }[metodo] || "Simular pago";
}

// Única secuencia de la demostración. Ninguna transición altera backend/SQL.
export function siguienteFasePagoDemo(fase) {
  const pasos = {
    resumen: "lectura",
    lectura: "espera",
    espera: "confirmado",
    confirmado: "completado"
  };
  return pasos[fase] || fase;
}
