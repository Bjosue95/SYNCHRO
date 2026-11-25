// Prueba de interfaz con contratos controlados; no recibe ni envía WhatsApp.
// La persistencia y las condiciones de seguridad se prueban además con SQL real.
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { resolve, extname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';

const raiz = fileURLToPath(new URL('../../../../', import.meta.url));
async function servir(carpeta) {
  const tipos = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.svg': 'image/svg+xml', '.png': 'image/png' };
  const server = createServer(async (req, res) => {
    const ruta = new URL(req.url, 'http://localhost').pathname;
    const archivo = resolve(carpeta, '.' + ruta);
    if (!archivo.startsWith(carpeta)) { res.writeHead(403).end(); return; }
    try { res.setHeader('Content-Type', tipos[extname(archivo)] || 'application/octet-stream'); res.end(await readFile(archivo)); }
    catch { res.setHeader('Content-Type', 'text/html'); res.end(await readFile(resolve(carpeta, 'index.html'))); }
  });
  await new Promise(ok => server.listen(0, '127.0.0.1', ok));
  return { server, url: `http://127.0.0.1:${server.address().port}` };
}
const web = await servir(resolve(raiz, 'frontend/usuarios-app/dist'));
const admin = await servir(resolve(raiz, 'frontend/admin-app/dist'));
let browser;
const cambios = [], errores = [];
const telefono = '+999123456701', destinatario = '+999123456702';
const enlace = 'https://wa.me/999123456700?text=SYNCHRO%20ACEPTO%2081';
const mensaje = `Tienes una encomienda SYNCHRO #81. Acepta desde tu propio WhatsApp: ${enlace}`;
const participante = (numero, remitente) => ({ personaId: remitente ? 1 : 2, telefono: numero, consentimiento: 'Pendiente', aceptaEncomienda: remitente,
  alcance: remitente ? 'CuentaTelefono' : 'Encomienda', codigo: remitente ? null : '81', texto: remitente ? 'SYNCHRO ACEPTO' : 'SYNCHRO ACEPTO 81', enlaceWhatsApp: enlace });
const detalle = { id: 81, estado: 'Preparacion', tamano: 'Mediano', venceUtc: '2099-01-01T00:00:00Z',
  version:1,creadaUtc:'2026-11-24T14:00:00Z',seguimiento:[{tipo:'Creada',descripcion:'Encomienda creada',fechaUtc:'2026-11-24T14:00:00Z'}],
  remitente: participante(telefono, true), destinatario: participante(destinatario, false),
  compartir: { mensaje, enlaceWhatsApp: `https://wa.me/${destinatario.slice(1)}?text=${encodeURIComponent(mensaje)}` },
  pago: { estado: 'ConfirmadoManual', importeHnl: 22, moneda: 'HNL' }, facial: { vigente: true }, pines: [], evidencias: [], historial: [],
  condiciones: { consentimientosConfirmados: false, pagoConfirmado: true } };
const cancelable=structuredClone(detalle);cancelable.id=82;
let nueva;
const catalogo={opciones:['Normal','Refrigerado','Caliente'].map((tipo,i)=>({codigo:'TEST'+i,tipo,tamano:'Mediano',ubicacionId:'prototipo',ubicacion:'Inventario de pruebas',duraciones:[30,60],tarifas:{30:22,60:40}})),ubicacionPendiente:false};
const personal={intentoId:'00000000-0000-0000-0000-000000000017',codigoAutorizacion:'ABCDEF0123456789',codigoCasillero:'T01',estado:'Pendiente',creadoUtc:detalle.creadaUtc,venceUtc:detalle.venceUtc,telefonoCliente:null,usoId:17};
function bandeja(servicio){
  const rows=['remitente','destinatario'].map(rol=>({servicio:'Encomiendas',operacionId:'81',encomiendaId:81,rol:rol==='remitente'?'Remitente':'Destinatario',persona:rol,telefono:detalle[rol].telefono,consentimiento:detalle[rol].consentimiento,referencia:detalle[rol].texto,finalidad:rol==='remitente'?'Deposito':'Retiro',emision:'NoEmitido',entrega:'Pendiente',estado:detalle.estado,creadoUtc:detalle.creadaUtc,pagoEstado:detalle.pago.estado}));
  rows.push({servicio:'ResguardoPersonal',operacionId:personal.intentoId,intentoId:personal.intentoId,usoId:17,rol:'Titular',persona:'Cliente de tablet',telefono:personal.telefonoCliente?`+${personal.telefonoCliente}`:null,consentimiento:personal.estado==='Pendiente'?'Pendiente':'Confirmado',referencia:personal.codigoAutorizacion,finalidad:'RetiroPersonal',emision:personal.estado==='Pendiente'?'NoEmitido':'Emitido',entrega:personal.estado==='Enviado'?'EnviadoManual':'Pendiente',estado:personal.estado,creadoUtc:personal.creadoUtc});
  return rows.filter(row=>!servicio||row.servicio===servicio);
}
try {
  browser = await chromium.launch({ executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true });
  const context = await browser.newContext({ reducedMotion: 'reduce' });
  await context.addInitScript(() => Object.defineProperty(navigator, 'clipboard', { value: { writeText: async texto => { window.__textoCopiado = texto; } } }));
  await context.route('**/api/**', async route => {
    const req = route.request(), url=new URL(req.url()),ruta = url.pathname;
    let datos;
    if (req.method() === 'POST') {
      const body = req.postDataJSON(); cambios.push({ ruta, body });
      if(ruta==='/api/web/reservas'){
        assert.equal(body.ubicacionId,'prototipo');assert.equal(body.minutos,60);assert.equal(body.tamanoSolicitado,'Mediano');assert.doesNotMatch(req.postData(),/descriptor|imagenPng|contrasena/i);
        nueva={...structuredClone(detalle),id:83,estado:'Preparacion',facial:{vigente:false},reserva:{servicio:body.servicio,tipoCasillero:body.tipoCasillero,minutos:body.minutos,ubicacion:'Inventario de pruebas'},pines:[],compartir:body.servicio==='Personal'?{}:detalle.compartir};datos={id:83};
      }
      if(ruta==='/api/web/reservas/82/cancelar'){
        cancelable.estado='Cancelada';cancelable.compartir={mensaje:null,enlaceWhatsApp:null};cancelable.cancelacion={motivo:body.motivo,fechaUtc:'2026-11-24T16:00:00Z',responsableId:1};cancelable.pagoRequiereRevision=true;
        cancelable.seguimiento.push({tipo:'Cancelada',descripcion:'Cancelada por el remitente',fechaUtc:'2026-11-24T16:00:00Z'});
      }
      if(ruta==='/api/admin/whatsapp-manual/confirmar'){
        assert.equal(body.codigoAutorizacion,personal.codigoAutorizacion);assert.equal(body.textoMensaje,`SYNCHRO ACEPTO ${personal.codigoAutorizacion} RECIBIR PIN DE RETIRO POR WHATSAPP`);assert.equal(body.mensajeComprobado,true);
        personal.estado='PinEmitido';personal.telefonoCliente=body.telefonoCliente;
        datos={pinRetiro:'876543',telefonoCliente:personal.telefonoCliente,usoId:17,codigoCasillero:'T01'};
      }
      if(ruta==='/api/admin/whatsapp-manual/marcar-enviado'){
        assert.equal(body.telefonoCliente,personal.telefonoCliente);assert.equal(body.envioComprobado,true);personal.estado='Enviado';
      }
      const rol = ruta.match(/consentimientos\/(remitente|destinatario)\/confirmar/)?.[1];
      if (rol) { detalle[rol].consentimiento = 'Confirmado'; detalle[rol].aceptaEncomienda = true; }
      if (ruta.endsWith('/pines/deposito/emitir') || ruta.endsWith('/pines/retiro/emitir')) {
        const retiro = ruta.endsWith('/retiro/emitir');
        datos = { id: retiro ? '00000000-0000-0000-0000-000000000002' : '00000000-0000-0000-0000-000000000001',
          finalidad: retiro ? 'Retiro' : 'Deposito', telefono: retiro ? destinatario : telefono, pin: retiro ? '654321' : '123456', venceUtc: detalle.venceUtc };
      }
      detalle.condiciones.consentimientosConfirmados = detalle.remitente.consentimiento === 'Confirmado' && detalle.destinatario.consentimiento === 'Confirmado';
      datos ??= { estado: 'Confirmado' };
    } else if (ruta.endsWith('/csrf-token')) datos = { csrfToken: 'CSRF-FIXTURE' };
    else if (ruta === '/api/web/clientes/mi-sesion') datos = { personaId: 1, nombreUsuario: 'fixture_web', telefono };
    else if (ruta === '/api/admin/mi-sesion') datos = { roles: ['Administrador'], nombreUsuario: 'fixture_admin' };
    else if (ruta.endsWith('/configuracion')) datos = {};
    else if(ruta==='/api/web/encomiendas/consentimiento-cuenta')datos={telefono,consentimiento:'Confirmado'};
    else if(ruta==='/api/web/reservas/disponibilidad')datos=catalogo;
    else if(ruta==='/api/web/reservas'){
      const pagina=Number(url.searchParams.get('pagina')||1),estado=url.searchParams.get('estado');
      const total=estado==='Cancelada'?1:41;
      datos={encomiendas:Array.from({length:estado==='Cancelada'?1:pagina===3?1:20},(_,i)=>({id:estado==='Cancelada'?82:121-(pagina-1)*20-i,tamano:'Mediano',telefonoDestinatario:destinatario,estado:estado||'Preparacion',creadaUtc:detalle.creadaUtc,pagoEstado:'Pendiente',retiradaConfirmada:false,cancelacion:estado==='Cancelada'?cancelable.cancelacion:null})),total,pagina,totalPaginas:Math.ceil(total/20),resumen:{preparacion:40,depositadas:1,entregadas:0}};
    }
    else if(ruta.endsWith('/82'))datos=cancelable;
    else if(ruta.endsWith('/83'))datos=nueva;
    else if (ruta.endsWith('/81')) datos = detalle;
    else if (ruta === '/api/admin/encomiendas/operativas') datos = { encomiendas: [{ id: 81, estado: detalle.estado, tamano: 'Mediano', pagoEstado: detalle.pago.estado }],total:1,totalPaginas:1 };
    else if(ruta==='/api/admin/whatsapp-pin/solicitudes'){const filas=bandeja(url.searchParams.get('servicio'));datos={solicitudes:filas,total:filas.length,totalPaginas:1};}
    else if(ruta==='/api/admin/whatsapp-manual/pendientes')datos={operaciones:[personal]};
    else throw new Error('Contrato no previsto: ' + ruta);
    await route.fulfill({ json: datos });
  });
  const pagina = await context.newPage(), panel = await context.newPage();
  for (const p of [pagina, panel]) p.on('pageerror', e => errores.push(e.message));
  await pagina.goto(web.url + '/encomiendas/81');
  const compartir = pagina.getByRole('link', { name: 'Compartir con destinatario', exact: true });
  await compartir.waitFor();
  const href = new URL(await compartir.getAttribute('href'));
  assert.equal(href.pathname, '/999123456702');
  assert.equal(href.searchParams.get('text'), mensaje);
  assert.ok(href.searchParams.get('text').includes(enlace));
  await pagina.getByRole('button', { name: 'Copiar mensaje', exact: true }).click();
  assert.equal(await pagina.evaluate(() => window.__textoCopiado), mensaje);
  await compartir.evaluate(el => el.addEventListener('click', e => e.preventDefault()));
  await compartir.click();
  assert.equal(cambios.length, 0, 'Abrir/copiar/compartir no confirma consentimiento');
  await panel.goto(admin.url + '/admin/encomiendas');
  await panel.getByRole('button', { name: 'Abrir encomienda 81', exact: true }).click();
  assert.equal(await panel.getByRole('button',{name:'Confirmar consentimiento',exact:true}).count(),0,'La ficha delega las comunicaciones al centro existente');
  await panel.getByRole('link',{name:'Gestionar WhatsApp y PIN de esta encomienda',exact:true}).click();
  const remitente = panel.locator('.enc-participants>.enc-card').nth(0);
  const destino = panel.locator('.enc-participants>.enc-card').nth(1);
  assert.equal(await remitente.getByLabel('Número de encomienda del mensaje recibido').count(), 0);
  for (const [seccion, rol, numero] of [[remitente, 'remitente', telefono], [destino, 'destinatario', destinatario]]) {
    await seccion.getByLabel('Número desde el que se recibió').fill(numero);
    await seccion.getByLabel('Texto recibido en WhatsApp').fill(detalle[rol].texto);
    await seccion.getByLabel('Referencia comprobable del mensaje').fill('WA-FIXTURE-' + rol);
    await seccion.getByLabel('He comprobado el mensaje').check();
    if (rol === 'destinatario') {
      await seccion.getByLabel('Número de encomienda del mensaje recibido').fill('81');
      await seccion.getByLabel('El destinatario aceptó').check();
    }
    await seccion.getByRole('button', { name: 'Confirmar consentimiento', exact: true }).click();
    await seccion.locator('form').waitFor({ state: 'detached' });
  }
  assert.equal(cambios[0].body.textoMensaje, 'SYNCHRO ACEPTO');
  assert.equal(cambios[0].body.codigo, '');
  assert.equal(cambios[1].body.textoMensaje, 'SYNCHRO ACEPTO 81');
  assert.equal(await panel.getByRole('button', { name: 'Emitir PIN de retiro', exact: true }).isDisabled(), true);
  await panel.getByRole('button', { name: 'Emitir PIN de depósito', exact: true }).click();
  const chat = panel.getByRole('link', { name: 'Abrir WhatsApp para entrega manual' });
  await chat.waitFor();
  assert.equal(await chat.getAttribute('href'), 'https://wa.me/999123456701');
  await panel.getByRole('button', { name: 'Copiar mensaje del PIN', exact: true }).click();
  assert.match(await panel.evaluate(() => window.__textoCopiado), /PIN exclusivo de deposito: 123456/);
  await panel.getByLabel('Referencia del mensaje enviado').fill('WA-ENVIO-FIXTURE');
  await panel.getByLabel('Comprobé la entrega manual').check();
  await panel.getByLabel('Número al que se entregó').fill(destinatario);
  assert.equal(await panel.getByRole('button', { name: 'Registrar entrega del PIN', exact: true }).isDisabled(), true);
  await panel.getByLabel('Número al que se entregó').fill(telefono);
  await panel.evaluate(() => {
    Object.defineProperty(document, 'hidden', { configurable: true, get: () => true });
    document.dispatchEvent(new Event('visibilitychange')); delete document.hidden;
  });
  await panel.locator('.enc-pin-value').waitFor({ state: 'detached' });
  assert.equal(await chat.count(), 0);
  await panel.getByRole('button', { name: 'Registrar entrega del PIN', exact: true }).click();
  await panel.locator('.enc-pin').waitFor({ state: 'detached' });
  assert.equal(cambios.at(-1).body.telefono, telefono);
  detalle.estado = 'Depositada';
  await panel.reload();
  await panel.getByRole('button', { name: 'Emitir PIN de retiro', exact: true }).click();
  await chat.waitFor();
  assert.equal(await chat.getAttribute('href'), 'https://wa.me/999123456702');
  await panel.getByRole('button', { name: 'Copiar mensaje del PIN', exact: true }).click();
  assert.match(await panel.evaluate(() => window.__textoCopiado), /PIN exclusivo de retiro: 654321/);
  await panel.getByRole('button',{name:'Resguardo personal (tablet)',exact:true}).click();
  await panel.getByRole('button',{name:'Abrir operación de resguardo',exact:true}).click();
  const personalFicha=panel.locator('.manual-admin');
  assert.equal(await personalFicha.getByRole('button',{name:'Confirmar mensaje y generar PIN',exact:true}).isDisabled(),true);
  await personalFicha.getByLabel('Teléfono del cliente').fill('50400000001');
  await personalFicha.getByLabel('Código del mensaje de resguardo recibido').fill(personal.codigoAutorizacion);
  await personalFicha.getByLabel('Texto recibido de resguardo personal').fill(`SYNCHRO ACEPTO ${personal.codigoAutorizacion} RECIBIR PIN DE RETIRO POR WHATSAPP`);
  await personalFicha.getByLabel('Referencia del mensaje de aceptación').fill('WA-PERSONAL-TEST');
  await personalFicha.getByLabel('He cotejado en WhatsApp').check();
  await personalFicha.getByRole('button',{name:'Confirmar mensaje y generar PIN',exact:true}).click();
  const chatPersonal=personalFicha.getByRole('link',{name:'Abrir chat de resguardo personal',exact:true});await chatPersonal.waitFor();
  assert.equal(await chatPersonal.getAttribute('href'),'https://wa.me/50400000001');
  const antesCopiar=cambios.length;await personalFicha.getByRole('button',{name:'Copiar mensaje completo',exact:true}).click();assert.equal(cambios.length,antesCopiar);
  await personalFicha.getByLabel('Referencia del envío del PIN personal').fill('WA-ENVIO-PERSONAL');
  await personalFicha.getByLabel('Comprobé que envié el PIN de retiro personal').check();
  await personalFicha.getByLabel('Teléfono al que envié el PIN personal').fill('50400000002');
  assert.equal(await personalFicha.getByRole('button',{name:'Ya envié el PIN por WhatsApp',exact:true}).isDisabled(),true);
  await personalFicha.getByLabel('Teléfono al que envié el PIN personal').fill('50400000001');
  await personalFicha.getByRole('button',{name:'Ya envié el PIN por WhatsApp',exact:true}).click();
  await personalFicha.getByText('Envío manual declarado. Esperando que el cliente termine el depósito en la tablet.',{exact:true}).waitFor();
  assert.equal(cambios.at(-1).ruta,'/api/admin/whatsapp-manual/marcar-enviado');
  await pagina.goto(web.url+'/encomiendas/82');
  const antesCancelar=cambios.length;await pagina.getByRole('button',{name:'Cancelar reserva',exact:true}).click();assert.equal(cambios.length,antesCancelar);
  await pagina.getByLabel('Motivo de cancelación').selectOption({label:'Número de destinatario incorrecto'});
  const botonCancelar=pagina.getByRole('button',{name:'Confirmar cancelación de reserva',exact:true});assert.equal(await botonCancelar.isDisabled(),true);
  await pagina.getByLabel('Confirmo que deseo cancelar esta reserva.').check();await botonCancelar.click();
  await pagina.getByRole('heading',{name:'Reserva cancelada',exact:true}).waitFor();assert.equal(await pagina.getByRole('link',{name:'Compartir con destinatario',exact:true}).count(),0);
  await pagina.getByText('El pago confirmado está pendiente de revisión administrativa. No se ha realizado un reembolso.',{exact:true}).waitFor();
  await pagina.goto(web.url+'/mi-cuenta');
  await pagina.getByText('Página 1 de 3 · 41 reservas',{exact:true}).waitFor();await pagina.getByRole('button',{name:'Siguiente',exact:true}).click();await pagina.getByText('Página 2 de 3 · 41 reservas',{exact:true}).waitFor();
  await pagina.getByLabel('Estado',{exact:true}).selectOption('Cancelada');await pagina.getByRole('button',{name:'Buscar reservas',exact:true}).click();
  await pagina.getByText('Página 1 de 1 · 1 reservas',{exact:true}).waitFor();assert.equal(await pagina.locator('.reserva-tarjeta').count(),1);
  await panel.getByRole('button',{name:'Todos',exact:true}).click();
  // Selección web real en los seis casos, sin cámara, registro facial ni peticiones de identidad.
  for(const servicio of ['Personal','Encomienda'])for(const tipo of ['Normal','Refrigerado','Caliente']){
    await pagina.setViewportSize({width:390,height:900});await pagina.goto(web.url+'/reservar');
    await pagina.getByRole('button',{name:/^Pertenencias personales/}).waitFor();
    if(servicio==='Encomienda')await pagina.getByRole('button',{name:/^Una encomienda/}).click();
    await pagina.getByRole('button',{name:new RegExp('^'+tipo)}).click();
    await pagina.getByRole('button',{name:/^Mediano/}).click();
    await pagina.getByLabel('Duración').selectOption('60');
    if(servicio==='Encomienda')await pagina.getByLabel('WhatsApp del destinatario').fill(destinatario);
    else assert.equal(await pagina.getByLabel('WhatsApp del destinatario').count(),0);
    await pagina.getByRole('button',{name:'Revisar reserva',exact:true}).click();
    assert.equal(await pagina.locator('video,input[type=file]').count(),0);
    const antes=cambios.length;
    await pagina.getByRole('button',{name:'Confirmar reserva',exact:true}).click();
    await pagina.getByRole('heading',{name:'Reserva #83',exact:true}).waitFor();
    assert.equal(cambios.length,antes+1);assert.equal(cambios.at(-1).body.servicio,servicio);assert.equal(cambios.at(-1).body.tipoCasillero,tipo);
    if(servicio==='Personal'){assert.equal(cambios.at(-1).body.telefonoDestinatario,null);assert.equal(await pagina.getByRole('heading',{name:'Destinatario',exact:true}).count(),0);}
  }
  await pagina.goto(web.url+'/mi-perfil');await pagina.getByRole('heading',{name:'Mi perfil',exact:true}).waitFor();
  assert.equal(await pagina.locator('.sidebar-navegacion a').count(),3);
  assert.deepEqual(await pagina.locator('.sidebar-navegacion a').allTextContents().then(textos=>textos.map(t=>t.trim())),['Reservar casillero','Mis reservas','Mi perfil']);
  await pagina.goto(web.url+'/reservar');await pagina.getByRole('button',{name:/^Normal/}).waitFor();
  const superficies=new Map();
  for (const width of [1440, 390]) {
    for(const tema of ['claro','oscuro'])for (const p of [pagina, panel]) {
      await p.bringToFront();
      await p.emulateMedia({colorScheme:tema==='oscuro'?'dark':'light'});await p.evaluate(tema=>document.documentElement.dataset.theme=tema,tema);
      await p.setViewportSize({ width, height: 900 });
      assert.equal(await p.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), true,`${width}/${tema}`);
      await p.evaluate(()=>new Promise(ok=>requestAnimationFrame(()=>requestAnimationFrame(ok))));
      const selector=p===panel?'.wa-card':'.env-card';
      const clave=`${width}/${p===panel?'admin':'web'}`;
      if(tema==='claro')superficies.set(clave,await p.locator(selector).first().evaluate(el=>getComputedStyle(el).backgroundColor));
      else await p.waitForFunction(({selector,anterior})=>document.documentElement.dataset.theme==='oscuro'&&getComputedStyle(document.querySelector(selector)).backgroundColor!==anterior,{selector,anterior:superficies.get(clave)},{timeout:3000});
    }
  }
  for(const width of [1440,390])for(const tema of ['claro','oscuro'])for(const [ruta,titulo] of [['/mis-reservas','Mis reservas'],['/mi-perfil','Mi perfil'],['/reservas/83','Reserva #83']]){
    await pagina.bringToFront();await pagina.setViewportSize({width,height:900});await pagina.goto(web.url+ruta);
    await pagina.getByRole('heading',{name:titulo,exact:true}).waitFor();
    await pagina.evaluate(t=>document.documentElement.dataset.theme=t,tema);
    assert.equal(await pagina.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true,`${ruta}/${width}/${tema}`);
    assert.equal(await pagina.locator('video').count(),0);
  }
  assert.deepEqual(errores, []);
  console.log('Interfaz OK: seis selecciones de servicio/tipo, tres apartados, sin cámara web, cancelación, historial, centro de WhatsApp en tarjetas, consentimiento y PIN separados, contacto correcto y pantallas 1440/390 en claro/oscuro. Sin envíos reales de WhatsApp.');
} finally {
  await browser?.close();
  web.server.close(); admin.server.close();
}
