// Ejecutar con el servidor local de usuarios-app; nunca usa el backend.
// Reutiliza la instalación temporal de Playwright de usuarios.visual.mjs.
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { chromium } from 'playwright';

const destino = 'node_modules/.cache/synchro-visual/estacion';
mkdirSync(destino, { recursive: true });
const browser = await chromium.launch({
  executablePath: process.env.SYNCHRO_CHROMIUM || 'C:/Program Files/Google/Chrome/Application/chrome.exe',
  headless: true,
});
const resultados = [];
const referencia = 'node_modules/.cache/synchro-visual/ajustes-estacion/antes.json';
const antes = existsSync(referencia) ? JSON.parse(readFileSync(referencia, 'utf8')) : [];
try {
  for (const width of [1440, 768, 390, 320]) {
    for (const tema of ['claro', 'oscuro']) {
      const touch = width < 1000;
      const page = await browser.newPage({ viewport: { width, height: 1000 }, hasTouch: touch, reducedMotion: 'reduce' });
      const errores = [];
      page.on('pageerror', e => errores.push(e.message));
      await page.route('**/api/**', r => r.fulfill({ status: 401, contentType: 'application/json', body: '{}' }));
      await page.goto(process.env.SYNCHRO_VISUAL_URL || 'http://127.0.0.1:5175/');
      await page.locator('.syn-v4-station').waitFor();
      if (tema === 'oscuro') await page.getByRole('button', { name: 'Activar modo oscuro' }).click();

      const celdas = await page.locator('.syn-v4-cell').evaluateAll(nodos => nodos.map(el => {
        const rect = el.querySelector(':scope > rect');
        return { codigo: el.getAttribute('aria-label').split(',')[0], objeto: el.querySelector('.syn-station-object').dataset.objeto,
          x: Number(rect.getAttribute('x')), y: Number(rect.getAttribute('y')),
          w: Number(rect.getAttribute('width')), h: Number(rect.getAttribute('height')) };
      }));
      assert.equal(celdas.length, 18);
      assert.equal(await page.locator('.syn-v4-cell[aria-pressed="true"]').count(), 0);
      const columnas = [43, 170, 278].map(x => celdas.filter(c => c.x === x).sort((a, b) => a.y - b.y));
      assert.deepEqual(columnas.map(col => col.map(c => c.codigo)), [
        ['M01', 'M02', 'L01'], ['S01', 'S02', 'M04', 'M05'], ['M03', 'M06', 'L02'],
      ]);
      assert.deepEqual(columnas[0].map(c => c.objeto), ['mochila', 'frio', 'maleta']);
      assert.deepEqual(columnas[2].map(c => c.objeto), ['caja', 'caliente', 'maleta']);
      assert.deepEqual(columnas[0].map(c => [c.w, c.h]), columnas[2].map(c => [c.w, c.h]));
      assert.ok(columnas[0][2].h > columnas[0][0].h);
      assert.ok(Math.max(...columnas[1].slice(0, 2).map(c => c.h)) < Math.min(...columnas[1].slice(2).map(c => c.h)));
      for (const col of columnas) {
        assert.equal(col[0].y, 113); assert.equal(col.at(-1).y + col.at(-1).h, 370);
        col.slice(1).forEach((c, i) => assert.equal(c.y - col[i].y - col[i].h, 6));
      }
      const carga = celdas.filter(c => c.codigo.startsWith('C'));
      assert.deepEqual([...new Set(carga.map(c => c.x))], [533, 613]);
      assert.equal(new Set(carga.map(c => c.y)).size, 4);
      assert.ok(carga.every(c => c.w === 73 && c.h === 37 && c.w / c.h < 2));
      assert.deepEqual(carga.map(c => c.objeto), ['laptop', 'laptop', ...Array(6).fill('celular')]);
      assert.equal(await page.locator('.syn-v4-screen-brand image[href="/branding/isotipo-oscuro.png"]').count(), 1);
      assert.equal(await page.locator('.syn-v4-screen-brand image[href="/branding/nombre-blanco.png"]').count(), 1);

      const geometria = await page.locator('.syn-v4-station').evaluate(svg => {
        const caja = el => { const r = el.getBoundingClientRect(); return { x:r.x, y:r.y, w:r.width, h:r.height, right:r.right }; };
        const paneles = [
          ...['M01', 'S01', 'M03'].map(c => svg.querySelector(`.syn-v4-cell[aria-label^="${c},"] > rect`)),
          svg.querySelector('.syn-v4-tablet > rect'), svg.querySelector('.syn-v4-charge-housing'),
        ].map(caja);
        return { estacion:caja(svg), paneles, pantalla:caja(svg.querySelector('.syn-v4-charge-screen')),
          logo:caja(svg.querySelector('.syn-v4-screen-brand')),
          casilleros:[...svg.querySelectorAll('.syn-v4-cell')].map(el => ({ codigo:el.getAttribute('aria-label').split(',')[0], ...caja(el.querySelector(':scope > rect')) })) };
      });
      const escala = geometria.paneles[0].w / 121;
      for (let i = 1; i < geometria.paneles.length; i++) {
        const hueco = geometria.paneles[i].x - geometria.paneles[i - 1].right;
        assert.ok(hueco >= 6 * escala - .01 && hueco <= 10 * escala + .01, 'Orden y separaciones sin superposiciones ni huecos exagerados');
      }
      const tablet = geometria.paneles[3], moduloCarga = geometria.paneles[4];
      assert.ok(Math.abs(tablet.w / escala - 107) < .01 && Math.abs(tablet.h / escala - 262) < .01);
      assert.ok(Math.abs(moduloCarga.w / escala - 169) < .01 && Math.abs(moduloCarga.h / escala - 262) < .01);
      assert.ok(geometria.pantalla.x > moduloCarga.x && geometria.pantalla.right < moduloCarga.right);
      assert.ok(geometria.logo.x > geometria.pantalla.x && geometria.logo.right < geometria.pantalla.right);
      const original = antes.find(v => v.width === width);
      if (original) {
        assert.ok(Math.abs(geometria.estacion.h - original.estacion.h) < .02, 'Conservar la altura visible de la estación');
        for (const celda of geometria.casilleros) {
          const previa = original.casilleros.find(c => c.codigo === celda.codigo);
          assert.ok(Math.abs(celda.h - previa.h) < .02, `Altura conservada: ${celda.codigo}`);
          if (!celda.codigo.startsWith('C')) assert.ok(Math.abs(celda.w - previa.w) < .02, `Ancho conservado: ${celda.codigo}`);
        }
      }

      await page.locator('.syn-v4').scrollIntoViewIfNeeded();
      await page.screenshot({ path: `${destino}/cerrada-${width}-${tema}.png` });
      for (const celda of celdas) {
        const puerta = page.locator(`.syn-v4-cell[aria-label^="${celda.codigo},"]`);
        await puerta.focus(); await page.keyboard.press('Enter');
        assert.equal(await puerta.getAttribute('aria-pressed'), 'true');
        const apertura = await puerta.evaluate(el => {
          const hoja = el.querySelector('.syn-v4-door');
          const matriz = new DOMMatrix(getComputedStyle(hoja).transform);
          const marco = hoja.parentElement;
          return { a: matriz.a, b: matriz.b, c: matriz.c, d: matriz.d, tx: matriz.e,
            recorte: marco.getAttribute('clip-path'), visible: getComputedStyle(el.querySelector('.syn-v4-object')).opacity };
        });
        assert.deepEqual([apertura.a, apertura.b, apertura.c, apertura.d], [1, 0, 0, 1], 'La apertura no debe escalar ni deformar la hoja');
        assert.ok(Math.abs(apertura.tx + celda.w * .82) < .01);
        assert.match(apertura.recorte, /^url\(#.+\)$/); assert.equal(apertura.visible, '1');
        if (width === 1440 && tema === 'claro' && ['M01', 'M02', 'L01', 'M03', 'M06', 'C01', 'C03'].includes(celda.codigo)) {
          await page.locator('.syn-v4-station').screenshot({ path: `${destino}/objeto-${celda.objeto}.png` });
        }
        await page.keyboard.press('Space');
        assert.equal(await puerta.getAttribute('aria-pressed'), 'false');
      }
      // Pulsar el centro fijo evita seguir la hoja de la puerta mientras se desliza.
      for (const celda of celdas) {
        const puerta = page.locator(`.syn-v4-cell[aria-label^="${celda.codigo},"]`);
        const rect = puerta.locator(':scope > rect');
        await rect.evaluate(el => el.scrollIntoView({ block:'center', inline:'center', behavior:'instant' }));
        const caja = await rect.boundingBox();
        for (const esperado of ['true', 'false']) {
          if (touch) await page.touchscreen.tap(caja.x + caja.width / 2, caja.y + caja.height / 2);
          else await page.mouse.click(caja.x + caja.width / 2, caja.y + caja.height / 2);
          await page.waitForFunction(({ codigo, esperado }) => document.querySelector(`.syn-v4-cell[aria-label^="${codigo},"]`).getAttribute('aria-pressed') === esperado, { codigo:celda.codigo, esperado });
        }
      }
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth), width);
      assert.deepEqual(errores, []);
      resultados.push({ width, tema, celdas: celdas.length, estructura: 'aprobada', aperturas: '18 sin deformaciones, teclado y mouse/táctil', escalaConservada:!!original, geometria, errores });
      console.log(`OK estación ${width}px / ${tema}: estructura, proporciones, 18 puertas y objetos SVG`);
      await page.close();
    }
  }
} finally {
  writeFileSync(`${destino}/resultados.json`, JSON.stringify(resultados, null, 2));
  await browser.close();
}
