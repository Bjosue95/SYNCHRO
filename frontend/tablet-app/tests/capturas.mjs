import { chromium } from '@playwright/test';
import { existsSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
const destino=fileURLToPath(new URL('../../../docs/capturas/',import.meta.url));mkdirSync(destino,{recursive:true});
const browser=await chromium.launch({executablePath:process.env.SYNCHRO_CHROMIUM || (existsSync('/usr/bin/chromium')?'/usr/bin/chromium':undefined)});
try {
 const page=await browser.newPage({viewport:{width:1280,height:900},timezoneId:'America/Tegucigalpa',reducedMotion:'reduce'});
 async function captura(nombre){await page.evaluate(()=>document.fonts.ready);await page.screenshot({path:`${destino}/${nombre}.png`,fullPage:true});}
 await page.goto('http://127.0.0.1:4173/menu');await page.getByRole('heading',{name:'¿Qué deseas hacer?'}).waitFor();await captura('tablet-menu');
 await page.getByRole('button',{name:'Activar modo oscuro'}).click();await captura('tablet-menu-oscuro');
 await page.getByRole('button',{name:'Activar modo claro'}).click();await page.getByRole('link',{name:/Tengo una reserva/}).click();await page.getByRole('button',{name:'Rellenar datos de ejemplo'}).click();await page.getByRole('button',{name:'Continuar →',exact:true}).click();await page.getByRole('heading',{name:'Verifiquemos al remitente'}).waitFor();await captura('tablet-camara');
 await page.goto('http://127.0.0.1:4173/servicios/carga');await page.getByRole('button',{name:/C01/}).waitFor();await page.getByRole('button',{name:'60 min',exact:true}).click();await captura('tablet-carga');
 console.log(`Capturas guardadas en ${destino}`);
} finally {await browser.close();}
