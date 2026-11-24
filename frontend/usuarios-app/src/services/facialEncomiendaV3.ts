// Adaptación web del protocolo V3 validado. La tablet se conserva en modo de solo lectura.
export const MODELO_FACIAL_V3 = "SYNCHRO-HUMANJS-EMBEDDING-1024-v3";
export type CapturaV3 = {
  imagenPng: string;
  descriptor: number[];
  modelo: string;
};
let cargaMotor: Promise<any> | null = null;
export async function cargarMotorV3() {
  if (!cargaMotor)
    cargaMotor = (async () => {
      const win = window as Window & { Human?: any };
      if (!win.Human)
        await new Promise<void>((resolve, reject) => {
          const script = document.createElement("script");
          script.src =
            "https://cdn.jsdelivr.net/npm/@vladmandic/human@3.3.6/dist/human.js";
          script.onload = () => resolve();
          script.onerror = () => {
            script.remove();
            reject(
              new Error(
                "No se pudo cargar el motor facial. Comprueba tu conexión.",
              ),
            );
          };
          document.head.appendChild(script);
        });
      const Human = win.Human?.Human ?? win.Human;
      const motor = new Human({
        backend: "webgl",
        modelBasePath:
          "https://cdn.jsdelivr.net/npm/@vladmandic/human@3.3.6/models/",
        cacheSensitivity: 0,
        face: {
          enabled: true,
          detector: { enabled: true, maxDetected: 2 },
          mesh: { enabled: true },
          description: { enabled: true },
          iris: { enabled: false },
          emotion: { enabled: false },
        },
        body: { enabled: false },
        hand: { enabled: false },
        object: { enabled: false },
        gesture: { enabled: false },
      });
      await motor.load();
      await motor.warmup();
      return motor;
    })().catch((e) => {
      cargaMotor = null;
      throw e;
    });
  return cargaMotor;
}
export function descriptorValido(entrada: unknown): entrada is number[] {
  return (
    Array.isArray(entrada) &&
    entrada.length === 1024 &&
    entrada.every((v) => Number.isFinite(v) && Math.abs(v) <= 1000) &&
    entrada.reduce((s, v) => s + v * v, 0) > 1e-8
  );
}
export function revisarCapturaV3(
  datos: any,
  video: HTMLVideoElement,
): { error: string; puntaje: number } {
  if (datos.face?.length !== 1)
    return {
      error: "Debe aparecer una sola persona frente a la cámara.",
      puntaje: 0,
    };
  const b = datos.face[0].box,
    w = datos.width || video.videoWidth,
    h = datos.height || video.videoHeight;
  if (!Array.isArray(b) || !b.slice(0, 4).every(Number.isFinite) || !w || !h)
    return { error: "Ajusta tu posición frente a la cámara.", puntaje: 0 };
  const cx = (b[0] + b[2] / 2) / w,
    cy = (b[1] + b[3] / 2) / h;
  if (cx < 0.31 || cx > 0.69 || cy < 0.24 || cy > 0.76)
    return { error: "Centra tu rostro.", puntaje: 0 };
  if (b[2] < w * 0.2 || b[3] < h * 0.24)
    return { error: "Acércate un poco.", puntaje: 0 };
  if (b[2] > w * 0.75 || b[3] > h * 0.85)
    return { error: "Aléjate un poco.", puntaje: 0 };
  const canvas = document.createElement("canvas");
  canvas.width = canvas.height = 64;
  const ctx = canvas.getContext("2d", { willReadFrequently: true });
  if (!ctx) throw new Error("No se pudo revisar la iluminación.");
  ctx.drawImage(
    video,
    (b[0] * video.videoWidth) / w,
    (b[1] * video.videoHeight) / h,
    (b[2] * video.videoWidth) / w,
    (b[3] * video.videoHeight) / h,
    0,
    0,
    64,
    64,
  );
  const pix = ctx.getImageData(0, 0, 64, 64).data,
    grises = new Float32Array(4096);
  let suma = 0,
    cuadrados = 0,
    detalle = 0;
  for (let i = 0; i < 4096; i++) {
    const j = i * 4,
      luz = 0.2126 * pix[j] + 0.7152 * pix[j + 1] + 0.0722 * pix[j + 2];
    grises[i] = luz;
    suma += luz;
    cuadrados += luz * luz;
    if (i % 64 > 0) detalle += Math.abs(luz - grises[i - 1]);
  }
  const promedio = suma / 4096,
    contraste = Math.sqrt(Math.max(0, cuadrados / 4096 - promedio * promedio));
  grises.fill(0);
  if (promedio < 45 || promedio > 215)
    return {
      error: "Ajusta la iluminación; evita sombras y luz directa.",
      puntaje: 0,
    };
  if (contraste < 16)
    return {
      error: "La imagen tiene poco contraste. Ajusta la iluminación.",
      puntaje: 0,
    };
  return {
    error: "",
    puntaje: contraste + detalle / 4096 - Math.abs(promedio - 130) * 0.08,
  };
}
export function pngV3(datos: any, video: HTMLVideoElement): string {
  const b = datos.face[0].box,
    vw = video.videoWidth,
    vh = video.videoHeight,
    fx = vw / (datos.width || vw),
    fy = vh / (datos.height || vh);
  const x = b[0] * fx,
    y = b[1] * fy,
    w = b[2] * fx,
    h = b[3] * fy,
    lado = Math.min(Math.max(w, h) * 1.45, Math.min(vw, vh));
  const sx = Math.max(0, Math.min(vw - lado, x + w / 2 - lado / 2)),
    sy = Math.max(0, Math.min(vh - lado, y + h / 2 - lado / 2));
  const canvas = document.createElement("canvas");
  canvas.width = canvas.height = 256;
  const ctx = canvas.getContext("2d");
  if (!ctx) throw new Error("No se pudo preparar la captura.");
  ctx.drawImage(video, sx, sy, lado, lado, 0, 0, 256, 256);
  return canvas.toDataURL("image/png").split(",")[1];
}
