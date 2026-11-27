import test from 'node:test';
import assert from 'node:assert/strict';
import { chatPinEncomienda, mensajePinEncomienda } from '../../admin-app/src/services/pinEncomiendaManual.js';
import { encomiendasApi } from '../src/services/encomiendasApi.js';

const detalle = { id: 81, estado: 'Preparacion', remitente: { telefono: '+999123456701', consentimiento: 'Confirmado' }, destinatario: { telefono: '+999123456702', consentimiento: 'Confirmado' } };
const deposito = { finalidad: 'Deposito', telefono: detalle.remitente.telefono, pin: '123456' };
const retiro = { finalidad: 'Retiro', telefono: detalle.destinatario.telefono, pin: '654321' };

test('el chat manual abre al participante correcto sin incorporar el PIN al enlace', () => {
  assert.equal(chatPinEncomienda(deposito, detalle), 'https://wa.me/999123456701');
  assert.equal(chatPinEncomienda(retiro, { ...detalle, estado: 'Depositada' }), 'https://wa.me/999123456702');
  assert.doesNotMatch(chatPinEncomienda(deposito, detalle), /123456$|\?text=|token|pin=/i);
  assert.match(mensajePinEncomienda(deposito, detalle, 'fecha de prueba'), /PIN exclusivo de depósito: 123456\./);
  assert.match(mensajePinEncomienda(retiro, { ...detalle, estado: 'Depositada' }, 'fecha de prueba'), /PIN exclusivo de retiro: 654321/);
});

test('bloquea PIN intercambiados, contactos equivocados, retiro anticipado y autorizaciones revocadas', () => {
  assert.equal(chatPinEncomienda({ ...deposito, telefono: retiro.telefono }, detalle), '');
  assert.equal(chatPinEncomienda({ ...retiro, telefono: deposito.telefono }, { ...detalle, estado: 'Depositada' }), '');
  assert.equal(chatPinEncomienda(retiro, detalle), '');
  assert.equal(chatPinEncomienda(deposito, { ...detalle, estado: 'Depositada' }), '');
  assert.equal(chatPinEncomienda({ ...deposito, finalidad: 'Otro' }, detalle), '');
  assert.equal(chatPinEncomienda(deposito, { ...detalle, remitente: { ...detalle.remitente, consentimiento: 'Revocado' } }), '');
  assert.equal(mensajePinEncomienda({ ...deposito, pin: '' }, detalle, 'fecha'), '');
});

test('la reserva personal utiliza un solo PIN para ambas etapas',()=>{
  const personal={...detalle,reserva:{servicio:'Personal'}};
  assert.equal(chatPinEncomienda(deposito,personal),'https://wa.me/999123456701');
  assert.equal(chatPinEncomienda({...retiro,telefono:telefonoTitular()},personal),'');
  assert.equal(chatPinEncomienda({...retiro,telefono:telefonoTitular()},{...personal,estado:'Depositada'}),'');
  assert.match(mensajePinEncomienda(deposito,personal,'fecha de prueba'),/PIN de depósito y retiro: 123456/);
  assert.equal(chatPinEncomienda(retiro,{...personal,estado:'Depositada'}),'');
  function telefonoTitular(){return personal.remitente.telefono;}
});

test('reservar consulta disponibilidad real y envía la selección sin datos faciales por CSRF',async()=>{
  const original=globalThis.fetch,llamadas=[];
  globalThis.fetch=async(ruta,opciones)=>{llamadas.push({ruta,opciones});return new Response(JSON.stringify(ruta.endsWith('/csrf-token')?{csrfToken:'csrf-reserva'}:{id:83}),{headers:{'Content-Type':'application/json'}});};
  try{
    await encomiendasApi.disponibilidad();assert.equal(llamadas[0].ruta,'/api/web/reservas/disponibilidad');assert.equal(llamadas[0].opciones.method,'GET');
    const seleccion={servicio:'Encomienda',tipoCasillero:'Refrigerado',tamanoSolicitado:'Mediano',ubicacionId:'prototipo',minutos:30,telefonoDestinatario:'+999123456702'};
    await encomiendasApi.reservar(seleccion);const ultima=llamadas.at(-1);
    assert.equal(ultima.ruta,'/api/web/reservas');assert.equal(ultima.opciones.method,'POST');assert.deepEqual(JSON.parse(ultima.opciones.body),seleccion);
    assert.equal(ultima.opciones.headers['X-CSRF-TOKEN'],'csrf-reserva');assert.equal(ultima.opciones.credentials,'same-origin');
    assert.doesNotMatch(ultima.opciones.body,/rostro|descriptor|imagen|pin|contrasena/i);
  }finally{globalThis.fetch=original;}
});

test('consultar autorización de cuenta no confirma y revocar usa CSRF autenticado', async () => {
  const original = globalThis.fetch, llamadas = [];
  globalThis.fetch = async (ruta, opciones) => {
    llamadas.push({ ruta, opciones });
    return new Response(JSON.stringify(ruta.endsWith('/csrf-token') ? { csrfToken: 'csrf-test' } : { consentimiento: 'Pendiente' }), { headers: { 'Content-Type': 'application/json' } });
  };
  try {
    await encomiendasApi.consentimientoCuenta();
    await encomiendasApi.revocarConsentimientoCuenta();
    const consultas = llamadas.filter(x => x.ruta.endsWith('/consentimiento-cuenta'));
    assert.equal(consultas.length, 2);
    assert.equal(consultas[0].opciones.method, 'GET');
    assert.equal(consultas[1].opciones.method, 'DELETE');
    assert.equal(consultas[1].opciones.headers['X-CSRF-TOKEN'], 'csrf-test');
    assert.equal(consultas[1].opciones.credentials, 'same-origin');
  } finally { globalThis.fetch = original; }
});

test('historial pide paginación al servidor y la cancelación envía confirmación y versión mediante CSRF', async()=>{
  const original=globalThis.fetch,llamadas=[];
  globalThis.fetch=async(ruta,opciones)=>{llamadas.push({ruta,opciones});return new Response(JSON.stringify(ruta.endsWith('/csrf-token')?{csrfToken:'csrf-cancelar'}:{cancelada:true}),{headers:{'Content-Type':'application/json'}});};
  try{
    await encomiendasApi.listar({pagina:12,tamanoPagina:20,estado:'Cancelada',desde:'2026-01-01',numero:81});
    const consulta=new URL(llamadas[0].ruta,'https://localhost');
    assert.equal(consulta.searchParams.get('pagina'),'12');assert.equal(consulta.searchParams.get('estado'),'Cancelada');assert.equal(consulta.searchParams.get('numero'),'81');
    await encomiendasApi.cancelar(81,{motivo:'Otro motivo',detalle:'Cambio de planes',confirmada:true,version:7});
    const ultima=llamadas.at(-1);assert.equal(ultima.ruta,'/api/web/reservas/81/cancelar');assert.equal(ultima.opciones.method,'POST');
    assert.equal(ultima.opciones.headers['X-CSRF-TOKEN'],'csrf-cancelar');assert.equal(JSON.parse(ultima.opciones.body).version,7);assert.doesNotMatch(ultima.ruta,/Cambio|planes|token|pin=/);
  }finally{globalThis.fetch=original;}
});
