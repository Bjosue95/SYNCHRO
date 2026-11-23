import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const leer = ruta => readFileSync(new URL(ruta, import.meta.url), "utf8");

test("el portal de usuarios es independiente de tablet y admin", () => {
  const pkg = JSON.parse(leer("../package.json"));
  assert.equal(pkg.name, "synchro-usuarios-app");
  assert.match(leer("../vite.config.js"), /port: 5175/);
  assert.match(leer("../vite.config.js"), /\/api/);
});

test("el cliente utiliza rutas existentes del backend", () => {
  const codigo = leer("../src/services/clientesApi.js");
  for (const ruta of [
    "/api/web/clientes/acceso/csrf-token",
    "/api/web/clientes/acceso/registro",
    "/api/web/clientes/acceso/login",
    "/api/web/clientes/mi-sesion",
    "/api/web/clientes/logout",
    "/api/web/encomiendas/pre-reservas"
  ]) assert.ok(codigo.includes(ruta), ruta);
  assert.match(codigo, /X-SYNCHRO-RESERVA/);
  assert.match(codigo, /X-CSRF-TOKEN/);
  assert.match(codigo, /credentials: "same-origin"/);
  assert.match(codigo, /cache: "no-store"/);
});

test("no se colocan claves privadas en la ruta de consulta", () => {
  const codigo = leer("../src/services/clientesApi.js");
  assert.match(codigo, /cabeceras: \{ "X-SYNCHRO-RESERVA": tokenGestion\.trim\(\) \}/);
  assert.doesNotMatch(codigo, /localStorage|sessionStorage/);
  assert.doesNotMatch(leer("../src/views/PreReservaView.vue"), /localStorage|sessionStorage/);
});

test("la navegación privada necesita sesión del servidor", () => {
  const codigo = leer("../src/router/index.js");
  assert.match(codigo, /meta: \{ privada: true \}/);
  assert.match(codigo, /await comprobarSesion\(\)/);
  assert.match(codigo, /name: "acceso"/);
});

test("no se ofrecen reservas personales ni de carga sin endpoints", () => {
  const codigo = leer("../src/views/InicioView.vue");
  assert.ok(codigo.includes("Almacenamiento personal"));
  assert.ok(codigo.includes("Carga de dispositivos"));
  assert.ok(codigo.includes("Encomiendas"));
  assert.ok(codigo.includes("Temperatura controlada"));
  const rutas = leer("../src/router/index.js");
  assert.doesNotMatch(rutas, /reservar\/personal|reservar\/carga/);
});
