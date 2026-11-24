import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import ts from 'typescript';
import { encomiendasApi } from '../src/services/encomiendasApi.js';

const respuesta = (datos, status = 200) => new Response(JSON.stringify(datos), { status, headers: {'Content-Type':'application/json'} });
test('encomiendas reutiliza sesión y CSRF autenticado en creación, referencia y autorización', async () => {
  const original = globalThis.fetch, llamadas = [];
  globalThis.fetch = async (ruta, opciones) => {
    llamadas.push({ruta, opciones});
    return respuesta(ruta.endsWith('/csrf-token') ? {csrfToken:'csrf-cliente'} : {id:81});
  };
  const captura = {imagenPng:'PNG-FICTICIO-TEST', descriptor:Array(1024).fill(.1), modelo:'SYNCHRO-HUMANJS-EMBEDDING-1024-v3'};
  try {
    await encomiendasApi.crear({telefonoDestinatario:'+999123456702',tamanoSolicitado:'Mediano'});
    await encomiendasApi.registrarReferencia(captura,'CONTRASENA-FICTICIA','CONSENTIMIENTO-TEST');
    await encomiendasApi.autorizarDeposito(81,'SOLICITUD-TEST','123456',captura,'CONSENTIMIENTO-TEST');
    await encomiendasApi.revocarReferencia();
    const operaciones = llamadas.filter(x => !x.ruta.endsWith('/csrf-token'));
    assert.equal(operaciones.length,4);
    for (const {ruta, opciones} of operaciones) {
      assert.equal(opciones.headers['X-CSRF-TOKEN'],'csrf-cliente');
      assert.equal(opciones.credentials,'same-origin'); assert.equal(opciones.cache,'no-store');
      assert.doesNotMatch(ruta,/123456|CONTRASENA|descriptor|imagenPng/);
    }
    assert.equal(operaciones[1].opciones.headers['X-SYNCHRO-CONSENTIMIENTO'],'ACEPTO');
    assert.equal(JSON.parse(operaciones[2].opciones.body).pin,'123456');
    assert.equal(operaciones[3].opciones.method,'DELETE');
    assert.ok(llamadas.filter(x=>x.ruta.endsWith('/csrf-token')).every(x=>x.ruta==='/api/web/clientes/csrf-token'));
    assert.equal(captura.descriptor[0],.1,'El transporte no altera el original mientras el componente lo utiliza');
  } finally { globalThis.fetch = original; }
});
test('la plataforma propaga rechazos reales y no confirma una operación fallida', async () => {
  const original=globalThis.fetch;
  globalThis.fetch=async ruta=>ruta.endsWith('/csrf-token')?respuesta({csrfToken:'csrf'}):respuesta({mensaje:'El pago no está confirmado.'},409);
  try { await assert.rejects(encomiendasApi.crear({}), e=>e.estado===409&&e.message==='El pago no está confirmado.'); }
  finally {globalThis.fetch=original;}
});
test('el descriptor web conserva el contrato V3 de 1024 valores y rechaza datos inválidos', async () => {
  const codigo=ts.transpileModule(readFileSync('src/services/facialEncomiendaV3.ts','utf8'),{compilerOptions:{module:ts.ModuleKind.ESNext}}).outputText;
  const modulo=await import('data:text/javascript;base64,'+Buffer.from(codigo).toString('base64'));
  assert.equal(modulo.MODELO_FACIAL_V3,'SYNCHRO-HUMANJS-EMBEDDING-1024-v3');
  assert.equal(modulo.descriptorValido(Array(1024).fill(.1)),true);
  for(const d of [Array(128).fill(.1),Array(1024).fill(0),Array(1024).fill(NaN),Array(1024).fill(Infinity),null]) assert.equal(modulo.descriptorValido(d),false);
});
