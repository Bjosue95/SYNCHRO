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

test("logo original de SYNCHRO integrado en el frontend sin Base64", () => {
  for (const nombre of [
    "marca-completa", "nombre-azul", "nombre-blanco",
    "isotipo-claro", "isotipo-oscuro"
  ]) {
    const logo = readFileSync(join(root, "public/branding", nombre + ".png"));
    assert.deepEqual([...logo.subarray(0, 8)], [137, 80, 78, 71, 13, 10, 26, 10]);
  }
  const componente = leer("src/components/ui/BrandLogo.vue");
  assert.match(componente, /\/branding\/nombre-blanco\.png/);
  assert.match(componente, /\/branding\/nombre-azul\.png/);
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
  assert.match(panel, /encomiendasApi\.listar\(/);
  assert.match(panel, /items\.value\s*=\s*datos\.encomiendas/);
  assert.doesNotMatch(panel, /reservas activas\s*\d|pr[oó]ximos vencimientos\s*\d/i);
  assert.match(panel, /v-for="e in items"/);
  assert.match(panel, /Reserva #\{\{ e\.id \}\}/);
  assert.match(panel, /estadoEtapa\(detalleDe\(e\)\)/);
  assert.match(panel, /encomiendasApi\.detalle\(e\.id\)/);
  assert.match(panel, /const puedeCancelar|function cancelable\(e:any\)/);
  assert.doesNotMatch(panel, /fechaHonduras\(e\.creadaUtc\)|fechaHonduras\(e\.entregaUtc\)/);
  assert.match(panel, /!cargando\s*&&\s*!error/);
  assert.match(panel, /Ver historial/);
  assert.match(panel, /categoria:categoria\.value/);
  assert.match(panel, /reservas-tarjetas/);
  assert.doesNotMatch(panel, /<table|ReferenciaFacialCaptura/);
});


test("bienvenida reutiliza la estación multifuncional con la marca original", () => {
  const bienvenida = leer("src/views/BienvenidaView.vue");
  const estacion = leer("src/components/ui/LockerJourney.vue");
  const portada = leer("src/views/InicioView.vue");
  assert.doesNotMatch(bienvenida, /TU ESPACIO SYNCHRO|inicio-cliente__casilleros/);
  assert.match(bienvenida, /<LockerJourney compacto\s*\/>/);
  assert.match(portada, /<LockerJourney\s*\/>/);
  assert.match(estacion, /compacto: \{ type: Boolean, default: false \}/);
  assert.match(estacion, /href="\/branding\/marca-completa\.png"/);
  assert.match(estacion, /href="\/branding\/isotipo-oscuro\.png"/);
  assert.match(estacion, /href="\/branding\/nombre-blanco\.png"/);
  assert.match(estacion, /href="\/branding\/nombre-azul\.png"/);
  assert.doesNotMatch(estacion, /<text[^>]*>SYNCHRO<\/text>/);
  assert.match(estacion, /<aside v-if="!props\.compacto" class="syn-v4-note"/);
  assert.match(estacion, /<div v-if="!props\.compacto" class="syn-journey-controls"/);
  assert.match(estacion, /\.syn-v4-compacta \.syn-v4-station \{ width:100%; min-width:0;/);
  assert.match(estacion, /@click="cambiarPuerta\(c\.codigo\)"/);
});
