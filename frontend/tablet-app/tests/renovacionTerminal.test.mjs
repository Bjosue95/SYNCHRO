import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import {
  comprobarSesionTerminalReservaWeb,
  identificarDepositoReservaWeb,
  SesionTerminalNoAutorizada
} from "../src/services/reservasWebTerminalApi.js";

const leer=(p)=>readFileSync(new URL("../src/"+p,import.meta.url),"utf8");

test("401 exige una autorización administrativa real y no valida el PIN",async()=>{
  const anterior=globalThis.fetch;
  try{
    globalThis.fetch=async()=>new Response(null,{status:401});
    await assert.rejects(comprobarSesionTerminalReservaWeb(),e=>e instanceof SesionTerminalNoAutorizada);
    await assert.rejects(identificarDepositoReservaWeb("123456"),e=>e instanceof SesionTerminalNoAutorizada);
  }finally{globalThis.fetch=anterior;}
});

test("un PIN rechazado con 403 no se presenta como sesión vencida",async()=>{
  const anterior=globalThis.fetch;
  try{
    globalThis.fetch=async(url)=>{
      if(String(url).endsWith("/csrf-token"))
        return new Response(JSON.stringify({csrfToken:"test"}),{status:200,headers:{"Content-Type":"application/json"}});
      return new Response(JSON.stringify({codigo:"PinNoValido",mensaje:"No se pudo validar el PIN de depósito."}),{
        status:403,headers:{"Content-Type":"application/json"}
      });
    };
    await assert.rejects(identificarDepositoReservaWeb("123456"),
      e=>e.message==="No se pudo validar el PIN de depósito." && !(e instanceof SesionTerminalNoAutorizada));
  }finally{globalThis.fetch=anterior;}
});

test("renovar sesión requiere solicitud, código del admin y confirmación de un token secreto",()=>{
 const router=leer("router/index.js");
 const elegir=leer("views/ReservaWebElegirView.vue");
 const pantalla=leer("views/AutorizarTerminalView.vue");
 assert.match(router,/path: "\/terminal\/vincular"/);
 assert.match(elegir,/to="\/terminal\/vincular"/);
 assert.match(elegir,/await comprobarSesionTerminalReservaWeb\(\)/);
 assert.match(pantalla,/\/api\/terminales\/vinculacion\/solicitar/);
 assert.match(pantalla,/\/api\/terminales\/vinculacion\/confirmar/);
 assert.match(pantalla,/Comprobar aprobación/);
 assert.match(pantalla,/tokenTemporal=null/);
 assert.doesNotMatch(pantalla,/localStorage|sessionStorage/);
});
