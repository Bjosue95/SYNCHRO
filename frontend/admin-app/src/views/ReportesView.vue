<script setup>
import {computed,onMounted,ref} from "vue";
import {apiAdmin} from "../services/adminApi.js";
import AdminIcon from "../components/AdminIcon.vue";
import {calcularReportes,fechasIniciales,diaLocal,servicios} from "../services/calcularReportes.js";
import {descargarExcel} from "../services/exportarReportes.js";

const pestañas=[{id:"general",nombre:"General"},{id:"ingresos",nombre:"Ingresos"},{id:"resguardo",nombre:"Resguardo personal"},{id:"encomiendas",nombre:"Encomiendas"},{id:"carga",nombre:"Carga de dispositivos"},{id:"casilleros",nombre:"Casilleros"}];
const activa=ref("general");
const inicial=fechasIniciales();
const desde=ref(inicial.desde),hasta=ref(inicial.hasta),hoy=inicial.hasta;
const servicio=ref("Todos"),codigo=ref(""),metodo=ref("Todos");
const datos=ref(null),periodo=ref(null),cargando=ref(false),error=ref("");
const cambioPendiente=computed(()=>!periodo.value||periodo.value.desde!==desde.value||periodo.value.hasta!==hasta.value);
const servicioEfectivo=computed(()=>({resguardo:"Resguardo",encomiendas:"TodasEncomiendas",carga:"Carga"})[activa.value]||servicio.value);
const servicioFijo=computed(()=>["resguardo","encomiendas","carga"].includes(activa.value));
const filtro=computed(()=>({servicio:servicioEfectivo.value,codigo:codigo.value,metodo:metodo.value}));
const reporte=computed(()=>datos.value?calcularReportes(datos.value,filtro.value):null);
const general=computed(()=>activa.value==="general");
const financiero=computed(()=>general.value||activa.value==="ingresos");
const operativo=computed(()=>general.value||activa.value==="casilleros");
const formato=new Intl.NumberFormat("es-HN",{style:"currency",currency:"HNL",minimumFractionDigits:2});
const moneda=v=>v==null?"—":formato.format(v);
const numero=v=>v==null?"—":Number(v).toLocaleString("es-HN",{maximumFractionDigits:1});
const porcentaje=v=>v==null?"—":numero(v)+" %";
const nombreTipo=v=>({Normal:"Normal",Refrigerado:"Refrigerado",Caliente:"Caliente",Inalambrica:"Inalámbrica",Cable:"Cable",Laptop:"Laptop"})[v]||v;
const nombreEstado=v=>({Preparacion:"En preparación",ListaParaDeposito:"Lista para depósito",Depositada:"Depositada",Retirada:"Retirada",Rechazada:"Rechazada",Cancelada:"Cancelada"})[v]||v;
const nombreServicio=v=>servicios.find(s=>s.id===v)?.nombre||v;
const maxIngreso=computed(()=>Math.max(1,...(reporte.value?.dias.map(x=>x.ingresos)||[])));
const maxHora=computed(()=>Math.max(1,...(reporte.value?.horas.map(x=>x.usos)||[])));
const punto=(x,i)=>[reporte.value.dias.length===1?300:30+i*540/(reporte.value.dias.length-1),150-120*x.ingresos/maxIngreso.value];
const linea=computed(()=>reporte.value?.dias.map((x,i)=>punto(x,i).join(",")).join(" ")||"");
const mostrarPuntos=computed(()=>reporte.value?.dias.length<=31);
const maxServicio=computed(()=>Math.max(1,...(reporte.value?.porServicio.map(x=>x.ingresos)||[])));
const seleccionEspacios=computed(()=>datos.value?.espacios.filter(e=>{
 switch(servicioEfectivo.value){
 case "Resguardo":case "Encomienda":return e.tipo==="Normal";
 case "TodasEncomiendas":return ["Normal","Refrigerado","Caliente"].includes(e.tipo);
 case "Carga":return ["Cable","Laptop","Inalambrica"].includes(e.tipo);
 case "Refrigerado":case "Caliente":return e.tipo===servicioEfectivo.value;
 default:return true;}
})||[]);
function cambiarPestaña(id){activa.value=id;codigo.value="";servicio.value="Todos";}
function cambiarServicio(){codigo.value="";}
async function consultar(){
 if(cargando.value)return;
 error.value="";cargando.value=true;
 const d=desde.value,h=hasta.value;
 try{
   const p=new URLSearchParams({desde:d,hasta:h});
   const r=await apiAdmin("/api/admin/reportes?"+p);
   if(!r?.pagoSimulado||!["espacios","usos","pagos","reservas"].every(k=>Array.isArray(r[k])))throw new Error("La respuesta de reportes no es válida.");
   datos.value=r;periodo.value={desde:d,hasta:h};
   if(codigo.value&&!r.espacios.some(x=>x.codigo===codigo.value))codigo.value="";
 }catch(e){datos.value=null;periodo.value=null;error.value=e?.message||"No se pudieron consultar los reportes.";}
 finally{cargando.value=false;}
}
function atajo(tipo){
 const actual=fechasIniciales();hasta.value=actual.hasta;
 if(tipo==="hoy")desde.value=actual.hasta;
 else if(tipo==="semana"){desde.value=new Date(+new Date(actual.hasta+"T00:00:00Z")-6*86400000).toISOString().slice(0,10);}
 else desde.value=actual.desde;
 void consultar();
}
function exportar(){
 if(!reporte.value||cambioPendiente.value||cargando.value)return;
 const r=reporte.value;
 const hojas=[
 {nombre:"Resumen",filas:[["SYNCHRO · Reporte del prototipo"],["Desde",periodo.value.desde],["Hasta",periodo.value.hasta],["Servicio",servicioEfectivo.value],["Espacio",codigo.value||"Todos"],["Método (solo ingresos)",metodo.value],["Cobros confirmados de prueba (HNL)",r.total],["Período anterior (HNL)",r.anterior],["Servicios iniciados",r.usos],["Servicios finalizados",r.finalizados],["Ocupación histórica estimada (%)",r.ocupacion],["Ingreso promedio por cobro (HNL)",r.promedioPago],["Duración promedio (min)",r.duracion],["Nota","No representa dinero real. Ocupación según capacidad actual y tiempos registrados."]]},
 {nombre:"Servicios",filas:[["Servicio","Ingresos HNL","Iniciados","Finalizados","Cobros"],...r.porServicio.map(x=>[x.nombre,x.ingresos,x.usos,x.finalizados,x.pagos])]},
 {nombre:"Ingresos diarios",filas:[["Fecha Honduras","Ingresos HNL","Usos iniciados","Cobros"],...r.dias.map(x=>[x.fecha,x.ingresos,x.usos,x.pagos])]},
 {nombre:"Métodos y origen",filas:[["Método","Ingresos HNL","Cobros"],...r.porMetodo.map(x=>[x.nombre,x.ingresos,x.pagos]),[],["Origen","Ingresos HNL","Cobros"],...r.porOrigen.map(x=>[x.nombre,x.ingresos,x.pagos])]},
 {nombre:"Casilleros",filas:[["Código","Tipo","Tamaño","Estado actual","Usos iniciados","Horas ocupadas","Ocupación %","Ingresos HNL"],...r.porEspacio.map(x=>[x.codigo,x.tipo,x.tamano,x.estado,x.usos,x.horas,x.ocupacion,x.ingresos])]},
 {nombre:"Reservas",filas:[["Estado actual de reservas creadas en el período","Cantidad"],...r.porReserva.map(x=>[nombreEstado(x.nombre),x.total])]},
 {nombre:"Demanda y modalidades",filas:[["Hora local","Inicios"],...r.horas.map(x=>[x.hora,x.usos]),[],["Modalidad carga","Inicios","Ingresos HNL"],...r.porModalidad.map(x=>[nombreTipo(x.nombre),x.usos,x.ingresos]),[],["Tamaño","Inicios","Ingresos HNL"],...r.porTamano.map(x=>[x.nombre,x.usos,x.ingresos])]},
 {nombre:"Cobros confirmados",filas:[["Fecha local","Espacio","Servicio","Origen","Método","Importe HNL"],...r.pagosDetalle.map(x=>[diaLocal(x.fechaUtc),x.codigo,nombreServicio(x.categoria),x.origen,x.metodo,x.importeHnl])]}
 ];
 descargarExcel(hojas,"SYNCHRO-reportes-"+periodo.value.desde+"-"+periodo.value.hasta+".xlsx");
}
function imprimir(){if(reporte.value&&!cambioPendiente.value&&!cargando.value)window.print();}
onMounted(consultar);
</script>

<template>
 <div class="reportes-view">
  <header class="page-heading spread-heading reportes-hero">
   <div><p class="eyebrow">GESTIÓN · SYNCHRO</p><h1>Reportes y estadísticas</h1><p>Analiza los ingresos, la demanda y la utilización de la estación.</p></div>
   <div class="reportes-exportar">
    <button class="button-secondary" type="button" :disabled="!reporte||cargando||cambioPendiente" @click="exportar"><AdminIcon name="report" :size="17"/> Exportar Excel</button>
    <button class="rp-primario" type="button" :disabled="!reporte||cargando||cambioPendiente" @click="imprimir">Generar PDF</button>
   </div>
  </header>
  <section class="panel reportes-filtros">
   <form @submit.prevent="consultar">
    <div class="rp-fechas">
     <label>Desde<input v-model="desde" type="date" required :max="hasta||hoy"/></label>
     <label>Hasta<input v-model="hasta" type="date" required :min="desde" :max="hoy"/></label>
     <button type="submit" class="rp-primario" :disabled="cargando">{{cargando?"Consultando…":"Consultar"}}</button>
    </div>
    <div class="rp-atajos"><button type="button" :disabled="cargando" @click="atajo('hoy')">Hoy / cierre diario</button><button type="button" :disabled="cargando" @click="atajo('semana')">Últimos 7 días</button><button type="button" :disabled="cargando" @click="atajo('mes')">Este mes</button></div>
    <div class="rp-filtros-sec">
     <label>Servicio<select v-model="servicio" :disabled="servicioFijo" @change="cambiarServicio"><option value="Todos">Todos los servicios</option><option v-for="s in servicios" :key="s.id" :value="s.id">{{s.nombre}}</option></select><small v-if="servicioFijo">Determinado por la pestaña</small></label>
     <label>Casillero o espacio<select v-model="codigo"><option value="">Todos los espacios</option><option v-for="e in seleccionEspacios" :key="e.codigo" :value="e.codigo">{{e.codigo}} · {{nombreTipo(e.tipo)}}</option></select></label>
     <label>Método de pago (ingresos)<select v-model="metodo"><option>Todos</option><option>Efectivo</option><option>Tarjeta</option><option>NFC</option><option>Sin clasificar</option></select></label>
    </div>
   </form>
  </section>
  <nav class="rp-tabs" aria-label="Tipo de reporte"><button v-for="p in pestañas" :key="p.id" type="button" :class="{activo:activa===p.id}" :aria-current="activa===p.id?'page':undefined" @click="cambiarPestaña(p.id)">{{p.nombre}}</button></nav>
  <p v-if="error" class="inline-error" role="alert">{{error}}</p>
  <p v-else-if="cargando" class="panel rp-vacio" role="status">Consultando registros de la estación…</p>
  <template v-else-if="reporte">
   <div class="rp-contexto"><strong>{{periodo.desde}} — {{periodo.hasta}}</strong><span>Honduras · HNL · Cobros del prototipo, sin ingresos bancarios reales.</span><span v-if="servicioEfectivo!=='Todos'||codigo||metodo!=='Todos'">Filtros: {{servicioEfectivo==='TodasEncomiendas'?'Encomiendas':nombreServicio(servicioEfectivo)}} · {{codigo||'Todos los espacios'}} · {{metodo}} </span></div>
   <p v-if="cambioPendiente" class="rp-nota">Pulsa Consultar para aplicar las nuevas fechas.</p>
   <div class="rp-kpis">
    <article class="rp-kpi rp-destacado"><span>Ingresos confirmados de prueba</span><strong>{{moneda(reporte.total)}}</strong><small>{{reporte.pagos}} cobros confirmados</small></article>
    <article class="rp-kpi"><span>Servicios iniciados</span><strong>{{numero(reporte.usos)}}</strong><small>{{reporte.finalizados}} finalizaciones en el período</small></article>
    <article class="rp-kpi"><span>Ocupación promedio estimada</span><strong>{{porcentaje(reporte.ocupacion)}}</strong><small>Sobre capacidad actual</small></article>
    <article class="rp-kpi"><span>Ingreso promedio por cobro</span><strong>{{moneda(reporte.promedioPago)}}</strong><small>Duración media finalizada: {{numero(reporte.duracion)}} min</small></article>
   </div>

   <div v-if="financiero" class="rp-grid">
    <section class="panel rp-panel"><h2>Ingresos por servicio</h2><div v-for="s in reporte.porServicio" :key="s.id" class="rp-barra-fila"><div><span>{{s.nombre}}</span><strong>{{moneda(s.ingresos)}}</strong></div><div class="rp-pista"><i :style="{width:(s.ingresos/maxServicio*100)+'%'}"></i></div></div></section>
    <section class="panel rp-panel"><h2>Comparación de períodos</h2><p class="rp-muted">Intervalo anterior de igual cantidad de días naturales.</p><div class="rp-comparacion"><span>Actual</span><strong>{{moneda(reporte.total)}}</strong><span>Anterior</span><strong>{{moneda(reporte.anterior)}}</strong><span>Variación</span><strong>{{porcentaje(reporte.variacion)}}</strong></div><p class="rp-muted">Si el día actual no terminó, el período actual es parcial. Sin base anterior no se calcula porcentaje.</p></section>
    <section class="panel rp-panel rp-ancho"><h2>Comportamiento de los ingresos</h2><p class="rp-muted">Cobros confirmados por fecha local</p>
     <svg class="rp-grafico" viewBox="0 0 600 180" role="img" aria-label="Evolución diaria de ingresos">
      <line v-for="y in [30,70,110,150]" :key="y" x1="30" x2="570" :y1="y" :y2="y" class="rp-gridline"/>
      <polyline :points="linea" fill="none" stroke="#00a4b5" stroke-width="3" stroke-linejoin="round"/>
      <template v-if="mostrarPuntos"><circle v-for="(x,i) in reporte.dias" :key="x.fecha" :cx="punto(x,i)[0]" :cy="punto(x,i)[1]" r="3.5" fill="#00a4b5"><title>{{x.fecha}}: {{moneda(x.ingresos)}}</title></circle></template>
      <text x="30" y="174" class="rp-svgtext">{{periodo.desde}}</text><text x="570" y="174" text-anchor="end" class="rp-svgtext">{{periodo.hasta}}</text>
     </svg>
     <details><summary>Ver cifras diarias</summary><div class="rp-tabla-scroll"><table><thead><tr><th>Fecha</th><th>Ingresos</th><th>Cobros</th><th>Servicios iniciados</th></tr></thead><tbody><tr v-for="d in reporte.dias" :key="d.fecha"><td>{{d.fecha}}</td><td>{{moneda(d.ingresos)}}</td><td>{{d.pagos}}</td><td>{{d.usos}}</td></tr></tbody></table></div></details>
    </section>
    <section class="panel rp-panel"><h2>Métodos de pago</h2><div v-for="m in reporte.porMetodo" :key="m.nombre" class="rp-barra-fila"><div><span>{{m.nombre}} · {{m.pagos}} cobros</span><strong>{{moneda(m.ingresos)}}</strong></div><div class="rp-pista"><i :style="{width:(reporte.total?100*m.ingresos/reporte.total:0)+'%'}"></i></div></div></section>
    <section class="panel rp-panel"><h2>Origen de los pagos</h2><div v-for="m in reporte.porOrigen" :key="m.nombre" class="rp-barra-fila"><div><span>{{m.nombre}} · {{m.pagos}} cobros</span><strong>{{moneda(m.ingresos)}}</strong></div><div class="rp-pista"><i :style="{width:(reporte.total?100*m.ingresos/reporte.total:0)+'%'}"></i></div></div></section>
   </div>

   <div v-if="operativo" class="rp-grid">
    <section class="panel rp-panel"><h2>Horarios de mayor demanda</h2><p class="rp-muted">Servicios iniciados por hora de Honduras</p><div class="rp-horas"><div v-for="h in reporte.horas" :key="h.hora" :title="h.hora+':00 · '+h.usos+' servicios'"><i :style="{height:(100*h.usos/maxHora)+'%'}"></i><span v-if="h.hora%3===0">{{h.hora}}</span></div></div></section>
    <section class="panel rp-panel"><h2>Disponibilidad actual</h2><p class="rp-muted">Estado al consultar; no es una reconstrucción histórica.</p><div class="rp-comparacion"><span>Disponibles</span><strong>{{reporte.porEspacio.filter(x=>x.estado==='Disponible').length}}</strong><span>Ocupados</span><strong>{{reporte.porEspacio.filter(x=>x.estado==='Ocupado').length}}</strong><span>Otros estados</span><strong>{{reporte.porEspacio.filter(x=>!['Disponible','Ocupado'].includes(x.estado)).length}}</strong></div></section>
   </div>
   <section v-if="activa==='resguardo'" class="panel rp-panel"><h2>Resguardo por tamaño</h2><div class="rp-tabla-scroll"><table><thead><tr><th>Tamaño</th><th>Usos iniciados</th><th>Ingresos</th></tr></thead><tbody><tr v-for="x in reporte.porTamano" :key="x.nombre"><td>{{x.nombre}}</td><td>{{x.usos}}</td><td>{{moneda(x.ingresos)}}</td></tr></tbody></table></div></section>
   <section v-if="activa==='encomiendas'" class="panel rp-panel"><h2>Encomiendas por tipo</h2><div class="rp-tabla-scroll"><table><thead><tr><th>Servicio</th><th>Depósitos iniciados</th><th>Retiros finalizados</th><th>Ingresos</th></tr></thead><tbody><tr v-for="s in reporte.porServicio.filter(x=>['Encomienda','Refrigerado','Caliente'].includes(x.id))" :key="s.id"><td>{{s.nombre}}</td><td>{{s.usos}}</td><td>{{s.finalizados}}</td><td>{{moneda(s.ingresos)}}</td></tr></tbody></table></div><h2>Reservas creadas · {{reporte.reservas}}</h2><p class="rp-muted">Estado actual de las reservas creadas durante el intervalo seleccionado.</p><div class="rp-comparacion"><template v-for="r in reporte.porReserva" :key="r.nombre"><span>{{nombreEstado(r.nombre)}}</span><strong>{{r.total}}</strong></template></div></section>
   <section v-if="activa==='carga'" class="panel rp-panel"><h2>Carga por modalidad</h2><div class="rp-tabla-scroll"><table><thead><tr><th>Modalidad</th><th>Servicios iniciados</th><th>Ingresos</th></tr></thead><tbody><tr v-for="s in reporte.porModalidad" :key="s.nombre"><td>{{nombreTipo(s.nombre)}}</td><td>{{s.usos}}</td><td>{{moneda(s.ingresos)}}</td></tr></tbody></table></div></section>

   <section v-if="activa!=='ingresos'" class="panel rp-panel"><h2>Utilización por casillero o espacio</h2><p class="rp-muted">Ordenados por cantidad de servicios iniciados.</p><div class="rp-tabla-scroll"><table><thead><tr><th>Espacio</th><th>Tipo</th><th>Estado actual</th><th>Usos</th><th>Horas ocupadas</th><th>Ocupación</th><th>Ingresos</th></tr></thead><tbody><tr v-for="e in reporte.porEspacio" :key="e.codigo"><td><strong>{{e.codigo}}</strong></td><td>{{nombreTipo(e.tipo)}}</td><td>{{e.estado}}</td><td>{{e.usos}}</td><td>{{numero(e.horas)}}</td><td>{{porcentaje(e.ocupacion)}}</td><td>{{moneda(e.ingresos)}}</td></tr></tbody></table></div></section>
   <section v-if="activa==='ingresos'" class="panel rp-panel"><h2>Detalle de cobros confirmados</h2><p v-if="!reporte.pagosDetalle.length" class="rp-muted">No hay cobros confirmados en el período.</p><div v-else class="rp-tabla-scroll"><table><thead><tr><th>Fecha</th><th>Espacio</th><th>Servicio</th><th>Origen</th><th>Método</th><th>Importe</th></tr></thead><tbody><tr v-for="(x,i) in reporte.pagosDetalle.slice(0,100)" :key="i"><td>{{diaLocal(x.fechaUtc)}}</td><td>{{x.codigo}}</td><td>{{nombreServicio(x.categoria)}}</td><td>{{x.origen}}</td><td>{{x.metodo}}</td><td>{{moneda(x.importeHnl)}}</td></tr></tbody></table></div><p v-if="reporte.pagosDetalle.length>100" class="rp-muted">Primeros 100 cobros. Excel incluye todos los registros del período.</p></section>
   <p class="rp-nota">Los importes son cobros brutos de prueba confirmados, no ingresos netos ni dinero real. La ocupación utiliza la capacidad actual y los intervalos registrados, sin duplicar solapamientos. El filtro de método solo afecta los importes y cobros, no los tiempos de uso.</p>
  </template>
 </div>
</template>
<style scoped>
.reportes-view{color:var(--ink);display:grid;gap:18px;min-width:0;}
.reportes-hero{margin-bottom:0;}
.reportes-exportar,.rp-fechas,.rp-atajos,.rp-tabs{display:flex;flex-wrap:wrap;align-items:center;gap:10px;}
.rp-primario{border:0;background:#00b9c7;color:#01305c;border-radius:11px;min-height:45px;padding:11px 18px;font:inherit;font-weight:750;cursor:pointer;}
.reportes-view button:disabled{opacity:.5;cursor:not-allowed;}
.reportes-filtros{padding:22px;}
.reportes-filtros form{display:grid;gap:15px;}
.rp-fechas{align-items:end;}
.rp-fechas label{flex:1;min-width:155px;}
.reportes-filtros label{display:grid;gap:7px;font-size:13px;font-weight:700;color:var(--ink);}
.reportes-filtros input,.reportes-filtros select{box-sizing:border-box;min-height:45px;min-width:0;width:100%;border:1px solid var(--border);border-radius:10px;background:var(--white);color:var(--ink);padding:10px;font:inherit;}
.rp-atajos button{border:1px solid var(--border);background:var(--canvas);color:var(--ink);border-radius:9px;padding:8px 13px;cursor:pointer;font:inherit;font-size:13px;}
.rp-filtros-sec{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px;}
.rp-tabs{width:fit-content;max-width:100%;background:var(--white);border:1px solid var(--border);padding:6px;border-radius:16px;}
.rp-tabs button{background:transparent;color:var(--ink);border:0;border-radius:11px;padding:11px 18px;min-height:45px;font:inherit;font-weight:750;font-size:14px;cursor:pointer;}
.rp-tabs button.activo{background:#00b9c7;color:#01305c;}
.rp-tabs button:hover:not(.activo){background:var(--canvas);}
.rp-contexto{display:flex;justify-content:space-between;flex-wrap:wrap;gap:8px;font-size:13px;color:var(--muted);}
.rp-kpis{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:14px;}
.rp-kpi{display:flex;flex-direction:column;gap:10px;padding:22px 19px;background:var(--white);border:1px solid var(--border);border-radius:18px;min-width:0;border-top:4px solid #00a7b7;}
.rp-kpi span{font-size:13px;font-weight:750;color:var(--muted);}
.rp-kpi strong{font-size:clamp(24px,2.5vw,40px);font-weight:850;line-height:1.15;font-variant-numeric:tabular-nums;overflow-wrap:anywhere;}
.rp-kpi small{color:var(--muted);font-size:12px;}
.rp-destacado{background:linear-gradient(135deg,#01305c,#07576b);color:#fff;}
.rp-destacado span,.rp-destacado small{color:#d4eef6;}
.rp-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:15px;}
.rp-panel{padding:clamp(16px,2vw,25px);min-width:0;}
.rp-panel h2{font-size:19px;margin:0 0 15px;}
.rp-panel h2:not(:first-child){margin-top:25px;}
.rp-ancho{grid-column:1/-1;}
.rp-barra-fila{display:grid;gap:7px;margin:14px 0;}
.rp-barra-fila>div:first-child{display:flex;justify-content:space-between;align-items:baseline;gap:8px;flex-wrap:wrap;font-size:14px;}
.rp-pista{height:10px;border-radius:20px;background:var(--canvas);overflow:hidden;}
.rp-pista i{display:block;height:100%;border-radius:20px;background:linear-gradient(90deg,#008396,#00b9c7);}
.rp-comparacion{display:grid;grid-template-columns:1fr auto;align-items:center;gap:14px;padding:10px 0;}
.rp-comparacion strong{font-size:20px;font-variant-numeric:tabular-nums;}
.rp-muted,.rp-nota{font-size:13px;line-height:1.55;color:var(--muted);}
.rp-nota{padding:13px 15px;border:1px solid var(--border);border-radius:11px;background:var(--canvas);margin:0;}
.rp-grafico{width:100%;height:auto;max-height:250px;}
.rp-gridline{stroke:var(--border);stroke-dasharray:4 4;}
.rp-svgtext{fill:var(--muted);font-size:12px;}
.rp-horas{height:130px;display:grid;grid-template-columns:repeat(24,minmax(0,1fr));gap:3px;padding-bottom:22px;}
.rp-horas>div{position:relative;display:flex;align-items:flex-end;}
.rp-horas i{display:block;background:#00a7b7;width:100%;border-radius:3px 3px 0 0;}
.rp-horas span{position:absolute;bottom:-19px;font-size:10px;color:var(--muted);}
.rp-tabla-scroll{overflow:auto;max-width:100%;}
.rp-panel table{width:100%;border-collapse:collapse;text-align:left;font-size:13px;}
.rp-panel td,.rp-panel th{padding:12px 10px;border-bottom:1px solid var(--border);white-space:nowrap;}
.rp-panel th{color:var(--muted);font-size:12px;background:var(--canvas);}
.rp-panel summary{cursor:pointer;color:var(--ink);font-weight:700;padding:10px 0;}
.rp-vacio{text-align:center;padding:35px;}
@media(max-width:1050px){.rp-kpis{grid-template-columns:repeat(2,minmax(0,1fr));}}
@media(max-width:690px){.rp-grid,.rp-filtros-sec{grid-template-columns:minmax(0,1fr);}.rp-tabs{width:100%;}.rp-tabs button{flex:1;padding:9px 12px;}.rp-kpis{gap:8px;}.rp-kpi{padding:15px 12px;}.rp-kpi strong{font-size:24px;}}
@media print{
 :global(.admin-sidebar),:global(.admin-topbar),:global(.mobile-overlay){display:none!important;}
 :global(.admin-shell),:global(.admin-workspace),:global(.admin-page){display:block!important;position:static!important;width:auto!important;max-width:none!important;height:auto!important;margin:0!important;padding:0!important;overflow:visible!important;background:#fff!important;}
 .reportes-view{--ink:#01305c;--muted:#506774;--white:#fff;--canvas:#f2f7fa;--border:#d3e1e8;gap:12px;}
 .reportes-exportar,.reportes-filtros,.rp-tabs{display:none!important;}
 .rp-kpis{grid-template-columns:repeat(2,minmax(0,1fr));}
 .rp-panel,.rp-kpi{box-shadow:none!important;break-inside:avoid;}
 .rp-destacado{background:#fff;color:#01305c;}.rp-destacado span,.rp-destacado small{color:#506774;}
 .rp-tabla-scroll{overflow:visible;}
 .rp-panel table{font-size:10px;}.rp-panel td,.rp-panel th{white-space:normal;padding:6px 4px;}
 .rp-grid{display:block;}.rp-grid>.rp-panel{margin-bottom:12px;}
}
</style>