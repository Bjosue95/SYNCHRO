import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const leer = path => readFileSync(join(root, path), "utf8");
const importantes = [
  "facial", "pin", "alarma", "sensores", "braille", "accesibilidad",
  "cifrado", "integridad", "solar", "bateria-respaldo", "eficiencia",
  "energia", "correo", "telefono", "instagram", "tiktok", "whatsapp",
  "reloj", "ubicacion", "enviar", "copiar", "mapa"
];

test("iconografia web original incluye 64 figuras en los dos temas", () => {
  const identificadores = [];
  for (const tema of ["claro", "oscuro"]) {
    const svg = leer("public/iconos/" + tema + "/web-iconos.svg");
    const nombres = [...svg.matchAll(/<symbol id="([^"]+)"/g)].map(m => m[1]);
    assert.equal(nombres.length, 64);
    assert.equal(new Set(nombres).size, nombres.length);
    for (const nombre of importantes) assert.ok(nombres.includes(nombre), nombre);
    assert.match(svg, /viewBox="0 0 128 128"/);
    assert.match(svg, /stroke-width="1\.65"/);
    identificadores.push(nombres);
  }
  assert.deepEqual(identificadores[0], identificadores[1]);
});

test("AppIcon utiliza la familia propia en toda la web", () => {
  const codigo = leer("src/components/ui/AppIcon.vue");
  assert.match(codigo, /web-iconos\.svg/);
  assert.match(codigo, /<use :href="ruta"/);
  assert.match(codigo, /aria-hidden/);
  assert.match(codigo, /etiqueta/);
});

test("soluciones reúnen protección, sensórica, accesibilidad y energía", () => {
  const section = leer("src/components/landing/SmartSolutions.vue");
  const inicio = leer("src/views/InicioView.vue");
  for (const palabra of [
    "Verificación facial", "Alarmas y alertas", "Sensores inteligentes",
    "Identificación en braille", "Protección de información",
    "Paneles solares", "Respaldo de energía", "Eficiencia energética"
  ]) assert.ok(section.includes(palabra), palabra);
  assert.match(inicio, /<SmartSolutions\s*\/>/);
  assert.match(section, /prefers-reduced-motion/);
});

test("la navegación y el contacto utilizan iconos en lugar de emojis o dibujos ajenos", () => {
  const footer = leer("src/components/landing/SiteFooter.vue");
  const contacto = leer("src/components/landing/ContactSection.vue");
  const cotizacion = leer("src/components/landing/QuoteForm.vue");
  for (const nombre of ["instagram", "tiktok", "correo", "telefono", "reloj", "ubicacion"]) {
    assert.ok(footer.includes('nombre="' + nombre + '"'), nombre);
  }
  assert.match(contacto, /nombre="enlace-externo"/);
  assert.match(cotizacion, /nombre="enviar"/);
  assert.match(cotizacion, /nombre="copiar"/);
});
