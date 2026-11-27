// Cálculos sobre datos del servidor; fechas locales de Honduras (UTC-6).
const HORA=3600000,DIA=24*HORA;
export const servicios=[
 {id:"Resguardo",nombre:"Resguardo personal"},
 {id:"Encomienda",nombre:"Encomiendas normales"},
 {id:"Refrigerado",nombre:"Encomiendas refrigeradas"},
 {id:"Caliente",nombre:"Encomiendas calientes"},
 {id:"Carga",nombre:"Carga de dispositivos"}
];
export function diaLocal(valor){return new Date(+new Date(valor)-6*HORA).toISOString().slice(0,10);}
export function fechasIniciales(ahora=new Date()){const hasta=diaLocal(ahora);return {desde:hasta.slice(0,8)+"01",hasta};}
const suma=filas=>filas.reduce((n,x)=>n+Math.round(x.importeHnl*100),0)/100;
const entre=(f,a,b)=>{const t=+new Date(f);return t>=a&&t<b;};
const categoria=(x,espacio)=>x.servicio==="Encomienda"&&["Refrigerado","Caliente"].includes(espacio?.tipo)?espacio.tipo:x.servicio;
function union(intervalos){
 let total=0,fin=-Infinity;
 for(const [a,b] of intervalos.sort((x,y)=>x[0]-y[0])){
  if(b<=a)continue;
  total+=Math.max(0,b-Math.max(a,fin));fin=Math.max(fin,b);
 }
 return total;
}
export function calcularReportes(datos,filtros={}){
 const a=+new Date(datos.desdeUtc),b=+new Date(datos.hastaUtc),prev=+new Date(datos.anteriorUtc);
 const mapa=new Map(datos.espacios.map(e=>[e.codigo,e]));
 const tipo=x=>categoria(x,mapa.get(x.codigo));
 const coincideServicio=x=>!filtros.servicio||filtros.servicio==="Todos"||tipo(x)===filtros.servicio||
  (filtros.servicio==="TodasEncomiendas"&&x.servicio==="Encomienda");
 const coincideEspacio=x=>!filtros.codigo||x.codigo===filtros.codigo;
 const coincide=x=>coincideEspacio(x)&&coincideServicio(x);
 const pagos=datos.pagos.filter(x=>coincide(x)&&(!filtros.metodo||filtros.metodo==="Todos"||x.metodo===filtros.metodo));
 const actuales=pagos.filter(x=>entre(x.fechaUtc,a,b)),anteriores=pagos.filter(x=>entre(x.fechaUtc,prev,a));
 const usos=datos.usos.filter(coincide);
 const iniciados=usos.filter(x=>entre(x.inicioUtc,a,b));
 const finalizados=usos.filter(x=>x.finUtc&&entre(x.finUtc,a,b)&&!["Cancelado","Cancelada"].includes(x.estado));
 const duraciones=finalizados.map(x=>(+new Date(x.finUtc)-+new Date(x.inicioUtc))/60000).filter(x=>Number.isFinite(x)&&x>=0);
 const reservas=datos.reservas.filter(coincide);
 const espacios=datos.espacios.filter(e=>{
  if(!coincideEspacio(e))return false;
  switch(filtros.servicio){
   case "Carga":return ["Cable","Laptop","Inalambrica"].includes(e.tipo);
   case "Resguardo":case "Encomienda":return e.tipo==="Normal";
   case "Refrigerado":case "Caliente":return e.tipo===filtros.servicio;
   case "TodasEncomiendas":return ["Normal","Refrigerado","Caliente"].includes(e.tipo);
   default:return true;
  }
 });
 // Intervalos recortados al período; se solapan sin sumar dos veces.
 const porEspacio=espacios.map(e=>{
  const registros=usos.filter(x=>x.codigo===e.codigo);
  const intervalos=registros.flatMap(x=>{
   const inicio=+new Date(x.inicioUtc);
   const fin=x.finUtc?+new Date(x.finUtc):["Ocupado","Activa"].includes(x.estado)?b:NaN;
   const aa=Math.max(a,inicio),bb=Math.min(b,fin);
   return Number.isFinite(aa)&&Number.isFinite(bb)&&bb>aa?[[aa,bb]]:[];
  });
  const ocupado=union(intervalos);
  return {...e,usos:registros.filter(x=>entre(x.inicioUtc,a,b)).length,horas:ocupado/HORA,
   ocupacion:b>a?100*ocupado/(b-a):null,ingresos:suma(actuales.filter(x=>x.codigo===e.codigo))};
 }).sort((x,y)=>y.usos-x.usos||x.codigo.localeCompare(y.codigo));
 const total=suma(actuales),anterior=suma(anteriores);
 const porServicio=servicios.map(s=>({...s,ingresos:suma(actuales.filter(x=>tipo(x)===s.id)),
  usos:iniciados.filter(x=>tipo(x)===s.id).length,
  finalizados:finalizados.filter(x=>tipo(x)===s.id).length,
  pagos:actuales.filter(x=>tipo(x)===s.id).length}));
 const porMetodo=["Efectivo","Tarjeta","NFC","Sin clasificar"].map(nombre=>({nombre,ingresos:suma(actuales.filter(x=>x.metodo===nombre)),pagos:actuales.filter(x=>x.metodo===nombre).length}));
 const porOrigen=["Web","Tablet"].map(nombre=>({nombre,ingresos:suma(actuales.filter(x=>x.origen===nombre)),pagos:actuales.filter(x=>x.origen===nombre).length}));
 const dias=[];
 for(let t=a;t<b;t+=DIA){const fin=Math.min(t+DIA,b);dias.push({fecha:diaLocal(t),ingresos:suma(actuales.filter(x=>entre(x.fechaUtc,t,fin))),usos:iniciados.filter(x=>entre(x.inicioUtc,t,fin)).length,pagos:actuales.filter(x=>entre(x.fechaUtc,t,fin)).length});}
 const horas=Array.from({length:24},(_,hora)=>({hora,usos:iniciados.filter(x=>new Date(+new Date(x.inicioUtc)-6*HORA).getUTCHours()===hora).length}));
 const porReserva=["Preparacion","ListaParaDeposito","Depositada","Retirada","Rechazada","Cancelada"].map(nombre=>({nombre,total:reservas.filter(x=>x.estado===nombre).length}));
 const porModalidad=["Inalambrica","Cable","Laptop"].map(nombre=>({nombre,usos:iniciados.filter(x=>mapa.get(x.codigo)?.tipo===nombre).length,ingresos:suma(actuales.filter(x=>mapa.get(x.codigo)?.tipo===nombre))}));
 const porTamano=[...new Set(espacios.filter(e=>e.tipo==="Normal").map(e=>e.tamano))].map(nombre=>({nombre,usos:iniciados.filter(x=>mapa.get(x.codigo)?.tipo==="Normal"&&mapa.get(x.codigo)?.tamano===nombre).length,ingresos:suma(actuales.filter(x=>mapa.get(x.codigo)?.tipo==="Normal"&&mapa.get(x.codigo)?.tamano===nombre))}));
 return {total,anterior,variacion:anterior>0?100*(total-anterior)/anterior:null,pagos:actuales.length,
  usos:iniciados.length,finalizados:finalizados.length,ocupacion:porEspacio.length&&b>a?porEspacio.reduce((n,x)=>n+x.horas,0)*HORA/(porEspacio.length*(b-a))*100:null,
  promedioPago:actuales.length?total/actuales.length:null,duracion:duraciones.length?duraciones.reduce((n,x)=>n+x,0)/duraciones.length:null,
  porEspacio,porServicio,porMetodo,porOrigen,dias,horas,porReserva,porModalidad,porTamano,reservas:reservas.length,
  pagosDetalle:actuales.map(x=>({...x,categoria:tipo(x)}))};
}
