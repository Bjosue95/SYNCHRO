// SYNCHRO: diagnostico visual de titulos; no modifica archivos ni realiza pagos.
import { chromium } from "@playwright/test";

const base = (process.env.SYNCHRO_VISUAL_URL || "https://localhost:5173").replace(/\/$/, "");
const resoluciones = [
  { width: 1280, height: 800 },
  { width: 768, height: 1024 }
];
const pantallas = ["menu", "pago", "deposito", "pago-resumen"];
const browser = await chromium.launch();
let errores = 0;

function redondear(n) { return Math.round(n * 10) / 10; }

try {
  for (const viewport of resoluciones) {
    const context = await browser.newContext({
      viewport,
      ignoreHTTPSErrors: true,
      reducedMotion: "reduce"
    });
    const page = await context.newPage();
    const medidas = [];

    for (const nombre of pantallas) {
      try {
        await page.goto(base + "/vista-previa/" + nombre, {
          waitUntil: "domcontentloaded",
          timeout: 15000
        });
        await page.locator(".pantalla-real main h1").first().waitFor({
          state: "visible",
          timeout: 12000
        });
        await page.evaluate(() => document.fonts.ready);
        const layout = await page.evaluate(() => {
          const heading = document.querySelector(".pantalla-real main h1");
          const elementos = [];
          for (let node = heading; node && elementos.length < 8; node = node.parentElement) {
            if (!(node instanceof HTMLElement)) break;
            const rect = node.getBoundingClientRect();
            const css = getComputedStyle(node);
            elementos.push({
              elemento: node.tagName.toLowerCase() +
                (node.classList.length ? "." + [...node.classList].slice(0, 3).join(".") : ""),
              y: Math.round(rect.y * 10) / 10,
              alto: Math.round(rect.height * 10) / 10,
              display: css.display,
              posicion: css.position,
              transformacion: css.transform === "none" ? "-" : css.transform,
              margenSuperior: css.marginTop,
              rellenoSuperior: css.paddingTop,
              filas: css.gridTemplateRows === "none" ? "-" : css.gridTemplateRows
            });
            if (node.classList.contains("pantalla-real")) break;
          }
          return elementos;
        });
        medidas.push({ pantalla: nombre, tituloY: layout[0].y, layout });
      } catch (e) {
        errores++;
        console.error("ERROR " + viewport.width + "x" + viewport.height + " " + nombre + ": " + e.message);
      }
    }

    const menu = medidas.find(item => item.pantalla === "menu");
    console.log("\n=== " + viewport.width + "x" + viewport.height + " | Posicion del titulo ===");
    console.table(medidas.map(item => ({
      pantalla: item.pantalla,
      tituloY: item.tituloY,
      diferenciaMenu: menu ? redondear(item.tituloY - menu.tituloY) : "Sin menu"
    })));

    for (const item of medidas) {
      console.log("\n" + item.pantalla + " | Arbol de contenedores (titulo -> pantalla):");
      console.table(item.layout);
    }
    await context.close();
  }
} finally {
  await browser.close();
}
if (errores) process.exitCode = 1;
