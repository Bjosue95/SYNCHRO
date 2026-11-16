export function mensajePin(datos) {
  return `SYNCHRO — El punto donde todo se conecta.\nTu reserva ${datos.usoId} está confirmada.\nCasillero: ${datos.codigoCasillero}.\nPIN de retiro: ${datos.pinRetiro}.\nConserva este mensaje y no compartas tu PIN. Presenta la referencia y el PIN al retirar tus pertenencias.`;
}
