export function chatPinEncomienda(pin, detalle) {
  if (!pin || !detalle || !["Deposito", "Retiro"].includes(pin.finalidad)) return "";
  const participante = detalle[pin.finalidad === "Deposito" || detalle.reserva?.servicio === "Personal" ? "remitente" : "destinatario"];
  if (!/^\+[1-9][0-9]{7,14}$/.test(pin.telefono) || pin.telefono !== participante?.telefono || participante.consentimiento !== "Confirmado") return "";
  if (pin.finalidad === "Retiro" && detalle.estado !== "Depositada") return "";
  if (pin.finalidad === "Deposito" && !["Preparacion", "ListaParaDeposito"].includes(detalle.estado)) return "";
  // Ningún PIN ni credencial se incorpora al enlace, ni siquiera como texto.
  return `https://wa.me/${pin.telefono.slice(1)}`;
}

export function mensajePinEncomienda(pin, detalle, vencimiento) {
  if (!chatPinEncomienda(pin, detalle) || !/^[0-9]{6}$/.test(pin.pin)) return "";
  return `SYNCHRO · Reserva #${detalle.id}. PIN exclusivo de ${pin.finalidad.toLowerCase()}: ${pin.pin}. Vence: ${vencimiento}. No lo compartas.`;
}
