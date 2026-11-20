const MODELO = "SYNCHRO-HUMANJS-EMBEDDING-1024-v3";
export const VERSION_CONSENTIMIENTO_V3 = "SYNCHRO-FACIAL-V3-2026-11";

export function construirInicioFacialV3(
  imagenDataUrl,
  descriptor,
  consentimiento
) {
  if (consentimiento !== true) {
    throw new Error("Falta consentimiento biométrico V3.");
  }

  if (typeof imagenDataUrl !== "string" ||
      !imagenDataUrl.startsWith("data:image/png;base64,")) {
    throw new Error("Captura PNG V3 inválida.");
  }

  if (!Array.isArray(descriptor) ||
      descriptor.length !== 1024 ||
      descriptor.some(v => !Number.isFinite(v) || Math.abs(v) > 1000)) {
    throw new Error("Descriptor facial V3 inválido.");
  }

  const norma = descriptor.reduce(
    (suma, valor) => suma + valor * valor, 0
  );

  if (!Number.isFinite(norma) || norma < 1e-8) {
    throw new Error("Descriptor facial V3 no utilizable.");
  }

  return {
    modelo: MODELO,
    body: JSON.stringify({
      imagenPng: imagenDataUrl.substring("data:image/png;base64,".length),
      descriptor,
      versionConsentimiento: VERSION_CONSENTIMIENTO_V3
    })
  };
}