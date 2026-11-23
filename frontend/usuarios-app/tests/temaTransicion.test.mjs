import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const leer = ruta => readFileSync(join(root, ruta), "utf8");

test("fundido de tema igual al de tablet en la página completa", () => {
  const codigo = leer("src/composables/useTheme.js");
  const css = leer("src/styles/theme-transition.css");
  const main = leer("src/main.js");
  assert.match(codigo, /document\.startViewTransition\(/);
  assert.match(codigo, /await nextTick\(\)/);
  assert.match(codigo, /transicion\.finished\.then\(limpiar, limpiar\)/);
  assert.match(codigo, /synchro-tema-instantaneo/);
  assert.match(css, /::view-transition-old\(root\)/);
  assert.match(css, /::view-transition-new\(root\)/);
  assert.match(css, /animation-duration: 220ms/);
  assert.match(main, /theme-transition\.css/);
});

test("evita cambios apilados y respeta movimiento reducido", () => {
  const codigo = leer("src/composables/useTheme.js");
  const css = leer("src/styles/theme-transition.css");
  assert.match(codigo, /transicionActiva/);
  assert.match(codigo, /ultimoCambio < 250/);
  assert.match(codigo, /prefers-reduced-motion: reduce/);
  assert.match(css, /transition: none !important/);
  assert.match(css, /animation-duration: 0\.01ms !important/);
});

test("la transición no altera contraseñas ni sesión", () => {
  const codigo = leer("src/composables/useTheme.js");
  assert.match(codigo, /synchro-usuarios-tema/);
  assert.doesNotMatch(codigo, /contrasena|password|tokenGestion|pin/i);
});
