/** Formatos de presentación. No alteran estados ni fechas del servidor. */
const estados = Object.freeze({
  Preparacion: "En preparación",
  ListaParaDeposito: "Lista para depósito",
  Depositada: "Depositada",
  Retirada: "Entregada",
  ConfirmadoManual: "Confirmado manualmente",
  Pequeno: "Pequeño",
  PendienteConsentimiento: "Pendiente de consentimiento",
  Pendiente: "Pendiente",
  Confirmado: "Confirmado",
  Confirmada: "Confirmada",
  Vencida: "Vencida",
  Cancelada: "Cancelada",
  Rechazado: "Rechazado",
  Revocado: "Revocado"
});

export function nombreEstadoReserva(estado) {
  if (typeof estado !== "string") return "Sin información";
  return estados[estado] || estado
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .slice(0, 70);
}

export function fechaHonduras(valor) {
  if (!valor || Number.isNaN(Date.parse(valor))) return "No disponible";
  return new Intl.DateTimeFormat("es-HN", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "America/Tegucigalpa"
  }).format(new Date(valor));
}
