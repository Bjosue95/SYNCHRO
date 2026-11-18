export const MINUTO_DEPOSITO_MS = 60_000;

export function segundosRestantesDeposito(limiteMs, ahoraMs) {
  if (!Number.isFinite(limiteMs) || !Number.isFinite(ahoraMs)) {
    return 0;
  }

  return Math.max(0, Math.ceil((limiteMs - ahoraMs) / 1000));
}

export function extenderLimiteDeposito(limiteMs, ahoraMs) {
  if (!Number.isFinite(ahoraMs)) {
    throw new Error("Hora actual invalida.");
  }

  const anterior = Number.isFinite(limiteMs) ? limiteMs : ahoraMs;

  // Si ya llego a cero, comienza otro minuto completo.
  // Si todavia queda tiempo, suma 60 segundos al restante.
  return Math.max(anterior, ahoraMs) + MINUTO_DEPOSITO_MS;
}

export function formatearTiempoDeposito(segundos) {
  const valor = Number.isFinite(segundos)
    ? Math.max(0, Math.floor(segundos))
    : 0;

  const minutos = String(Math.floor(valor / 60)).padStart(2, "0");
  const restantes = String(valor % 60).padStart(2, "0");

  return `${minutos}:${restantes}`;
}