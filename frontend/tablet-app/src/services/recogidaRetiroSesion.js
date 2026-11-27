// Sesión efímera común: nunca se persisten credenciales.
let pendiente=null;
export function registrarRecogidaRetiro({codigo,tipo,vence,confirmar,volver,limpiar=()=>{}}){
  if(!/^[A-Za-z0-9-]{1,10}$/.test(codigo||"")||!["personal","encomienda","carga"].includes(tipo)||
    !Number.isFinite(vence)||vence<=Date.now()||typeof confirmar!=="function"||
    typeof limpiar!=="function"||!["/retirar/rostro","/retirar/encomienda","/retirar/dispositivo"].includes(volver))
    throw new Error("La autorización de recogida no está disponible. Repite la verificación.");
  limpiarRecogidaPendiente();
  pendiente=Object.freeze({codigoCasillero:codigo,tipo,vence,confirmar,volver,limpiar});
}
export function tomarRecogidaRetiro(){const actual=pendiente;pendiente=null;return actual;}
export function limpiarRecogidaPendiente(){const anterior=pendiente;pendiente=null;anterior?.limpiar();}
