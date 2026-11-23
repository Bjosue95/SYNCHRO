import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const leer = ruta => readFileSync(join(root, ruta), "utf8");
const iconos = [
  "inicio", "encomienda", "reservas", "casillero", "temperatura",
  "carga", "usuario", "seguridad", "buscar", "whatsapp", "sol", "luna",
  "salir", "menu", "flecha", "calendario", "reloj", "ubicacion",
  "check", "volver"
];

test("iconografia completa en claro y oscuro con estilo de tablet", () => {
  for (const tema of ["claro", "oscuro"]) {
    for (const nombre of iconos) {
      const svg = leer(`public/iconos/${tema}/${nombre}.svg`);
      assert.match(svg, /<svg\b/);
      assert.match(svg, /viewBox="0 0 128 128"/);
      assert.match(svg, /#[0-9A-Fa-f]{6}/);
    }
  }
});

test("logo original de SYNCHRO integrado en el frontend", () => {
  for (const nombre of [
    "marca-completa", "nombre-azul", "nombre-blanco",
    "isotipo-claro", "isotipo-oscuro"
  ]) {
    const logo = leer(`public/branding/${nombre}.svg`);
    assert.match(logo, /<svg\b/);
    assert.match(logo, /image\/png;base64/);
  }
  const logo = leer("src/components/ui/BrandLogo.vue");
  assert.match(logo, /\/branding\/nombre-blanco.svg/);
  assert.match(logo, /\/branding\/nombre-azul.svg/);
});

test("tema accesible y variables semanticas aplicadas", () => {
  const temas = leer("src/styles/tokens.css");
  const toggle = leer("src/components/ui/ThemeToggle.vue");
  const css = leer("src/styles/base.css");
  assert.match(temas, /data-theme="oscuro"/);
  assert.match(temas, /--bg:/);
  assert.match(temas, /--surface:/);
  assert.match(toggle, /alternarTema/);
  assert.match(toggle, /aria-label/);
  assert.match(css, /prefers-reduced-motion: reduce/);
  assert.match(css, /@media \(max-width: 620px\)/);
});

test("las paginas publicas y privadas usan layouts distintos", () => {
  const app = leer("src/App.vue");
  const shell = leer("src/components/layout/AppShell.vue");
  assert.match(app, /PublicLayout/);
  assert.match(app, /AppShell/);
  assert.match(shell, /sidebar-navegacion/);
  assert.match(shell, /ThemeToggle/);
  assert.match(shell, /cerrarSesion/);
});

test("no se inventan metricas ni historial de reservas en el panel", () => {
  const panel = leer("src/views/PanelView.vue");
  assert.match(panel, /crear y consultar|gestionar tus solicitudes|crear y consultar/i);
  assert.doesNotMatch(panel, /reservas activas\s*\d|pr[oó]ximos vencimientos\s*\d/i);
  assert.match(panel, /AppIcon nombre="reservas"/);
  assert.match(panel, /AppIcon nombre="buscar"/);
});
