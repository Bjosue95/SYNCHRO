// QA visual local. Instalar la herramienta sin cambiar manifests:
// npm install --no-save --package-lock=false --ignore-scripts playwright
// node tests/visual/usuarios.visual.mjs
// Solo se simula la respuesta de sesión anónima; nunca se llama al backend.
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import { chromium } from 'playwright';

const baseURL = process.env.SYNCHRO_VISUAL_URL || 'http://127.0.0.1:5175';
const destino = 'node_modules/.cache/synchro-visual/despues';
mkdirSync(destino, { recursive: true });
const browser = await chromium.launch({
  executablePath: process.env.SYNCHRO_CHROMIUM || 'C:/Program Files/Google/Chrome/Application/chrome.exe',
  headless: true,
});
const resultados = [];

async function pulsarPuerta(page, puerta) {
  const rectangulo = puerta.locator(':scope > rect.syn-v4-cavity');
  await rectangulo.scrollIntoViewIfNeeded();
  const caja = await rectangulo.boundingBox();
  assert.ok(caja);
  // Pulsar el centro fijo del compartimento, sin seguir la hoja deslizante.
  await page.mouse.click(caja.x + caja.width / 2, caja.y + caja.height / 2);
}

try {
  for (const viewport of [{ width: 1440, height: 1000 }, { width: 768, height: 1024 }, { width: 390, height: 844 }]) {
    for (const tema of ['claro', 'oscuro']) {
      const page = await browser.newPage({ viewport, reducedMotion: 'reduce' });
      const errores = [], recursosFallidos = [];
      page.on('pageerror', error => errores.push(error.message));
      page.on('response', r => { if (r.status() >= 400 && !r.url().includes('/api/')) recursosFallidos.push({ url: r.url(), estado: r.status() }); });
      await page.route('**/api/**', ruta => ruta.fulfill({ status: 401, contentType: 'application/json', body: '{}' }));
      await page.goto(baseURL);
      await page.locator('.syn-v4-station').waitFor();
      await page.evaluate(() => document.fonts.ready);
      if (tema === 'oscuro') await page.getByRole('button', { name: 'Activar modo oscuro' }).click();
      assert.equal(await page.locator('html').getAttribute('data-theme'), tema);
      assert.equal(await page.locator('html').evaluate(el => getComputedStyle(el).filter), 'none', 'El tema no debe filtrar ni oscurecer toda la página');
      assert.match(await page.locator('.syn-v4-note-icon use').getAttribute('href'), /\/iconos\/claro\/web-iconos\.svg#/, 'El recuadro blanco conserva un icono azul legible en ambos temas');
      assert.ok(!(await page.locator('.syn-hero-highlights').innerText()).includes('Fr\u00c3'));
      assert.ok((await page.locator('.syn-hero-highlights').innerText()).includes('Frío y calor'));
      assert.equal(await page.locator('.syn-feature .syn-mini-demo').count(), 5);
      assert.equal(await page.locator('.syn-journey .syn-mini-demo').count(), 0);
      assert.equal(await page.locator('.syn-mini-demo').count(), 5);

      const metricas = await page.evaluate(() => ({
        ancho: innerWidth, anchoDocumento: document.documentElement.scrollWidth,
        altoMinimoCompartimento: Math.min(...[...document.querySelectorAll('.syn-v4-cell > rect')].map(el => el.getBoundingClientRect().height)),
        tarjetasOscuras: [...document.querySelectorAll('.syn-feature')].every(el => getComputedStyle(el).backgroundImage.includes('rgb(5, 50, 82)')),
      }));
      assert.equal(metricas.anchoDocumento, viewport.width, 'Desbordamiento horizontal de página');
      assert.ok(metricas.altoMinimoCompartimento >= 24, 'Compartimentos demasiado pequeños');
      assert.ok(metricas.tarjetasOscuras, 'Los módulos deben seguir dentro de tarjetas oscuras');

      await page.screenshot({ path: `${destino}/portada-${viewport.width}-${tema}.png` });
      await page.locator('.syn-v4').scrollIntoViewIfNeeded();
      await page.screenshot({ path: `${destino}/estacion-${viewport.width}-${tema}.png` });
      if (viewport.width < 650) {
        const escena = page.locator('.syn-v4-scene');
        await escena.evaluate(el => { el.scrollLeft = 0; });
        await escena.focus(); await page.keyboard.press('ArrowRight');
        await page.waitForFunction(() => document.querySelector('.syn-v4-scene').scrollLeft > 0);
      }
      for (const codigo of ['M01', 'M03', 'C01', 'C08']) {
        const puerta = page.locator(`.syn-v4-cell[aria-label^="${codigo},"]`);
        await pulsarPuerta(page, puerta);
        assert.equal(await puerta.getAttribute('aria-pressed'), 'true', `Abrir ${codigo}`);
        await pulsarPuerta(page, puerta);
        assert.equal(await puerta.getAttribute('aria-pressed'), 'false', `Cerrar ${codigo}`);
      }
      await page.getByRole('button', { name: 'Ver paso 3: Entrega y retiro' }).click();
      const m03 = page.locator('.syn-v4-cell[aria-label^="M03,"]');
      assert.equal(await m03.getAttribute('aria-pressed'), 'true');
      await m03.focus(); await page.keyboard.press('Enter');
      assert.equal(await m03.getAttribute('aria-pressed'), 'false', 'Cerrar apertura automática con Enter');
      await page.keyboard.press('Space');
      assert.equal(await m03.getAttribute('aria-pressed'), 'true', 'Abrir con Espacio');
      await page.getByRole('button', { name: 'Ver paso 1: Solicitud' }).focus();
      await page.keyboard.press('Home');
      await page.keyboard.press('ArrowRight');
      assert.equal(await page.getByRole('button', { name: 'Ver paso 2: Consentimiento' }).getAttribute('aria-pressed'), 'true');

      for (const modulo of await page.locator('.syn-feature .syn-mini-demo').all()) {
        const puerta = modulo.locator('button').last();
        await puerta.click(); assert.equal(await puerta.getAttribute('aria-pressed'), 'true');
        await puerta.click(); assert.equal(await puerta.getAttribute('aria-pressed'), 'false');
        await puerta.focus(); await page.keyboard.press('Enter'); assert.equal(await puerta.getAttribute('aria-pressed'), 'true');
        await page.keyboard.press('Space'); assert.equal(await puerta.getAttribute('aria-pressed'), 'false');
      }
      await page.locator('#servicios').scrollIntoViewIfNeeded();
      await page.screenshot({ path: `${destino}/servicios-${viewport.width}-${tema}.png` });
      await page.locator('#cotizacion').scrollIntoViewIfNeeded();
      const alineacion = await page.locator('.syn-button-content').evaluate(el => {
        const caja = el.getBoundingClientRect(), icono = el.querySelector('svg').getBoundingClientRect();
        return { display: getComputedStyle(el).display, diferencia: Math.abs(caja.y + caja.height / 2 - icono.y - icono.height / 2) };
      });
      // Un elemento inline-flex se blockifica al ser hijo directo de otro flex.
      assert.ok(['flex', 'inline-flex'].includes(alineacion.display)); assert.ok(alineacion.diferencia < 2);
      await page.screenshot({ path: `${destino}/cotizacion-${viewport.width}-${tema}.png` });
      assert.deepEqual(errores, []); assert.deepEqual(recursosFallidos, []);
      resultados.push({ viewport, tema, metricas, alineacion, errores, recursosFallidos, estado: 'aprobado' });
      console.log(`OK ${viewport.width}px / ${tema}: geometría, tarjetas, mouse, teclado y alineación`);
      await page.close();
    }
  }

  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'no-preference' });
  await page.route('**/api/**', ruta => ruta.fulfill({ status: 401, contentType: 'application/json', body: '{}' }));
  await page.goto(baseURL);
  const puerta = page.locator('.syn-v4-cell[aria-label^="M01,"]');
  await puerta.waitFor(); await pulsarPuerta(page, puerta);
  assert.equal(await puerta.getAttribute('aria-pressed'), 'true');
  const duracion = await puerta.locator('.syn-v4-door').evaluate(el => getComputedStyle(el).transitionDuration);
  assert.ok(parseFloat(duracion) > 0, 'La animación normal debe conservarse');
  await page.emulateMedia({ reducedMotion: 'reduce' });
  const duracionReducida = await puerta.locator('.syn-v4-door').evaluate(el => getComputedStyle(el).transitionDuration);
  assert.ok(parseFloat(duracionReducida) <= .001, 'Movimiento reducido: transición desactivada o prácticamente instantánea');
  await page.close();
  console.log('OK animaciones normales y preferencia de movimiento reducido');
} finally {
  writeFileSync(`${destino}/resultados.json`, JSON.stringify(resultados, null, 2));
  await browser.close();
}
