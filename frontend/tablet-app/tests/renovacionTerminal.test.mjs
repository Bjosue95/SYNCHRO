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

test("la tablet definitiva recupera sola la sesion de trabajo mediante vínculo aprobado",async()=>{
  const anterior=globalThis.fetch;
  const rutas=[];
  let autorizada=false;
  try{
    globalThis.fetch=async(url,opciones={})=>{
      const ruta=String(url);rutas.push(ruta);
      if(ruta==="/api/terminales/mi-sesion")
        return autorizada
          ? new Response(JSON.stringify({terminalId:"1",rol:"Terminal"}),{status:200})
          : new Response(null,{status:401});
      if(ruta==="/api/terminales/dispositivo/csrf-token")
        return new Response(JSON.stringify({csrfToken:"csrf-test"}),{status:200});
      if(ruta==="/api/terminales/sesion/renovar"){
        assert.equal(opciones.method,"POST");
        assert.equal(opciones.headers["X-CSRF-TOKEN"],"csrf-test");
        autorizada=true;
        return new Response(JSON.stringify({terminalId:1}),{status:200});
      }
      throw new Error("Ruta inesperada "+ruta);
    };
    const resultado=await comprobarSesionTerminalReservaWeb();
    assert.equal(resultado.terminalId,"1");
    assert.deepEqual(rutas,[
      "/api/terminales/mi-sesion",
      "/api/terminales/dispositivo/csrf-token",
      "/api/terminales/sesion/renovar",
      "/api/terminales/mi-sesion"
    ]);
  }finally{globalThis.fetch=anterior;}
});

test("el deposito reintenta csrf tras renovar sin duplicar peticiones de PIN",async()=>{
  const anterior=globalThis.fetch;
  let tokenDisponible=false,identificaciones=0;
  try{
    globalThis.fetch=async(url,opciones={})=>{
      const ruta=String(url);
      if(ruta==="/api/terminales/dispositivo/csrf-token")
        return new Response(JSON.stringify({csrfToken:"csrf-renovacion"}),{status:200});
      if(ruta==="/api/terminales/sesion/renovar"){
        tokenDisponible=true;
        return new Response(JSON.stringify({terminalId:1}),{status:200});
      }
      if(ruta==="/api/terminales/reservas/csrf-token")
        return tokenDisponible
          ? new Response(JSON.stringify({csrfToken:"csrf-operacion"}),{status:200})
          : new Response(null,{status:401});
      if(ruta==="/api/terminales/reservas/depositos/identificar"){
        identificaciones++;
        assert.equal(opciones.headers["X-CSRF-TOKEN"],"csrf-operacion");
        return new Response(JSON.stringify({reservaId:5012,casillero:"M03"}),{status:200});
      }
      throw new Error("Ruta inesperada "+ruta);
    };
    const datos=await identificarDepositoReservaWeb("123456");
    assert.equal(datos.reservaId,5012);
    assert.equal(identificaciones,1);
  }finally{globalThis.fetch=anterior;}
});

test("la credencial de dispositivo no sustituye aprobacion ni valida PIN incorrecto",()=>{
  const backend=readFileSync(new URL("../../../backend/Synchro.Api/Endpoints/ConfirmacionTerminalEndpoints.cs",import.meta.url),"utf8");
  const config=readFileSync(new URL("../../../backend/Synchro.Api/Program.cs",import.meta.url),"utf8");
  assert.match(backend,/Estado=N'Aprobada'/);
  assert.match(backend,/new Claim\("VinculoVersion", vinculoVersion\)/);
  assert.match(backend,/SignInAsync\("TerminalDispositivo"/);
  assert.match(backend,/\/api\/terminales\/sesion\/renovar/);
  assert.match(backend,/IsRequestValidAsync\(contexto\)/);
  assert.match(config,/AddPolicy\("SoloTerminalDispositivo"/);
  assert.match(config,/\.AddCookie\("TerminalDispositivo"/);
  assert.match(config,/ExpireTimeSpan = TimeSpan\.FromDays\(365\)/);
  assert.match(config,/FechaAutorizacion FROM dbo\.Terminales/);
  assert.match(config,/context\.RejectPrincipal\(\)/);
});
