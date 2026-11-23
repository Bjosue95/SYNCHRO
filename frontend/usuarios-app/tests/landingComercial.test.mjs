import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { empresa, ubicaciones, soluciones, linkMaps, linkWhatsapp } from "../src/data/empresa.js";

const raiz = join(dirname(fileURLToPath(import.meta.url)), "..");
const leer = ruta => readFileSync(join(raiz, ruta), "utf8");

test("datos de contacto coinciden con los oficiales", () => {
  assert.equal(empresa.nombre, "SYNCHRO");
  assert.equal(empresa.correo, "synchrohn@gmail.com");
  assert.equal(empresa.whatsapp, "+50433182642");
  assert.equal(empresa.instagram, "https://www.instagram.com/synchrohn");
  assert.ok(empresa.tiktok.startsWith("https://www.tiktok.com/@synchro_hn"));
  assert.match(empresa.direccion, /CURC/);
  assert.match(empresa.horario, /8:00 a\. m\. a 5:00 p\. m\./);
  assert.match(linkWhatsapp(), /^https:\/\/wa\.me\/50433182642\?/);
});

test("siete ubicaciones se presentan sin afirmar estaciones operativas", () => {
  assert.equal(ubicaciones.length, 7);
  const plaza = ubicaciones.find(x => x.id === "ciudad-nueva");
  assert.equal(plaza.ciudad, "Comayagua");
  assert.match(plaza.direccion, /Comayagua/);
  assert.equal(plaza.coordenadas, null);
  for (const lugar of ubicaciones) {
    assert.equal(typeof lugar.nombre, "string");
    assert.match(lugar.direccion, /Honduras/);
    assert.ok(linkMaps(lugar).startsWith("https://www.google.com/maps/search/"));
    if (lugar.coordenadas) {
      const [lat,long] = lugar.coordenadas;
      assert.ok(lat > 13 && lat < 17);
      assert.ok(long < -86 && long > -90);
    }
  }
});

test("el formulario prepara correo sin simular su envío ni guardar datos", () => {
  const codigo = leer("src/components/landing/QuoteForm.vue");
  assert.match(codigo, /mailto:/);
  assert.match(codigo, /encodeURIComponent/);
  assert.match(codigo, /navigator\.clipboard\.writeText/);
  assert.match(codigo, /No se envía automáticamente/);
  assert.doesNotMatch(codigo, /fetch\(|localStorage|sessionStorage|apiClientes/);
  assert.ok(soluciones.some(x => x.id === "modulos"));
});

test("mapa se carga a demanda y reconoce Plaza Ciudad Nueva, Comayagua", () => {
  const mapa = leer("src/components/landing/LocationsMap.vue");
  assert.match(mapa, /await import\("leaflet"\)/);
  assert.match(mapa, /IntersectionObserver/);
  assert.match(mapa, /mapa\?\.remove\(\)/);
  assert.match(mapa, /Plaza Ciudad Nueva, Comayagua/);
  assert.match(mapa, /Ubicaciones propuestas/);
  assert.match(mapa, /OpenStreetMap/);
  assert.match(mapa, /linkMaps\(lugar\)/);
  assert.doesNotMatch(mapa, /apiClientes|reservarCasillero|cobrar/);
});

test("inicio y pie contienen contacto, cotización, ubicaciones y redes", () => {
  const inicio = leer("src/views/InicioView.vue");
  const layout = leer("src/components/layout/PublicLayout.vue");
  const footer = leer("src/components/landing/SiteFooter.vue");
  const contacto = leer("src/components/landing/ContactSection.vue");
  assert.match(inicio, /<LocationsMap\s*\/>/);
  assert.match(inicio, /<QuoteForm\s*\/>/);
  assert.match(inicio, /<ContactSection\s*\/>/);
  assert.match(layout, /<SiteFooter v-if="!esAcceso"/);
  assert.match(footer, /empresa\.instagram/);
  assert.match(footer, /empresa\.tiktok/);
  assert.match(footer, /empresa\.correo/);
  assert.match(contacto, /empresa\.horario/);
});

test("origen, misión y visión proceden del material de SYNCHRO", () => {
  const inicio = leer("src/views/InicioView.vue");
  const nosotros = leer("src/components/landing/AboutSynchro.vue");
  assert.match(inicio, /<AboutSynchro\s*\/>/);
  assert.match(nosotros, /SYNCHRO nació en 2026/);
  assert.match(nosotros, /Nuestra misión/);
  assert.match(nosotros, /Hacia dónde vamos/);
  assert.match(nosotros, /temperatura controlada/);
});
