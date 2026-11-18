// SYNCHRO - auditoria visual local. No realiza operaciones de pago ni API.
import { chromium } from "@playwright/test";
import { existsSync, mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const base = (process.env.SYNCHRO_VISUAL_URL || "https://localhost:5173")
  .replace(/\/$/, "");
const salida = join(process.cwd(), "test-results", "auditoria-visual");
mkdirSync(salida, { recursive: true });

const pantallas = [
  "menu", "guardar", "tamano", "casillero", "rostro-guardar",
  "whatsapp", "deposito", "retirar", "rostro-retirar",
  "pin", "pago", "pago-resumen"
];
const tamanos = [
  { width: 1280, height: 800 },
  { width: 768, height: 1024 }
];

const navegador = await chromium.launch({
  executablePath: process.env.SYNCHRO_CHROMIUM ||
    (existsSync("/usr/bin/chromium") ? "/usr/bin/chromium" : undefined)
});

const informe = {
  version: 1,
  origen: base,
  fecha: new Date().toISOString(),
  intentos: 0,
  capturas: [],
  comparaciones: [],
  errores: []
};

async function obtenerMedidas(pagina) {
  return pagina.evaluate(() => {
    const titulo = document.querySelector(".pantalla-real main h1");
    const pantalla = document.querySelector(".pantalla-real main");
    if (!titulo || !pantalla) return null;
    const r = titulo.getBoundingClientRect();
    const c = getComputedStyle(titulo);
    const botones = [...pantalla.querySelectorAll("button")];
    const continuar = botones.find(b => b.textContent.trim().includes("Continuar"));
    const e = continuar ? getComputedStyle(continuar) : null;
    const br = continuar?.getBoundingClientRect();
    return {
      titulo: titulo.textContent.trim().replace(/\s+/g, " "),
      y: Math.round(r.y * 10) / 10,
      alto: Math.round(r.height * 10) / 10,
      fuente: c.fontSize,
      anchoPagina: document.documentElement.scrollWidth,
      anchoViewport: window.innerWidth,
      botonContinuar: br ? {
        x: Math.round(br.x * 10) / 10,
        y: Math.round(br.y * 10) / 10,
        ancho: Math.round(br.width * 10) / 10,
        alto: Math.round(br.height * 10) / 10,
        fondo: e.backgroundColor,
        borde: e.borderTopColor,
        deshabilitado: continuar.disabled
      } : null
    };
  });
}

console.log(`Auditoría iniciada. Pantallas: ${pantallas.length}; resoluciones: ${tamanos.length}.`);
console.log(`Origen: ${base}`);
console.log(`Intentos previstos: ${pantallas.length * tamanos.length}.`);

try {
  for (const viewport of tamanos) {
    const contexto = await navegador.newContext({
      viewport,
      ignoreHTTPSErrors: true,
      reducedMotion: "reduce"
    });
    const pagina = await contexto.newPage();

    for (const id of pantallas) {
      const clave = `${viewport.width}x${viewport.height}-${id}`;
      informe.intentos++;
      console.log(`[${informe.intentos}/${pantallas.length * tamanos.length}] ${clave}`);
      try {
        await pagina.goto(`${base}/vista-previa/${id}`, {
          waitUntil: "domcontentloaded",
          timeout: 15000
        });
        await pagina.locator(".pantalla-real main h1").first().waitFor({
          state: "visible",
          timeout: 12000
        });
        await pagina.evaluate(() => document.fonts.ready);
        const medidas = await obtenerMedidas(pagina);
        if (!medidas) throw new Error("No se pudo medir el titulo.");

        await pagina.screenshot({
          path: join(salida, `${clave}-claro.png`),
          animations: "disabled"
        });

        informe.capturas.push({ pantalla: id, viewport, tema: "claro", ...medidas });

        const activarOscuro = pagina.getByRole("button", {
          name: "Activar modo oscuro"
        });
        if (await activarOscuro.count()) {
          await activarOscuro.click();
          await pagina.screenshot({
            path: join(salida, `${clave}-oscuro.png`),
            animations: "disabled"
          });
          informe.capturas.push({
            pantalla: id, viewport, tema: "oscuro",
            ...await obtenerMedidas(pagina)
          });
          const volverClaro = pagina.getByRole("button", {
            name: "Activar modo claro"
          });
          if (await volverClaro.count()) await volverClaro.click();
        }
      } catch (error) {
        informe.errores.push({ pantalla: id, viewport, error: String(error) });
        console.error(`No se pudo capturar ${clave}: ${error.message}`);
      }
    }
    await contexto.close();
  }
} finally {
  await navegador.close();
}

for (const viewport of tamanos) {
  const casos = informe.capturas.filter(x =>
    x.tema === "claro" &&
    x.viewport.width === viewport.width &&
    x.viewport.height === viewport.height
  );
  const menu = casos.find(x => x.pantalla === "menu");
  if (!menu) continue;
  for (const c of casos) {
    informe.comparaciones.push({
      tamano: `${viewport.width}x${viewport.height}`,
      pantalla: c.pantalla,
      titulo: c.titulo,
      y: c.y,
      diferenciaRespectoMenu: Math.round((c.y - menu.y) * 10) / 10
    });
  }
}

writeFileSync(
  join(salida, "informe.json"),
  JSON.stringify(informe, null, 2),
  "utf8"
);

console.log(`\nIntentos: ${informe.intentos}; mediciones: ${informe.capturas.length}; comparaciones: ${informe.comparaciones.length}; errores: ${informe.errores.length}.`);
console.log(`Capturas e informe: ${salida}`);

if (informe.intentos !== pantallas.length * tamanos.length) {
  console.error("ERROR: no se recorrieron todas las pantallas programadas.");
  process.exitCode = 1;
}
if (informe.capturas.length === 0) {
  console.error("ERROR: no hay mediciones. Comprueba que npm.cmd run dev este activo en https://localhost:5173");
  process.exitCode = 1;
}
if (informe.comparaciones.length === 0) {
  console.error("ERROR: no se pudo generar la comparacion. Comprueba las capturas del menu en informe.json.");
  process.exitCode = 1;
} else {
  console.log("\nDiferencias de altura de titulo respecto al menu (pixeles):");
  console.table(informe.comparaciones);
}
if (informe.errores.length > 0) {
  console.error("Errores de captura:", informe.errores.slice(0, 5));
  process.exitCode = 1;
}
console.log("Las diferencias son mediciones, NO ajustes automaticos de CSS.");
