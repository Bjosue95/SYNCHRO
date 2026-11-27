import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import {
  elegirServicioReservaWeb, obtenerEleccionReservaWeb,
  registrarFotoReservaWeb, hayFotoReservaWeb,
  iniciarReservaWebDeposito, obtenerReservaWebDeposito,
  limpiarReservaWebDeposito
} from "../src/services/reservaWebSesion.js";

const leer = p => readFileSync(new URL("../src/"+p,import.meta.url),"utf8");

test("reserva web: eleccion personal o encomienda y los tres tipos permitidos",()=>{
  elegirServicioReservaWeb("Personal");
  assert.deepEqual(obtenerEleccionReservaWeb(),{servicio:"Personal",tipoCasillero:"Normal"});
  assert.throws(()=>elegirServicioReservaWeb("Personal","Refrigerado"));
  for(const tipo of ["Normal","Refrigerado","Caliente"]){
    elegirServicioReservaWeb("Encomienda",tipo);
    assert.equal(obtenerEleccionReservaWeb()?.tipoCasillero,tipo);
  }
  assert.throws(()=>elegirServicioReservaWeb("Encomienda","Invalido"));
  limpiarReservaWebDeposito();
});

test("PIN real se valida tras fotografía, y ambos solo viven en memoria",()=>{
  elegirServicioReservaWeb("Personal");
  assert.equal(hayFotoReservaWeb(),false);
  assert.throws(()=>iniciarReservaWebDeposito({reservaId:5012,casillero:"M03",servicio:"Personal",tipoCasillero:"Normal"},"123456"));
  registrarFotoReservaWeb("QUJD");
  assert.equal(hayFotoReservaWeb(),true);
  assert.throws(()=>iniciarReservaWebDeposito({reservaId:5012,casillero:"M03",servicio:"Encomienda",tipoCasillero:"Normal"},"123456"));
  iniciarReservaWebDeposito({reservaId:5012,casillero:"M03",servicio:"Personal",tipoCasillero:"Normal"},"123456");
  assert.equal(obtenerReservaWebDeposito()?.reservaId,5012);
  assert.equal(obtenerReservaWebDeposito()?.foto,"QUJD");
  limpiarReservaWebDeposito();
  assert.equal(obtenerReservaWebDeposito(),null);
});

test("tablet reserva: dos elecciones, foto, PIN, mismo contador y confirmacion SQL",()=>{
  const rutas=leer("router/index.js");
  const elegir=leer("views/ReservaWebElegirView.vue");
  const foto=leer("views/VerificacionFacialView.vue");
  const pin=leer("views/RetiroPinView.vue");
  const contador=leer("views/DepositoView.vue");
  const backend=readFileSync(new URL("../../../backend/Synchro.Api/Services/Encomiendas/ReservasCapturaDemostrativa.cs",import.meta.url),"utf8");
  assert.match(rutas,/path: "\/guardar\/reserva\/tipo"/);
  assert.match(rutas,/path: "\/guardar\/reserva\/captura"/);
  assert.match(rutas,/path: "\/guardar\/reserva\/pin"/);
  assert.match(rutas,/path: "\/guardar\/reserva\/deposito"/);
  assert.match(elegir,/Pertenencias personales/);
  assert.match(elegir,/Encomienda/);
  for(const tipo of ["Normal","Refrigerado","Caliente"])assert.match(elegir,new RegExp(tipo));
  assert.match(foto,/registrarFotoReservaWeb\(coincidencia\[1\]\)/);
  assert.match(foto,/router.push\("\/guardar\/reserva\/pin"\)/);
  assert.match(foto,/const modoV3 = !esReservaWeb\.value && pruebaDescriptorV3/);
  assert.doesNotMatch(foto,/await depositarReservaWeb/);
  assert.match(pin,/identificarDepositoReservaWeb\(pin\.value\)/);
  assert.match(pin,/router.replace\("\/guardar\/reserva\/deposito"\)/);
  assert.match(contador,/segundosRestantesDeposito/);
  assert.match(contador,/extenderLimiteDeposito/);
  assert.match(contador,/Agregar 1 minuto más/);
  assert.match(contador,/await depositarReservaWeb\(sesion.reservaId/);
  assert.match(contador,/if\(datos\?\.depositoConfirmado!==true\)/);
  assert.match(contador,/router.replace\("\/menu"\)/);
  assert.match(backend,/captura\.Descriptor is null/);
  assert.match(backend,/var pin=await VerificarPin/);
  assert.match(backend,/return await ConfirmarDeposito/);
});

test("la seleccion de reserva sigue el diseño de Guardar y el modo oscuro de tablet",()=>{
 const vista=leer("views/ReservaWebElegirView.vue");
 const guardar=leer("views/GuardarView.vue");
 for(const clase of ["pantalla-guardar","regresar","contenido-guardar","cuadricula","tarjeta","contenedor-icono","nombre-tarjeta","accion-guardar","boton-continuar"]){
   assert.match(vista,new RegExp(clase));
   assert.match(guardar,new RegExp(clase));
 }
 assert.match(vista,/28_regresar-oscuro\.svg/);
 assert.match(vista,/29_continuar-oscuro\.svg/);
 assert.match(vista,/\.tarjeta\.seleccionada::after/);
 assert.match(vista,/class="espacio-vacio"/);
 assert.match(vista,/\.cuadricula\.dos-opciones/);
 assert.match(vista,/watch\(elegirTipo/);
 assert.match(vista,/@media \(orientation:portrait\)/);
 assert.match(vista,/@media \(min-width:600px\) and \(min-height:700px\)/);
});

test("se puede elegir la reserva sin vincular ningún dispositivo",()=>{
  const api=leer("services/reservasWebTerminalApi.js");
  const elegir=leer("views/ReservaWebElegirView.vue");
  const pin=leer("views/RetiroPinView.vue");
  assert.doesNotMatch(elegir,/comprobarSesionTerminalReservaWeb|comprobarAsignacionTerminalReservasWeb/);
  assert.doesNotMatch(elegir,/Autorizar estación|errorTerminal|consultandoTerminal/);
  assert.match(elegir,/class="cuadricula"/);
  assert.match(elegir,/:disabled="!seleccionado"/);
  assert.match(pin,/await identificarDepositoReservaWeb\(pin.value\)/);
  assert.match(api,/const BASE = "\/api\/terminales\/reservas"/);
  assert.match(api,/X-CSRF-TOKEN/);
  assert.match(api,/validarPin\(pin\)/);
  assert.doesNotMatch(api,/La sesión de la terminal ha vencido\./);
});
