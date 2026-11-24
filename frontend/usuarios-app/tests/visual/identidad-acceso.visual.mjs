// Chrome real y API controlada: no se realizan intentos sobre cuentas ni se utiliza el backend.
// node tests/visual/identidad-acceso.visual.mjs
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { chromium } from 'playwright';

const baseURL = process.env.SYNCHRO_VISUAL_URL || 'http://127.0.0.1:5175';
const destino = 'node_modules/.cache/synchro-visual/identidad-acceso';
mkdirSync(destino, { recursive: true });
const referencia = `${destino}/antes.json`;
const antes = existsSync(referencia) ? JSON.parse(readFileSync(referencia, 'utf8')) : [];
const browser = await chromium.launch({
  executablePath: process.env.SYNCHRO_CHROMIUM || 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true,
});
const resultados = [];

async function estadoCampo(campo, esperado) {
  await campo.page().waitForFunction(({ id, esperado }) => document.getElementById(id)?.getAttribute('aria-invalid') === esperado,
    { id: await campo.getAttribute('id'), esperado });
}

async function verificarHero(page, referencia, actual) {
  if (createHash('sha256').update(actual).digest('hex') === referencia.heroHash) return { diferenciaMaxima:0 };
  const anterior = readFileSync(`${destino}/hero-antes-${referencia.width}-${referencia.tema}.png`);
  const diferencia = await page.evaluate(async datos => {
    const imagenes = await Promise.all(datos.map(data => new Promise(resolve => {
      const img = new Image();
      img.onload = () => {
        const canvas = document.createElement('canvas'); canvas.width = img.width; canvas.height = img.height;
        const ctx = canvas.getContext('2d'); ctx.drawImage(img, 0, 0);
        resolve({ width:img.width, height:img.height, pixels:ctx.getImageData(0, 0, img.width, img.height).data });
      };
      img.src = `data:image/png;base64,${data}`;
    })));
    const [a, b] = imagenes;
    if (a.width !== b.width || a.height !== b.height) return { dimensionesIguales:false };
    let maxima = 0;
    for (let i = 0; i < a.pixels.length; i++) maxima = Math.max(maxima, Math.abs(a.pixels[i] - b.pixels[i]));
    return { dimensionesIguales:true, diferenciaMaxima:maxima };
  }, [anterior, actual].map(buf => buf.toString('base64')));
  assert.equal(diferencia.dimensionesIguales, true);
  // Chrome puede cuantizar los gradientes con una diferencia de hasta 2/255 entre contextos GPU.
  // Se comprueba cada píxel: ningún cambio de geometría, texto o color significativo se admite.
  assert.ok(diferencia.diferenciaMaxima <= 2, `Cambio visual en el hero: ${diferencia.diferenciaMaxima}/255`);
  return diferencia;
}

async function verificarContrastes(page, pares) {
  const contrastes = await page.evaluate(pares => {
    const luminancia = color => {
      const rgb = color.match(/[\d.]+/g).slice(0, 3).map(Number).map(v => {
        const c = v / 255; return c <= .04045 ? c / 12.92 : ((c + .055) / 1.055) ** 2.4;
      });
      return rgb[0] * .2126 + rgb[1] * .7152 + rgb[2] * .0722;
    };
    return pares.map(([selector, fondo, pseudo]) => {
      const el = document.querySelector(selector), superficie = document.querySelector(fondo);
      const texto = getComputedStyle(el, pseudo || null).color, colorFondo = getComputedStyle(superficie).backgroundColor;
      const a = luminancia(texto), b = luminancia(colorFondo);
      return { selector, texto, fondo:colorFondo, contraste:(Math.max(a, b) + .05) / (Math.min(a, b) + .05) };
    });
  }, pares);
  for (const c of contrastes) assert.ok(c.contraste >= 4.5, `${c.selector}: contraste ${c.contraste.toFixed(2)}`);
  return contrastes;
}

try {
  for (const width of [1440, 768, 390]) for (const tema of ['claro', 'oscuro']) {
    const page = await browser.newPage({ viewport:{ width, height:1000 }, hasTouch:width < 1000,
      colorScheme:tema === 'claro' ? 'light' : 'dark', reducedMotion:'reduce' });
    const errores = [], peticiones = [];
    let respuesta = 401, red = false, csrfAusente = false, autenticado = false, espera = null;
    let avisarSolicitud = () => {};
    page.on('pageerror', e => errores.push(e.message));
    page.on('response', r => { if (r.status() >= 400 && !r.url().includes('/api/')) errores.push(`${r.status()}: ${r.url()}`); });
    await page.route('**/api/**', async ruta => {
      const req = ruta.request(), path = new URL(req.url()).pathname;
      if (path === '/api/web/clientes/mi-sesion') return ruta.fulfill({ status:autenticado ? 200 : 401, json:autenticado ? { nombreUsuario:'cliente.prueba' } : {} });
      if (path === '/api/web/clientes/acceso/csrf-token') return ruta.fulfill({ json:csrfAusente ? {} : { csrfToken:'csrf-controlado' } });
      // El panel autenticado ahora consulta contratos reales de encomiendas.
      // Este arnés de acceso permanece aislado y devuelve una lista vacía explícita.
      if (path === '/api/web/encomiendas') return ruta.fulfill({json:{encomiendas:[],limite:200}});
      if (path === '/api/web/encomiendas/configuracion') return ruta.fulfill({json:{habilitado:true,tarifas:{pequeno:null,mediano:null,grande:null},tabletIntegrada:false}});
      if (path === '/api/web/encomiendas/referencia-facial') return ruta.fulfill({json:{registrada:false,vigente:false}});
      if (path === '/api/web/clientes/acceso/login' || path === '/api/web/clientes/acceso/registro') {
        assert.equal(req.method(), 'POST'); assert.equal(req.headers()['x-csrf-token'], 'csrf-controlado');
        const datos = req.postDataJSON();
        assert.equal(datos.nombreUsuario, 'cliente.prueba');
        assert.equal(typeof datos.contrasena, 'string');
        assert.deepEqual(Object.keys(datos).sort(), path.endsWith('registro') ? ['contrasena', 'nombreUsuario', 'telefono'] : ['contrasena', 'nombreUsuario']);
        peticiones.push({ path, estado:red ? 'red' : respuesta });
        avisarSolicitud();
        if (espera) await espera;
        if (red) return ruta.abort('failed');
        if (respuesta === 200) autenticado = true;
        // Esta información deliberadamente identificable nunca debe aparecer en el error de la UI.
        return ruta.fulfill({ status:respuesta, json:respuesta === 200 ? { nombreUsuario:'cliente.prueba' } : { mensaje:'CUENTA_EXISTENTE_DE_PRUEBA' } });
      }
      throw new Error(`Petición API inesperada: ${path}`);
    });

    await page.goto(baseURL); await page.locator('.syn-v4-station').waitFor(); await page.evaluate(() => document.fonts.ready);
    assert.equal(await page.locator('html').getAttribute('data-theme'), tema);
    const hero = await page.locator('.syn-home-hero').screenshot({ path:`${destino}/hero-validado-${width}-${tema}.png` });
    const previa = antes.find(v => v.width === width && v.tema === tema);
    const comparacionHero = previa ? await verificarHero(page, previa, hero) : null;
    const contrastesHome = await verificarContrastes(page, [
      ['.nav-link', '.cabecera-publica'], ['.syn-about-story h2', '.syn-about'],
      ['.syn-about-story-copy p', '.syn-about'], ['.syn-about-block p', '.syn-about-block'],
      ['.syn-contacto-copy > span', '.syn-contacto-item'], ['.syn-contacto-cta', '.syn-contacto-cta'],
      ['.syn-quote-form input', '.syn-quote-form input'], ['.syn-footer-bottom', '.syn-footer'],
    ]);
    for (const selector of ['.syn-about', '.syn-contacto']) {
      assert.deepEqual(await page.locator(selector).evaluate(el => getComputedStyle(el, '::before').backgroundSize.split(', ')), ['56px 56px', '56px 56px']);
      assert.equal(await page.locator(selector).evaluate(el => getComputedStyle(el, '::before').pointerEvents), 'none');
    }
    for (const [selector, nombre] of [['#servicios','servicios'], ['#nosotros','nosotros'], ['#contacto','contacto'], ['#cotizacion','cotizacion'], ['.syn-footer','pie']]) {
      await page.locator(selector).scrollIntoViewIfNeeded();
      await page.screenshot({ path:`${destino}/${nombre}-${width}-${tema}.png` });
    }
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth), width);

    await page.goto(`${baseURL}/acceso`); await page.locator('#acceso-contrasena').waitFor();
    const usuario = page.getByLabel('Nombre de usuario', { exact:true });
    const clave = page.getByLabel('Contraseña', { exact:true });
    const ingresar = page.locator('button[type="submit"]');
    assert.equal(await clave.getAttribute('type'), 'password');
    const contrastesAcceso = await verificarContrastes(page, [
      ['#titulo-acceso', '.acceso-formulario'], ['.acceso-form-header p', '.acceso-formulario'],
      ['.campo label', '.acceso-formulario'], ['#acceso-usuario', '#acceso-usuario'],
      ['#acceso-usuario', '#acceso-usuario', '::placeholder'], ['#usuario-ayuda', '.acceso-formulario'],
      ['.acceso-enviar', '.acceso-enviar'],
    ]);
    await page.screenshot({ path:`${destino}/ingreso-${width}-${tema}.png` });
    await ingresar.click();
    await estadoCampo(usuario, 'true'); await estadoCampo(clave, 'true');
    assert.ok((await page.locator('#usuario-error').textContent()).includes('Escribe tu nombre'));
    assert.ok((await page.locator('#contrasena-error').textContent()).includes('Escribe tu contrase'));
    assert.equal(await usuario.evaluate(el => el === document.activeElement), true);
    assert.equal(peticiones.length, 0);
    await page.screenshot({ path:`${destino}/errores-campos-${width}-${tema}.png` });
    await usuario.fill('correo@ejemplo.com'); await clave.fill('DatoDePrueba_123!'); await ingresar.click();
    await estadoCampo(usuario, 'true'); assert.equal(peticiones.length, 0);
    await usuario.fill('cliente.prueba'); await estadoCampo(usuario, 'false');

    const mostrar = page.getByRole('button', { name:'Mostrar contraseña', exact:true });
    if (width < 1000) await mostrar.tap(); else await mostrar.click();
    assert.equal(await clave.getAttribute('type'), 'text'); assert.equal(await clave.inputValue(), 'DatoDePrueba_123!');
    await page.getByRole('button', { name:'Ocultar contraseña', exact:true }).focus(); await page.keyboard.press('Space');
    assert.equal(await clave.getAttribute('type'), 'password');
    await page.keyboard.press('Enter'); assert.equal(await clave.getAttribute('type'), 'text');
    await page.keyboard.press('Space'); assert.equal(await clave.getAttribute('type'), 'password');
    assert.equal(peticiones.length, 0, 'El control de contraseña no debe enviar el formulario');
    await page.locator('.boton-tema').click();
    const opuesto = tema === 'claro' ? 'oscuro' : 'claro';
    await page.waitForFunction(t => document.documentElement.dataset.theme === t, opuesto);
    assert.equal(await clave.inputValue(), 'DatoDePrueba_123!');
    await page.waitForTimeout(270); // El cambio de tema existente agrupa pulsaciones rápidas.
    await page.locator('.boton-tema').click();
    await page.waitForFunction(t => document.documentElement.dataset.theme === t, tema);
    assert.equal(await page.locator('html').evaluate(el => getComputedStyle(el).filter), 'none');
    for (const selector of ['#acceso-contrasena', '.acceso-ver-clave', '.acceso-enviar', '.acceso-cambiar button']) {
      assert.ok((await page.locator(selector).boundingBox()).height >= 44, `Objetivo táctil ${selector}`);
    }

    for (const [estado, fragmento] of [[401,'Verifica tus datos'], [400,'validar los datos'], [403,'no autorizó'], [423,'restringido'], [429,'Demasiadas solicitudes'], [500,'no está disponible'], [503,'no está disponible']]) {
      respuesta = estado; await ingresar.click();
      await page.waitForFunction(texto => document.querySelector('.acceso-error')?.textContent.includes(texto), fragmento);
      assert.ok(!(await page.locator('.acceso-error').textContent()).includes('CUENTA_EXISTENTE'));
      assert.equal(await ingresar.isEnabled(), true); assert.equal(await clave.inputValue(), 'DatoDePrueba_123!');
      if (estado === 401) await page.screenshot({ path:`${destino}/credenciales-${width}-${tema}.png` });
    }
    red = true; await ingresar.click();
    await page.waitForFunction(() => document.querySelector('.acceso-error')?.textContent.includes('conectar con el servidor'));
    assert.equal(await ingresar.isEnabled(), true); red = false;
    csrfAusente = true; const previas = peticiones.length; await ingresar.click();
    await page.waitForFunction(() => document.querySelector('.acceso-error')?.textContent.includes('verificar la seguridad'));
    assert.equal(peticiones.length, previas, 'No enviar credenciales sin CSRF'); csrfAusente = false;

    respuesta = 200;
    let liberar;
    espera = new Promise(resolve => { liberar = resolve; });
    const recibida = new Promise(resolve => { avisarSolicitud = resolve; });
    await ingresar.click(); await page.waitForFunction(() => document.querySelector('.acceso-enviar')?.disabled);
    await recibida;
    assert.ok(page.url().includes('/acceso'));
    assert.equal(await page.locator('.syn-button-spinner').count(), 1);
    await page.waitForFunction(() => document.querySelector('.acceso-enviar')?.getAttribute('aria-busy') === 'true');
    const durante = peticiones.length;
    await page.locator('form').evaluate(el => { el.requestSubmit(); el.requestSubmit(); });
    await page.waitForTimeout(80); assert.equal(peticiones.length, durante, 'Una única petición pendiente');
    await page.screenshot({ path:`${destino}/cargando-${width}-${tema}.png` });
    liberar(); await page.waitForURL('**/mi-cuenta'); await page.locator('.app-shell').waitFor();
    assert.equal(await page.evaluate(() => JSON.stringify({ ...localStorage, ...sessionStorage }).includes('DatoDePrueba')), false);
    assert.deepEqual(errores, []);
    resultados.push({ width, tema, comparacionHero, contrastesHome, contrastesAcceso, estados:peticiones.map(p => p.estado), errores, estado:'aprobado' });
    console.log(`OK identidad/acceso ${width}px / ${tema}: hero idéntico, contraste, campos, contraseña, HTTP, red, CSRF, carga y sesión controlada`);
    await page.close();
  }

  // Registro y ventanas bajas: conservar validaciones y comprobar que los controles sean alcanzables.
  for (const viewport of [{ width:320, height:568 }, { width:768, height:600 }, { width:812, height:375 }]) {
    const page = await browser.newPage({ viewport, reducedMotion:'reduce' });
    let registrado = false, peticiones = 0;
    await page.route('**/api/**', ruta => {
      const path = new URL(ruta.request().url()).pathname;
      if (path.endsWith('mi-sesion')) return ruta.fulfill({ status:registrado ? 200 : 401, json:registrado ? { nombreUsuario:'cliente.prueba' } : {} });
      if (path.endsWith('csrf-token')) return ruta.fulfill({ json:{ csrfToken:'csrf-registro' } });
      assert.ok(path.endsWith('/registro')); assert.equal(ruta.request().headers()['x-csrf-token'], 'csrf-registro');
      assert.equal(ruta.request().postDataJSON().telefono, '+50499998888');
      peticiones++; registrado = true; return ruta.fulfill({ json:{ nombreUsuario:'cliente.prueba' } });
    });
    await page.goto(`${baseURL}/acceso?registro=1`); await page.locator('#acceso-telefono').waitFor();
    await page.locator('button[type=submit]').click();
    assert.equal(await page.locator('.campo-error').count(), 3); assert.equal(peticiones, 0);
    await page.locator('#acceso-usuario').fill('cliente.prueba'); await page.locator('#acceso-telefono').fill('50499998888');
    await page.locator('#acceso-contrasena').fill('corta'); await page.locator('button[type=submit]').click();
    assert.ok((await page.locator('#contrasena-error').textContent()).includes('12 y 128')); assert.equal(peticiones, 0);
    await page.locator('.acceso-cambiar button').scrollIntoViewIfNeeded();
    const rect = await page.locator('.acceso-cambiar button').boundingBox(); assert.ok(rect.y >= 0 && rect.y + rect.height <= viewport.height + 1);
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth), viewport.width);
    await page.screenshot({ path:`${destino}/registro-validado-${viewport.width}-${viewport.height}.png` });
    await page.locator('#acceso-contrasena').fill('DatoDePrueba_123!'); await page.locator('button[type=submit]').click();
    await page.waitForURL('**/mi-cuenta'); assert.equal(peticiones, 1); await page.close();
  }
  console.log('OK registro, teléfono normalizado y controles alcanzables en ventanas bajas');
} finally {
  writeFileSync(`${destino}/resultados.json`, JSON.stringify(resultados, null, 2));
  await browser.close();
}
