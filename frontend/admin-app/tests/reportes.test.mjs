import test from "node:test";
import assert from "node:assert/strict";
import {calcularReportes,fechasIniciales,diaLocal} from "../src/services/calcularReportes.js";
import {crearExcel} from "../src/services/exportarReportes.js";
const d={desdeUtc:"2026-11-27T06:00:00Z",hastaUtc:"2026-11-28T06:00:00Z",anteriorUtc:"2026-11-26T06:00:00Z",
 espacios:[{codigo:"M03",tipo:"Normal",tamano:"Mediano",estado:"Ocupado"},{codigo:"M04",tipo:"Refrigerado",tamano:"Mediano",estado:"Disponible"},{codigo:"C1",tipo:"Cable",tamano:"No aplica",estado:"Disponible"}],
 usos:[{codigo:"M03",servicio:"Resguardo",estado:"Finalizado",inicioUtc:"2026-11-27T05:00:00Z",finUtc:"2026-11-27T08:00:00Z"},{codigo:"M03",servicio:"Resguardo",estado:"Finalizado",inicioUtc:"2026-11-27T07:00:00Z",finUtc:"2026-11-27T09:00:00Z"},{codigo:"C1",servicio:"Carga",estado:"Activa",inicioUtc:"2026-11-28T05:00:00Z",finUtc:null}],
 pagos:[{codigo:"M03",servicio:"Resguardo",origen:"Tablet",metodo:"Efectivo",importeHnl:15,fechaUtc:"2026-11-27T08:00:00Z"},{codigo:"M04",servicio:"Encomienda",origen:"Web",metodo:"Tarjeta",importeHnl:25,fechaUtc:"2026-11-27T10:00:00Z"},{codigo:"C1",servicio:"Carga",origen:"Tablet",metodo:"NFC",importeHnl:10,fechaUtc:"2026-11-26T10:00:00Z"}],
 reservas:[{codigo:"M04",servicio:"Encomienda",estado:"Depositada",pagoEstado:"ConfirmadoManual",creadoUtc:"2026-11-27T08:00:00Z"}]};
test("ingresos por fecha, modalidad y servicio sin duplicar",()=>{
 const r=calcularReportes(d);assert.equal(r.total,40);assert.equal(r.anterior,10);assert.equal(r.variacion,300);
 assert.equal(r.pagos,2);assert.equal(r.pagosDetalle.find(x=>x.codigo==="M04").categoria,"Refrigerado");assert.equal(r.porServicio.find(x=>x.id==="Refrigerado").ingresos,25);
 assert.equal(r.porMetodo.find(x=>x.nombre==="Efectivo").ingresos,15);assert.equal(r.reservas,1);
});
test("ocupación recorta y une intervalos, y contempla uso activo",()=>{
 const r=calcularReportes(d);assert.equal(r.porEspacio.find(x=>x.codigo==="M03").horas,3);
 assert.equal(r.porEspacio.find(x=>x.codigo==="C1").horas,1);assert.equal(r.usos,2);
 assert.ok(Math.abs(r.ocupacion-100*4/72)<0.00001);
});
test("el método solo modifica importes, no los servicios iniciados",()=>{
 const r=calcularReportes(d,{metodo:"Tarjeta"});assert.equal(r.total,25);assert.equal(r.usos,2);
 const e=calcularReportes(d,{servicio:"TodasEncomiendas"});assert.equal(e.total,25);assert.equal(e.porEspacio.length,2);
});
test("Honduras usa fecha local UTC-6",()=>{
 assert.equal(diaLocal("2026-11-28T01:00:00Z"),"2026-11-27");
 assert.deepEqual(fechasIniciales(new Date("2026-11-28T01:00:00Z")),{desde:"2026-11-01",hasta:"2026-11-27"});
});
test("genera archivo XLSX ZIP con celdas numéricas y texto escapado",async()=>{
 const blob=crearExcel([{nombre:"Resumen",filas:[["A & B",15],["=2+2"]]}]);
 const bytes=new Uint8Array(await blob.arrayBuffer());
 assert.deepEqual([...bytes.slice(0,4)],[80,75,3,4]);
 const texto=new TextDecoder().decode(bytes);
 assert.match(texto,/A &amp; B/);assert.match(texto,/<c><v>15<\/v><\/c>/);assert.match(texto,/=2\+2/);
 assert.doesNotMatch(texto,/<f>/);assert.match(texto,/xl\/workbook.xml/);
});
