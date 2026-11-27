import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {registrarRecogidaRetiro,tomarRecogidaRetiro,limpiarRecogidaPendiente} from "../src/services/recogidaRetiroSesion.js";
const leer=p=>readFileSync(new URL("../src/"+p,import.meta.url),"utf8");
test("todos los servicios entregan la sesión común sin confirmar al navegar",async()=>{
  for(const [tipo,codigo,volver] of [["personal","M01","/retirar/rostro"],["encomienda","M04","/retirar/encomienda"],["carga","W1","/retirar/dispositivo"]]){
    let confirmaciones=0;
    let limpiezas=0;
    registrarRecogidaRetiro({tipo,codigo,volver,vence:Date.now()+60000,
      confirmar:async()=>{confirmaciones++;return {retiroConfirmado:true,codigoCasillero:codigo};},
      limpiar:()=>limpiezas++});
    const sesion=tomarRecogidaRetiro();
    assert.equal(sesion.codigoCasillero,codigo);
    assert.equal(sesion.tipo,tipo);
    assert.equal(confirmaciones,0);
    assert.equal(tomarRecogidaRetiro(),null);
    assert.equal((await sesion.confirmar()).retiroConfirmado,true);
    assert.equal(confirmaciones,1);
    sesion.limpiar();assert.equal(limpiezas,1);
  }
});
test("autorizaciones inválidas o vencidas no abren recogida",()=>{
  const base={tipo:"personal",codigo:"M03",volver:"/retirar/rostro",confirmar:()=>{}};
  assert.throws(()=>registrarRecogidaRetiro({...base,vence:Date.now()-1}));
  assert.throws(()=>registrarRecogidaRetiro({...base,tipo:"otro",vence:Date.now()+60000}));
  assert.equal(tomarRecogidaRetiro(),null);
  limpiarRecogidaPendiente();
});
test("misma animación, contador, botón de recogida y bienvenida para todos",()=>{
  for(const vista of ["RetiroPinView","RetiroPagoView","RetiroCargaPagoView","RetiroEncomiendaWebView"]){
    const codigo=leer("views/"+vista+".vue");
    assert.match(codigo,/registrarRecogidaRetiro/);
    assert.ok(codigo.includes('router.replace("/retirar/recoger")'));
  }
  const vista=leer("views/DepositoView.vue");
  for(const texto of ["Ya retiré mis pertenencias","Ya retiré mi encomienda","Ya retiré mi dispositivo","retiro-puerta__hoja","Agregar 1 minuto más","¡Gracias por utilizar SYNCHRO!"])assert.ok(vista.includes(texto));
  assert.ok(vista.includes("await actual.confirmar()"));
  assert.ok(vista.includes('router.replace("/")'));
  assert.ok(vista.includes("limiteMs=Math.min(limiteMs,recogida.value.vence)"));
});
