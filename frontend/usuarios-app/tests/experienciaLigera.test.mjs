import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync, statSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const base = join(dirname(fileURLToPath(import.meta.url)), "..");
const leer = ruta => readFileSync(join(base, ruta), "utf8");

test("el frontend conserva dependencias ligeras y rutas separadas", () => {
  const pkg = JSON.parse(leer("package.json"));
  assert.deepEqual(Object.keys(pkg.dependencies).sort(), ["leaflet", "vue", "vue-router"]);
  assert.ok(pkg.devDependencies.vite);
  assert.ok(pkg.devDependencies["@vitejs/plugin-vue"]);
  const rutas = leer("src/router/index.js");
  assert.match(rutas, /import\("\.\.\/views\/InicioView\.vue"\)/);
  assert.match(rutas, /import\("\.\.\/views\/PanelView\.vue"\)/);
});

test("la portada integra una escena de producto con pasos interactivos", () => {
  const inicio = leer("src/views/InicioView.vue");
  const escena = leer("src/components/ui/LockerJourney.vue");
  assert.match(inicio, /<LockerJourney\s*\/>/);
  for (const nombre of ["Almacenamiento personal", "Encomiendas",
    "Temperatura controlada", "Carga de dispositivos"]) {
    assert.ok(inicio.includes(nombre), nombre);
  }
  for (const paso of ["Solicitud", "Consentimiento", "Entrega y retiro"]) {
    assert.ok(escena.includes(paso), paso);
  }
  assert.match(escena, /puertaAbierta/);
  assert.match(escena, /rotateY\(-73deg\)/);
  assert.match(escena, /@click="elegir\(i\)"/);
  assert.match(escena, /controlarTeclado/);
  assert.match(escena, /prefers-reduced-motion: reduce/);
});

test("la interacción editorial no inventa operaciones de la base de datos", () => {
  const escena = leer("src/components/ui/LockerJourney.vue");
  const inicio = leer("src/views/InicioView.vue");
  assert.doesNotMatch(escena, /fetch\(|apiClientes|localStorage|sessionStorage/);
  assert.match(inicio, /pre-reserva no asigna un casillero/);
  assert.match(inicio, /Reserva web aún no habilitada/);
  assert.match(inicio, /Uso presencial/);
});

test("las animaciones ligeras usan tecnologías nativas y no loops innecesarios", () => {
  const inicio = leer("src/views/InicioView.vue");
  assert.match(inicio, /IntersectionObserver/);
  assert.match(inicio, /observador\?\.disconnect/);
  assert.match(inicio, /prefers-reduced-motion: reduce/);
  assert.doesNotMatch(inicio, /gsap|framer-motion|lottie|three/);
});

test("logos originales son PNG, sin envolturas Base64", () => {
  const logos = ["marca-completa", "nombre-azul", "nombre-blanco",
    "isotipo-claro", "isotipo-oscuro"];
  for (const logo of logos) {
    const archivo = join(base, "public/branding", logo + ".png");
    const png = readFileSync(archivo);
    assert.deepEqual([...png.subarray(0, 8)], [137, 80, 78, 71, 13, 10, 26, 10]);
    assert.ok(statSync(archivo).size < 120000);
  }
});
