import test from "node:test";
import assert from "node:assert/strict";
import {recuperarSesionTerminal,obtenerCsrfTerminal} from "../src/services/recuperarSesionTerminal.js";
const json=(datos,status=200)=>new Response(JSON.stringify(datos),{status});
test("renueva silenciosamente un dispositivo autorizado y comparte las peticiones",async()=>{
 const anterior=globalThis.fetch, llamadas=[];
 try{
  globalThis.fetch=async(url,op)=>{
   llamadas.push(url);
   if(url.endsWith("mi-sesion"))return json({},401);
   if(url.endsWith("dispositivo/csrf-token"))return json({csrfToken:"prueba"});
   assert.equal(op.headers["X-CSRF-TOKEN"],"prueba");
   return json({terminalId:960});
  };
  assert.deepEqual(await Promise.all([recuperarSesionTerminal(),recuperarSesionTerminal()]),[true,true]);
  assert.deepEqual(llamadas,["/api/terminales/mi-sesion","/api/terminales/dispositivo/csrf-token","/api/terminales/sesion/renovar"]);
 }finally{globalThis.fetch=anterior;}
});
test("no autoriza dispositivos nuevos ni solicita vinculación",async()=>{
 const anterior=globalThis.fetch, llamadas=[];
 try{
  globalThis.fetch=async url=>{llamadas.push(url);return json({},401);};
  assert.equal(await recuperarSesionTerminal(),false);
  assert.equal(llamadas.length,2);
  assert.ok(llamadas.every(x=>!x.includes("vinculacion")));
 }finally{globalThis.fetch=anterior;}
});
test("cuando la red falla la recuperación no interrumpe la pantalla",async()=>{
 const anterior=globalThis.fetch;
 try{globalThis.fetch=async()=>{throw new Error("offline");};assert.equal(await recuperarSesionTerminal(),false);}
 finally{globalThis.fetch=anterior;}
});
test("solo reintenta el GET de CSRF después de renovar",async()=>{
 const anterior=globalThis.fetch;let n=0;
 try{
  globalThis.fetch=async url=>{
   if(url==="/api/terminales/csrf-token")return ++n===1?json({},401):json({csrfToken:"nuevo"});
   if(url.endsWith("mi-sesion"))return json({},401);
   if(url.endsWith("dispositivo/csrf-token"))return json({csrfToken:"prueba"});
   return json({terminalId:960});
  };
  assert.equal((await obtenerCsrfTerminal("/api/terminales/csrf-token")).status,200);
  assert.equal(n,2);
 }finally{globalThis.fetch=anterior;}
});
