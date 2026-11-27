// Datos de la reserva solo en memoria; nunca PIN ni fotografía en URL o sessionStorage.
let actual=null;
const CINCO_MINUTOS=5*60*1000;
const tipos=Object.freeze(["Normal","Refrigerado","Caliente"]);
export function elegirServicioReservaWeb(servicio,tipoCasillero="Normal"){
  if(!["Personal","Encomienda"].includes(servicio)||
    !tipos.includes(tipoCasillero)||
    (servicio==="Personal"&&tipoCasillero!=="Normal"))throw new Error("Selecciona un servicio válido.");
  limpiarReservaWebDeposito();
  actual={servicioElegido:servicio,tipoElegido:tipoCasillero,
    foto:null,descriptor:null,pin:null,reservaId:null,casillero:null,vence:Date.now()+CINCO_MINUTOS};
}
export function obtenerEleccionReservaWeb(){
  if(!actual||Date.now()>=actual.vence){limpiarReservaWebDeposito();return null;}
  return {servicio:actual.servicioElegido,tipoCasillero:actual.tipoElegido};
}
export function registrarFotoReservaWeb(foto,descriptor=null){
  if(!obtenerEleccionReservaWeb()||typeof foto!=="string"||
    !/^[A-Za-z0-9+/]+={0,2}$/.test(foto)||foto.length>1400000)
    throw new Error("La fotografía no tiene un formato permitido.");
  const personal=actual.servicioElegido==="Personal";
  if(personal&&(!Array.isArray(descriptor)||descriptor.length!==1024||descriptor.some(v=>!Number.isFinite(v)||Math.abs(v)>1000)||descriptor.reduce((s,v)=>s+v*v,0)<1e-8))
    throw new Error("Falta un descriptor facial V3 válido.");
  if(!personal&&descriptor!==null)throw new Error("La encomienda solo requiere fotografía.");
  if(actual.descriptor)actual.descriptor.fill(0);
  actual.descriptor=personal?Array.from(descriptor):null;
  actual.foto=foto;
  actual.pin=null;actual.vence=Date.now()+CINCO_MINUTOS;
}
export function hayFotoReservaWeb(){
  return Boolean(obtenerEleccionReservaWeb()&&actual.foto);
}
export function iniciarReservaWebDeposito(reserva,pin){
  if(!hayFotoReservaWeb()||!Number.isInteger(reserva?.reservaId)||
    reserva.reservaId<=0||typeof reserva.casillero!=="string"||
    !/^\d{6}$/.test(pin)||
    reserva.servicio!==actual.servicioElegido||
    reserva.tipoCasillero!==actual.tipoElegido)
    throw new Error("El PIN no corresponde al tipo de reserva seleccionado.");
  actual.reservaId=reserva.reservaId;
  actual.casillero=reserva.casillero;
  actual.servicio=reserva.servicio;
  actual.tipoCasillero=reserva.tipoCasillero;
  actual.pin=pin;
  actual.vence=Date.now()+CINCO_MINUTOS;
}
export function obtenerReservaWebDeposito(){
  if(!obtenerEleccionReservaWeb()||!actual.pin||!actual.foto||!actual.reservaId)return null;
  return {reservaId:actual.reservaId,casillero:actual.casillero,
    servicio:actual.servicio,tipoCasillero:actual.tipoCasillero,
    pin:actual.pin,foto:actual.foto,descriptor:actual.descriptor,vence:actual.vence};
}
export function limpiarReservaWebDeposito(){
  if(actual?.descriptor)actual.descriptor.fill(0);
  actual=null;
}
