import { identificarRetiroReservaWeb, solicitarPagoReservaWebTablet, consultarPagoReservaWebTablet, capturarRetiroReservaWeb, autorizarRecogidaEncomienda, finalizarRecogidaEncomienda } from "./reservasWebTerminalApi.js";
import { registrarRecogidaRetiro } from "./recogidaRetiroSesion.js";
// Foto consentida y PIN únicamente en memoria, con vigencia temporal.
const estado=import.meta.hot?(import.meta.hot.data.encomiendaRetiro??={sesion:null}):{sesion:null};
export function limpiarRetiroEncomienda(){
  if(estado.sesion){estado.sesion.foto="";estado.sesion.pin="";}
  estado.sesion=null;
}
export function registrarFotoRetiroEncomienda(foto){
  if(typeof foto!=="string"||!/^[A-Za-z0-9+/=]+$/.test(foto)||foto.length>1400000)throw new Error("Fotografía PNG inválida.");
  limpiarRetiroEncomienda();
  estado.sesion={foto,pin:"",vence:Date.now()+600000,orden:null,metodo:""};
}
function activa(){
  if(!estado.sesion||Date.now()>=estado.sesion.vence){limpiarRetiroEncomienda();throw new Error("La captura temporal venció. Inicia nuevamente el retiro.");}
  return estado.sesion;
}
function comprobar(s){if(activa()!==s)throw new Error("La sesión de retiro cambió.");}
export function hayFotoRetiroEncomienda(){try{return Boolean(activa().foto);}catch{return false;}}
export function obtenerOrdenRetiroEncomienda(){try{return activa().orden;}catch{return null;}}
export function obtenerMetodoRetiroEncomienda(){try{return activa().metodo;}catch{return "";}}
export async function identificarEncomiendaConPin(pin){
  const s=activa();
  if(!s.foto||!/^[0-9]{6}$/.test(pin))throw new Error("Falta la foto o el PIN de retiro.");
  const d=await identificarRetiroReservaWeb(pin);comprobar(s);
  if(!Number.isInteger(d?.encomiendaId)||d.encomiendaId<=0||!/^[A-Za-z0-9-]{1,10}$/.test(d.casillero||"")||
    (d.pagoConfirmado!==true&&d.pagoAdicionalRequerido!==true))throw new Error("No se pudo autorizar la encomienda.");
  if(d.pagoConfirmado!==true&&(!Number.isFinite(d.importeHnl)||d.importeHnl<=0))throw new Error("La tarifa de la reserva no está configurada.");
  s.pin=pin;s.orden=Object.freeze({encomiendaId:d.encomiendaId,codigoCasillero:d.casillero,importeHnl:d.importeHnl,
    minutosUtilizados:d.reserva?.minutos??null,descripcionTarifa:"Tarifa de la reserva web",pagoConfirmado:d.pagoConfirmado===true,pagoAdicionalRequerido:d.pagoAdicionalRequerido===true});
  return s.orden;
}
export async function consultarPagoEncomienda(){
  const s=activa(),o=s.orden;if(!o||!s.pin)throw new Error("Falta la verificación de la encomienda.");
  const d=await consultarPagoReservaWebTablet(o.encomiendaId);comprobar(s);
  if(d?.reservaId!==o.encomiendaId||d.casillero!==o.codigoCasillero)throw new Error("La respuesta no corresponde a la reserva.");
  return d;
}
export async function elegirPagoEncomienda(metodo){
  const s=activa(),o=s.orden;if(!o||!s.pin||!["Efectivo","Tarjeta","NFC"].includes(metodo))throw new Error("Selecciona un método de pago.");
  const estadoPago=await consultarPagoEncomienda();if(estadoPago.pagoConfirmado===true)return {pagoConfirmado:true};
  if(estadoPago.pagoAdicionalRequerido!==true)throw new Error("El cobro no está disponible.");
  const d=await solicitarPagoReservaWebTablet(o.encomiendaId,s.pin,metodo);comprobar(s);
  if(d?.pendiente!==true||d.pagoRealProcesado!==false)throw new Error("No se registró la solicitud de pago.");
  s.metodo=metodo;return d;
}
export async function prepararRecogidaEncomienda(){
  const s=activa(),o=s.orden;if(!o||!s.foto||!s.pin)throw new Error("Falta la foto o el PIN.");
  const pago=await consultarPagoEncomienda();if(pago.pagoConfirmado!==true)throw new Error("El pago todavía no está confirmado.");
  const id=o.encomiendaId;
  const evidencia=await capturarRetiroReservaWeb(id,{imagenPng:s.foto,descriptor:null,modelo:"SYNCHRO-CAPTURA-DEMOSTRATIVA-v1",versionConsentimiento:"SYNCHRO-CAPTURA-DEMOSTRATIVA-2026-11"});
  comprobar(s);if(!evidencia?.evidenciaId)throw new Error("No se pudo registrar la evidencia.");
  const resultado=await autorizarRecogidaEncomienda(id,s.pin,evidencia.evidenciaId);comprobar(s);
  if(resultado.casillero!==o.codigoCasillero)throw new Error("La autorización no corresponde al casillero.");
  const autorizacion=resultado.autorizacionRetiro;
  registrarRecogidaRetiro({codigo:resultado.casillero,tipo:"encomienda",vence:Date.parse(resultado.venceUtc),volver:"/retirar/encomienda",confirmar:()=>finalizarRecogidaEncomienda(id,autorizacion)});
  limpiarRetiroEncomienda();
}
