import test from "node:test";
import assert from "node:assert/strict";
import {registrarFotoRetiroEncomienda,identificarEncomiendaConPin,elegirPagoEncomienda,prepararRecogidaEncomienda,limpiarRetiroEncomienda,hayFotoRetiroEncomienda} from "../src/services/retiroEncomiendaSesion.js";
import {tomarRecogidaRetiro,limpiarRecogidaPendiente} from "../src/services/recogidaRetiroSesion.js";

const base="/api/terminales/reservas";
const token="a".repeat(80);
function entorno(pagada){
  let confirmado=pagada;
  const llamadas=[];
  const anterior=globalThis.fetch;
  globalThis.fetch=async(url,opciones={})=>{
    llamadas.push({url,opciones});
    const datos=url.endsWith("/csrf-token")?{csrfToken:"prueba"}:
      url.endsWith("/retiros/identificar")?{encomiendaId:5012,casillero:"M04",pagoConfirmado:confirmado,pagoAdicionalRequerido:!confirmado,importeHnl:45,reserva:{minutos:120}}:
      url.endsWith("/pago/estado")?{reservaId:5012,casillero:"M04",pagoConfirmado:confirmado,pagoAdicionalRequerido:!confirmado,importeHnl:45}:
      url.endsWith("/pago/solicitar")?{pendiente:true,pagoRealProcesado:false}:
      url.endsWith("/captura-retiro")?{evidenciaId:"foto-prueba"}:
      url.endsWith("/autorizar-recogida")?{retiroAutorizado:true,retiroConfirmado:false,estadoCasillero:"Ocupado",casillero:"M04",autorizacionRetiro:token,venceUtc:new Date(Date.now()+120000).toISOString()}:
      url.endsWith("/confirmar-recogida")?{retiroConfirmado:true,casillero:"M04"}:null;
    if(!datos)throw new Error("Ruta inesperada: "+url);
    return new Response(JSON.stringify(datos),{status:200});
  };
  return {llamadas,confirmar:()=>{confirmado=true;},cerrar:()=>{globalThis.fetch=anterior;limpiarRetiroEncomienda();limpiarRecogidaPendiente();}};
}
test("encomienda pagada: foto y PIN, no se solicita pago, recogida no cierra antes de confirmar",async()=>{
  const e=entorno(true);
  try{
    registrarFotoRetiroEncomienda("QUJD");
    const orden=await identificarEncomiendaConPin("123456");
    assert.equal(orden.pagoConfirmado,true);
    await prepararRecogidaEncomienda();
    assert.equal(hayFotoRetiroEncomienda(),false);
    assert.equal(e.llamadas.some(x=>x.url.endsWith("/pago/solicitar")),false);
    assert.equal(e.llamadas.some(x=>x.url.endsWith("/confirmar-recogida")),false);
    const recogida=tomarRecogidaRetiro();
    assert.equal(recogida.tipo,"encomienda");
    assert.equal((await recogida.confirmar()).retiroConfirmado,true);
  }finally{e.cerrar();}
});
test("encomienda diferida: importe de reserva, espera confirmación antes de enviar foto y autorizar",async()=>{
  const e=entorno(false);
  try{
    registrarFotoRetiroEncomienda("QUJD");
    const orden=await identificarEncomiendaConPin("123456");
    assert.equal(orden.importeHnl,45);
    await assert.rejects(prepararRecogidaEncomienda(),/pago todavía/);
    assert.equal(e.llamadas.some(x=>x.url.endsWith("/captura-retiro")),false);
    await elegirPagoEncomienda("NFC");
    const peticion=e.llamadas.find(x=>x.url.endsWith("/pago/solicitar"));
    assert.deepEqual(JSON.parse(peticion.opciones.body),{pin:"123456",metodo:"NFC"});
    await assert.rejects(prepararRecogidaEncomienda(),/pago todavía/);
    e.confirmar();
    await prepararRecogidaEncomienda();
    const recogida=tomarRecogidaRetiro();
    assert.equal(recogida.codigoCasillero,"M04");
    assert.equal(e.llamadas.filter(x=>x.url.endsWith("/pago/solicitar")).length,1);
    assert.equal(e.llamadas.some(x=>x.url.endsWith("/confirmar-recogida")),false);
  }finally{e.cerrar();}
});
