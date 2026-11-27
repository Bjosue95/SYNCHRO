import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {iniciarRetiro,limpiarRetiro,registrarCaptura,obtenerRetiro,registrarOrdenRetiroPagado,obtenerOrdenRetiroPagado} from "../src/services/retiroSesion.js";

const orden={cobroId:"orden-pruebas",codigoCasillero:"M03",importeHnl:15,estadoPago:"Pendiente"};
const leer=p=>readFileSync(new URL("../src/"+p,import.meta.url),"utf8").replace(/\r\n/g,"\n");
test("limpieza de una operación anterior no elimina la orden actual",()=>{
  iniciarRetiro();
  registrarCaptura("rostro-prueba");
  registrarOrdenRetiroPagado("autorizacion-nueva",orden);
  limpiarRetiro("autorizacion-anterior");
  assert.equal(obtenerOrdenRetiroPagado().autorizacionPago,"autorizacion-nueva");
  assert.equal(obtenerRetiro().capturaHecha,false);
  limpiarRetiro("autorizacion-nueva");
  assert.equal(obtenerOrdenRetiroPagado(),null);
});
test("el PIN conserva autorización de reintento hasta completar la navegación",()=>{
  const pin=leer("views/RetiroPinView.vue");
  assert.match(pin,/const navegacion = await router\.replace\("\/retirar\/pago"\)/);
  assert.match(pin,/if \(navegacion \|\| router\.currentRoute\.value\.path !== "\/retirar\/pago"\)\s+throw new Error[^\n]+\n\s+autorizacionPagoPendiente\.value = null/);
  const router=leer("router/index.js");
  assert.match(router,/from\.path === "\/retirar\/pin" && to\.path === "\/retirar\/pago" && !obtenerOrdenRetiroPagado\(\)\) return false/);
  const selector=leer("views/SeleccionMetodoPagoView.vue");
  assert.doesNotMatch(selector,/router\.replace\(esCarga \? "\/retirar\/dispositivo" : "\/retirar\/rostro"\)/);
  assert.match(selector,/Repetir verificación/);
});
test("todos los módulos de retiro personal usan la misma ruta explícita",()=>{
  for(const p of ["RetiroPinView","RetirarView","VerificacionFacialView","SeleccionMetodoPagoView","RetiroPagoView"]){
    assert.match(leer("views/"+p+".vue"),/from "\.\.\/services\/retiroSesion\.js"/);
  }
  assert.match(leer("services/retiroSesion.js"),/import\.meta\.hot\.data\.estadoRetiro \?\?= crearEstadoRetiro\(\)/);
});
