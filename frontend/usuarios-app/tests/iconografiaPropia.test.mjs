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
  "reloj", "ubicacion", "enviar", "copiar", "mapa", "ojo", "ojo-oculto"
];

test("iconografia web original incluye 66 figuras en los dos temas, incluidos los estados de contraseña", () => {
  const identificadores = [];
  for (const tema of ["claro", "oscuro"]) {
    const svg = leer("public/iconos/" + tema + "/web-iconos.svg");
    const nombres = [...svg.matchAll(/<symbol id="([^"]+)"/g)].map(m => m[1]);
    assert.equal(nombres.length, 66);
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
  assert.match(cotizacion, /<AppIcon[^>]*nombre="enviar"/);
  assert.match(cotizacion, /@submit.prevent="enviarSolicitud"/);
  assert.doesNotMatch(cotizacion, /nombre="copiar"|copiarSolicitud/);
});


test("la estación multifuncional conserva módulos, marca e interacción", () => {
  const escena = leer("src/components/ui/LockerJourney.vue");
  const mini = leer("src/components/ui/MiniLockerDemo.vue");

  // El logo oficial y la pantalla derecha se mantienen visibles.
  assert.match(escena, /marca-completa\.png/);
  assert.match(escena, /nombre-blanco\.png/);
  assert.match(escena, /isotipo-oscuro\.png/);

  // Tres columnas juntas, seguidas por la tablet, con frío, calor y maletas.
  for (const codigo of ["M01", "M02", "L01", "S01", "S02", "M04", "M05", "M03", "M06", "L02"]) {
    assert.ok(escena.includes('codigo: "' + codigo + '"'), codigo);
  }
  assert.match(escena, /const casilleros = Object\.freeze\(\[/);
  assert.match(escena, /tipo: "frio"/);
  assert.match(escena, /tipo: "calor"/);
  assert.match(escena, /class="syn-v4-tablet" transform="translate\(131 0\)"/);
  assert.match(escena, /x="525"/); // pantalla/módulo de carga derecha

  // Carga: 2 columnas y 4 filas; los primeros 2 son para laptop.
  assert.match(escena, /length: 8/);
  assert.match(escena, /i % 2/);
  assert.match(escena, /i < 2 \? "laptop" : "celular"/);
  assert.match(escena, /cambiarPuerta\(c\.codigo\)/);
  assert.match(escena, /@keydown\.enter\.prevent/);
  assert.match(escena, /prefers-reduced-motion: reduce/);

  // Las tarjetas reutilizan los modelos aprobados y pueden abrirse.
  assert.match(mini, /temperatura: \{ titulo: "Frío y calor", columnas: 4, filas: 3/);
  assert.match(mini, /maletas: \{ titulo: "Casilleros para maletas", columnas: 5, filas: 2/);
  assert.match(mini, /carga: \{ titulo: "Carga de dispositivos", columnas: 2, filas: 4/);
  assert.match(mini, /fila === 0/);
  assert.match(mini, /@click="alternar\(c\.codigo\)"/);
});

test("los interiores de la estación usan objetos SYNCHRO dibujados en SVG y no emojis", () => {
  const escena = leer("src/components/ui/LockerJourney.vue");
  const mini = leer("src/components/ui/MiniLockerDemo.vue");
  const objetos = leer("src/components/ui/ObjetoEstacion.vue");
  assert.match(escena, /import ObjetoEstacion/);
  assert.match(mini, /import ObjetoEstacion/);
  assert.match(mini, /<ObjetoEstacion :tipo="c\.contenido"/);
  assert.doesNotMatch(escena + mini + objetos, /\p{Extended_Pictographic}/u);
  for (const objeto of ["caja", "mochila", "maleta", "frio", "caliente", "laptop", "celular"]) {
    assert.ok(objetos.includes("tipo === '" + objeto + "'"), objeto);
    assert.ok(escena.includes('"' + objeto + '"'), objeto);
  }
  assert.match(objetos, /stroke="#01305C"/);
  assert.match(objetos, /stroke-width="1\.5"/);
  assert.match(escena, /clipPathUnits="userSpaceOnUse"/);
  assert.doesNotMatch(escena, /rotateY|perspective\(/);
});

test("energía solar cuenta con un recorrido visual y movimiento reducido", () => {
  const seccion = leer("src/components/landing/SmartSolutions.vue");
  assert.match(seccion, /Esquema de alimentación energética/);
  assert.match(seccion, /Captación solar/);
  assert.match(seccion, /Respaldo/);
  assert.match(seccion, /Estación SYNCHRO/);
  assert.match(seccion, /prefers-reduced-motion:reduce/);
});
