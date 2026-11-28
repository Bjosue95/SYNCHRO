import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const leer = ruta => readFileSync(join(root, ruta), "utf8");

test("cabecera pública con navegación a las secciones del inicio y controles conservados", () => {
  const layout = leer("src/components/layout/PublicLayout.vue");
  assert.match(layout, /class="nav-publica"/);
  const hashes = [...layout.matchAll(/hash: "(#[a-z]+)", texto:/g)].map(coincidencia => coincidencia[1]);
  assert.deepEqual(hashes, ["#inicio", "#servicios", "#nosotros", "#contacto"]);
  assert.match(layout, /path: '\/', hash: enlace.hash/);
  assert.match(layout, /position:sticky; top:0/);
  assert.match(layout, /:aria-expanded="menuAbierto"/);
  assert.match(layout, /v-if="!esAcceso"/);
  assert.match(layout, /BrandLogo/);
  assert.match(layout, /ThemeToggle/);
});

test("inicio de sesión y registro sin microtítulos duplicados", () => {
  const vista = leer("src/views/AccesoView.vue");
  assert.match(vista, /"Crea tu cuenta" : "Inicia sesión"/);
  assert.doesNotMatch(vista, /TU CUENTA SYNCHRO/);
  assert.doesNotMatch(vista, /class="pestanas"/);
  assert.match(vista, /cambiarModo\(esRegistro \? 'ingreso' : 'registro'\)/);
  assert.match(vista, /@submit\.prevent="enviar"/);
  assert.match(vista, /crearCuenta/);
  assert.match(vista, /iniciarSesion/);
});

test("vista de acceso utiliza alto útil con salida accesible en pantallas reducidas", () => {
  const estilos = leer("src/styles/access.css");
  const main = leer("src/main.js");
  assert.match(main, /import "\.\/styles\/access\.css"/);
  assert.match(estilos, /height: 100dvh/);
  assert.match(estilos, /overflow: hidden/);
  assert.match(estilos, /@media \(max-width: 850px\)/);
  assert.match(estilos, /@media \(max-width: 620px\)/);
  assert.match(estilos, /@media \(max-height: 580px\)/);
  assert.match(estilos, /overflow: visible/);
  assert.match(estilos, /\.es-acceso \.acceso-presentacion \{ display: none; \}/);
});

test("control de tema usa iconos de luna y sol distintos con trazo SYNCHRO", () => {
  const theme = leer("src/components/ui/ThemeToggle.vue");
  assert.match(theme, /alternarTema/);
  assert.match(theme, /nombre="|:nombre="/);
  assert.match(theme, /nombre="sol"/);
  assert.match(theme, /nombre="luna"/);
  assert.match(theme, /:data-modo="tema"/);
  assert.match(theme, /:class="\{ activa: tema === 'claro' \}"/);
  assert.match(theme, /:class="\{ activa: tema === 'oscuro' \}"/);
  assert.match(theme, /@click="alternarTema"/);
  assert.match(theme, /min-height: 44px/);
  assert.match(theme, /prefers-reduced-motion: reduce/);
  assert.doesNotMatch(theme, /etiqueta-tema/);
  for (const modo of ["claro", "oscuro"]) {
    const luna = leer(`public/iconos/${modo}/luna.svg`);
    const sol = leer(`public/iconos/${modo}/sol.svg`);
    assert.notEqual(luna, sol);
    for (const svg of [luna, sol]) {
      assert.match(svg, /viewBox="0 0 128 128"/);
      assert.match(svg, /stroke-width="6"/);
    }
  }
});
