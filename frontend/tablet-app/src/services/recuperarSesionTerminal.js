// Renovación silenciosa: únicamente una credencial de dispositivo ya aprobada.
// Nunca solicita ni aprueba una vinculación nueva, ni reintenta una mutación.
let enCurso=null;
const opciones={credentials:"same-origin",cache:"no-store"};
async function renovar(){
 try{
  const sesion=await fetch("/api/terminales/mi-sesion",opciones);
  if(sesion.ok)return true;
  if(sesion.status!==401&&sesion.status!==403)return false;
  const csrf=await fetch("/api/terminales/dispositivo/csrf-token",opciones);
  if(!csrf.ok)return false;
  const datos=await csrf.json();
  if(typeof datos?.csrfToken!=="string"||!datos.csrfToken)return false;
  const respuesta=await fetch("/api/terminales/sesion/renovar",{
   ...opciones,method:"POST",headers:{"X-CSRF-TOKEN":datos.csrfToken}
  });
  if(!respuesta.ok)return false;
  const d=await respuesta.json();
  return Number.isInteger(d?.terminalId)&&d.terminalId>0;
 }catch{return false;}
}
export function recuperarSesionTerminal(){
 if(!enCurso)enCurso=renovar().finally(()=>{enCurso=null;});
 return enCurso;
}
// Solo reintentar la consulta GET de CSRF, antes de iniciar cualquier operación.
export async function obtenerCsrfTerminal(ruta){
 const primera=await fetch(ruta,opciones);
 if(primera.status!==401)return primera;
 if(!await recuperarSesionTerminal())return primera;
 return fetch(ruta,opciones);
}
