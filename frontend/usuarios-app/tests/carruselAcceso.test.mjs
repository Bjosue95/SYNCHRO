import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const raiz = join(dirname(fileURLToPath(import.meta.url)), "..");
const leer = ruta => readFileSync(join(raiz, ruta), "utf8");

test("el acceso usa un componente de carrusel independiente del formulario", () => {
  const acceso = leer("src/views/AccesoView.vue");
  assert.match(acceso, /import AccessHighlights from/);
  assert.match(acceso, /<AccessHighlights\s*\/>/);
  assert.match(acceso, /@submit\.prevent="enviar"/);
  assert.match(acceso, /crearCuenta/);
  assert.match(acceso, /iniciarSesion/);
});

test("el carrusel contiene los tres contenidos acordados", () => {
  const codigo = leer("src/components/ui/AccessHighlights.vue");
  for (const palabra of [
    "Todo conectado.", "Tus solicitudes.", "Comunicación.",
    "seguridad", "reservas", "whatsapp"
  ]) assert.ok(codigo.includes(palabra), palabra);
  assert.match(codigo, /const DURACION_MS = 6000/);
  assert.match(codigo, /:key="indice"/);
  assert.match(codigo, /<Transition name="beneficio"/);
});

test("el carrusel avanza solo, ofrece navegación manual y no muestra botón de pausa", () => {
  const codigo = leer("src/components/ui/AccessHighlights.vue");
  assert.match(codigo, /@click="mostrar\(numero\)"/);
  assert.match(codigo, /setInterval\(avanzarTiempo, 100\)/);
  assert.match(codigo, /detenidoPorUsuario/);
  assert.doesNotMatch(codigo, /class="acceso-pausa"/);
  assert.doesNotMatch(codigo, /alternarReproduccion/);
  assert.match(codigo, /:aria-current=/);
  assert.match(codigo, /@mouseenter=/);
  assert.match(codigo, /@focusin=/);
  assert.match(codigo, /prefers-reduced-motion: reduce/);
  assert.match(codigo, /visibilitychange/);
  assert.match(codigo, /clearInterval/);
  assert.match(codigo, /aria-roledescription="carrusel"/);
  assert.match(codigo, /aria-live=/);
});

test("el carrusel informativo no ejecuta operaciones de cuenta", () => {
  const codigo = leer("src/components/ui/AccessHighlights.vue");
  assert.doesNotMatch(codigo, /fetch\(|apiClientes|tokenGestion|localStorage|sessionStorage/);
});
