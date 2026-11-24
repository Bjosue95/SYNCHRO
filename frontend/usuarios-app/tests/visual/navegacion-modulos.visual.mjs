// Chrome real; sesión anónima simulada para no utilizar backend ni base de datos.
// Ejecutar con el servidor local: node tests/visual/navegacion-modulos.visual.mjs
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { chromium } from 'playwright';

const baseURL = process.env.SYNCHRO_VISUAL_URL || 'http://127.0.0.1:5175';
const destino = 'node_modules/.cache/synchro-visual/navegacion';
mkdirSync(destino, { recursive: true });
const antes = existsSync(`${destino}/antes.json`) ? JSON.parse(readFileSync(`${destino}/antes.json`, 'utf8')) : [];
const browser = await chromium.launch({
  executablePath: process.env.SYNCHRO_CHROMIUM || 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true,
});
const resultados = [];

async function navegar(page, hash, compacto) {
  if (compacto) await page.locator('.menu-publico').click();
  await page.locator(`.nav-publica a[href="/${hash}"]`).click();
  await page.waitForURL(`**/${hash}`);
  if (hash === '#inicio') {
    await page.waitForFunction(() => scrollY < 2);
  } else {
    await page.waitForFunction(id => {
      const target = document.getElementById(id);
      const header = document.querySelector('.cabecera-publica');
      if (!target || !header) return false;
      const delta = target.getBoundingClientRect().top - header.getBoundingClientRect().bottom;
      return delta >= 8 && delta <= 18;
    }, hash.slice(1));
  }
  if (compacto) assert.equal(await page.locator('.menu-publico').getAttribute('aria-expanded'), 'false');
}

try {
  for (const width of [1440, 768, 390, 320]) {
    for (const tema of ['claro', 'oscuro']) {
      const touch = width < 1000;
      const page = await browser.newPage({ viewport: { width, height: 1000 }, hasTouch: touch, reducedMotion: 'reduce' });
      const errores = [];
      page.on('pageerror', e => errores.push(e.message));
      page.on('response', r => { if (r.status() >= 400 && !r.url().includes('/api/')) errores.push(`${r.status()}: ${r.url()}`); });
      await page.route('**/api/**', r => r.fulfill({ status: 401, contentType: 'application/json', body: '{}' }));
      await page.goto(baseURL);
      await page.locator('.syn-mini-demo').first().waitFor();
      await page.evaluate(() => document.fonts.ready);
      if (tema === 'oscuro') await page.getByRole('button', { name: 'Activar modo oscuro' }).click();
      assert.equal(await page.locator('html').getAttribute('data-theme'), tema);
      const tarjetas = await page.locator('.syn-feature').evaluateAll(els => els.map(el => {
        const r = el.getBoundingClientRect(), s = getComputedStyle(el);
        return { width: r.width, height: r.height, background: s.backgroundImage, padding: s.padding, gap: s.gap };
      }));
      const original = antes.find(v => v.width === width);
      if (original) assert.deepEqual(tarjetas, original.tarjetas, 'Conservar exactamente dimensiones, colores y distribución de tarjetas');
      assert.equal(await page.locator('.syn-service-section .syn-mini-demo').count(), 5);
      assert.equal(await page.locator('.syn-mini-demo').count(), 5);
      assert.equal(await page.locator('.syn-mini-celda[aria-pressed="true"]').count(), 0, 'Puertas inicialmente cerradas');
      assert.equal(await page.locator('.nav-publica [aria-current="location"]').count(), 1);
      assert.equal(await page.locator('.nav-publica [aria-current="page"]').count(), 0);
      assert.deepEqual(await page.locator('.nav-publica a').evaluateAll(els => els.map(el => [el.textContent.trim(), el.getAttribute('href')])), [
        ['Inicio', '/#inicio'], ['Servicios', '/#servicios'], ['Nosotros', '/#nosotros'], ['Contáctanos', '/#contacto'],
      ]);
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth), width);
      await page.screenshot({ path: `${destino}/inicio-${width}-${tema}.png` });

      const compacto = width <= 900;
      const menu = page.locator('.menu-publico');
      if (compacto) {
        await menu.focus(); await page.keyboard.press('Enter');
        assert.equal(await menu.getAttribute('aria-expanded'), 'true');
        await page.keyboard.press('Tab');
        assert.equal(await page.locator('.nav-publica a').first().evaluate(el => el === document.activeElement), true);
        assert.notEqual(await page.locator('.nav-publica a').first().evaluate(el => getComputedStyle(el).outlineStyle), 'none');
        await page.keyboard.press('Escape');
        assert.equal(await menu.evaluate(el => el === document.activeElement), true);
        assert.equal(await menu.getAttribute('aria-expanded'), 'false');
        await menu.tap();
        await page.screenshot({ path: `${destino}/menu-${width}-${tema}.png` });
        await page.touchscreen.tap(width - 12, 350); // Fondo fuera del desplegable.
        assert.equal(await menu.getAttribute('aria-expanded'), 'false', 'Cerrar al tocar fuera');
        await menu.focus(); await page.keyboard.press('Space');
        await page.keyboard.press('Tab'); await page.keyboard.press('Tab'); await page.keyboard.press('Enter');
        await page.waitForURL('**/#servicios');
        assert.equal(await menu.getAttribute('aria-expanded'), 'false', 'Navegar con teclado y cerrar');
      }
      await navegar(page, '#servicios', compacto);
      const fijos = await page.locator('.cabecera-publica').evaluate(el => {
        const r = el.getBoundingClientRect();
        return { top: r.top, position: getComputedStyle(el).position };
      });
      assert.equal(fijos.position, 'sticky'); assert.equal(fijos.top, 0);
      for (const selector of ['.boton-tema', '.acceso-enlace']) {
        const rect = await page.locator(selector).boundingBox();
        assert.ok(rect.x >= 0 && rect.x + rect.width <= width && rect.y >= 0 && rect.y + rect.height < 100);
      }
      await page.locator('.boton-tema').click();
      assert.equal(await page.locator('html').getAttribute('data-theme'), tema === 'claro' ? 'oscuro' : 'claro');
      await page.locator('.boton-tema').click();
      assert.equal(await page.locator('html').getAttribute('data-theme'), tema);
      await page.locator('.acceso-enlace').click();
      await page.waitForURL('**/acceso'); await page.locator('input[type="password"]').waitFor();
      await navegar(page, '#contacto', compacto);
      await page.screenshot({ path: `${destino}/contacto-${width}-${tema}.png` });
      await page.locator('.acceso-enlace').click();
      await page.waitForURL('**/acceso'); await page.locator('input[type="password"]').waitFor();
      await navegar(page, '#servicios', compacto);
      await page.locator('.acceso-enlace').click();
      await page.waitForURL('**/acceso'); await page.locator('input[type="password"]').waitFor();
      await navegar(page, '#nosotros', compacto);
      await page.screenshot({ path: `${destino}/nosotros-${width}-${tema}.png` });
      await navegar(page, '#inicio', compacto);
      await navegar(page, '#inicio', compacto); // Repetir la misma ubicación.

      const modulos = [];
      for (const modulo of await page.locator('.syn-mini-demo').all()) {
        const datos = await modulo.evaluate(el => {
          const rejilla = el.querySelector('.syn-mini-rejilla');
          return { nombre: el.getAttribute('aria-label'), cols: rejilla.style.getPropertyValue('--cols'),
            filas: rejilla.style.getPropertyValue('--rows'),
            objetos: [...el.querySelectorAll('svg')].map(svg => svg.dataset.objeto) };
        });
        const prefijo = (await modulo.locator('.syn-mini-codigo').first().textContent())[0];
        const tamanos = { P: ['3', '4'], T: ['4', '3'], L: ['5', '2'], E: ['3', '3'], C: ['2', '4'] };
        assert.deepEqual([datos.cols, datos.filas], tamanos[prefijo]);
        assert.equal(datos.objetos.length, Number(datos.cols) * Number(datos.filas));
        if (prefijo === 'C') assert.deepEqual(datos.objetos, ['laptop', 'laptop', ...Array(6).fill('celular')]);
        if (prefijo === 'T') assert.deepEqual(datos.objetos, Array.from({ length: 12 }, (_, i) => i % 4 < 2 ? 'frio' : 'caliente'));
        if (prefijo === 'E') assert.ok(datos.objetos.every(o => o === 'caja'));
        if (prefijo === 'P') assert.ok(['mochila', 'bolso'].every(o => datos.objetos.includes(o)));
        if (prefijo === 'L') assert.ok(['maleta', 'mochila', 'bolso'].every(o => datos.objetos.includes(o)));
        for (const puerta of await modulo.locator('button').all()) {
          assert.equal(await puerta.getAttribute('aria-pressed'), 'false');
          assert.equal(await puerta.locator('.syn-mini-interior').evaluate(el => getComputedStyle(el).opacity), '0');
          if (touch) await puerta.tap(); else await puerta.click();
          assert.equal(await puerta.getAttribute('aria-pressed'), 'true');
          const apertura = await puerta.evaluate(el => {
            const hoja = el.querySelector('.syn-mini-puerta'), ventana = el.querySelector('.syn-mini-ventana');
            const objeto = el.querySelector('svg').getBoundingClientRect(), marco = ventana.getBoundingClientRect();
            const m = new DOMMatrix(getComputedStyle(hoja).transform);
            return { matriz: [m.a, m.b, m.c, m.d], tx: m.e, recorte: getComputedStyle(ventana).overflow,
              centrado: Math.abs(objeto.x + objeto.width / 2 - marco.x - marco.width / 2) < 1 && Math.abs(objeto.y + objeto.height / 2 - marco.y - marco.height / 2) < 1,
              contenido: objeto.x >= marco.x && objeto.right <= marco.right && objeto.y >= marco.y && objeto.bottom <= marco.bottom,
              opacidad: getComputedStyle(el.querySelector('.syn-mini-interior')).opacity };
          });
          assert.deepEqual(apertura.matriz, [1, 0, 0, 1]); assert.ok(apertura.tx < 0);
          assert.equal(apertura.recorte, 'hidden'); assert.equal(apertura.opacidad, '1');
          assert.ok(apertura.centrado && apertura.contenido, 'Objeto centrado dentro de su compartimento');
          if (touch) await puerta.tap(); else await puerta.click();
          assert.equal(await puerta.getAttribute('aria-pressed'), 'false');
        }
        const botones = modulo.locator('button');
        await botones.first().focus(); await page.keyboard.press('Enter');
        await botones.nth(1).focus(); await page.keyboard.press('Space');
        assert.equal(await modulo.locator('[aria-pressed="true"]').count(), 2, 'Aperturas independientes');
        await modulo.screenshot({ path: `${destino}/modulo-${prefijo}-${width}-${tema}.png` });
        await page.keyboard.press('Enter'); await botones.first().focus(); await page.keyboard.press('Space');
        assert.equal(await modulo.locator('[aria-pressed="true"]').count(), 0);
        // Preparación de captura: las interacciones reales se comprueban arriba.
        await botones.evaluateAll(els => els.forEach(el => el.click()));
        await modulo.screenshot({ path: `${destino}/objetos-${prefijo}-${width}-${tema}.png` });
        await botones.evaluateAll(els => els.forEach(el => el.click()));
        modulos.push(datos);
      }
      assert.deepEqual(errores, []);
      resultados.push({ width, tema, touch, fijos, tarjetasSinCambios: !!original, modulos, errores, estado: 'aprobado' });
      console.log(`OK ${width}px / ${tema}: sticky, rutas, menú, tema, acceso, 51 puertas, SVG centrados, tarjetas conservadas`);
      await page.close();
    }
  }
  // La navegación entre rutas y las puertas también deben funcionar con movimiento normal.
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'no-preference' });
  await page.route('**/api/**', r => r.fulfill({ status: 401, contentType: 'application/json', body: '{}' }));
  await page.goto(`${baseURL}/acceso`); await page.locator('input[type="password"]').waitFor();
  await navegar(page, '#nosotros', false);
  await navegar(page, '#servicios', false);
  const puerta = page.locator('.syn-mini-celda').first();
  await puerta.click();
  assert.ok(parseFloat(await puerta.locator('.syn-mini-puerta').evaluate(el => getComputedStyle(el).transitionDuration)) > .1);
  await page.waitForFunction(() => getComputedStyle(document.querySelector('.syn-mini-interior')).opacity === '1');
  await page.emulateMedia({ reducedMotion: 'reduce' });
  assert.ok(parseFloat(await puerta.locator('.syn-mini-puerta').evaluate(el => getComputedStyle(el).transitionDuration)) < .001);
  await puerta.click(); assert.equal(await puerta.getAttribute('aria-pressed'), 'false');
  await page.close();
  console.log('OK transiciones normales y movimiento reducido');
} finally {
  writeFileSync(`${destino}/resultados.json`, JSON.stringify(resultados, null, 2));
  await browser.close();
}
