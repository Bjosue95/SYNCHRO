import test from "node:test";
import assert from "node:assert/strict";
import { espaciosPara, infoCarga, consultarEspaciosCarga, comprobarEspacioCarga } from "../src/services/catalogoEspaciosCarga.js";
const inventario = [
  {codigo:"W1",estado:"Disponible"}, {codigo:"W2",estado:"Ocupado"},
  {codigo:"C1",estado:"Disponible"}, {codigo:"C2",estado:"Reservado"},
  {codigo:"C3",estado:"Disponible"}, {codigo:"C4",estado:"Disponible"},
  {codigo:"L1",estado:"Disponible"}, {codigo:"L2",estado:"Ocupado"},
  {codigo:"QI01",estado:"Disponible"}, {codigo:"USB01",estado:"Disponible"},
];
test("inalambrica muestra solamente W libres", () => assert.deepEqual(espaciosPara(inventario,"Celular","Inalambrica").map(x=>x.codigo),["W1"]));
test("cable muestra solamente C libres", () => assert.deepEqual(espaciosPara(inventario,"Celular","Cable").map(x=>x.codigo),["C1","C3","C4"]));
test("laptop muestra solamente L libres", () => assert.deepEqual(espaciosPara(inventario,"Laptop","Laptop").map(x=>x.codigo),["L1"]));
test("no muestra codigos provisionales", () => { assert.equal(infoCarga("QI01"),null); assert.deepEqual(espaciosPara(inventario,"Celular",""),[]); });

test("consulta y comprobacion usan la ruta autenticada de carga", async () => {
  let llamadas=0;
  const solicitar=async (ruta,opciones)=>{
    llamadas++;
    assert.equal(ruta,"/api/terminales/carga/espacios");
    assert.equal(opciones.credentials,"same-origin");
    assert.equal(opciones.cache,"no-store");
    return {ok:true,json:async()=>[{Codigo:" C1 ",Estado:" Disponible "},{codigo:"W1",estado:"Ocupado"},{codigo:"L1",estado:"Disponible"}]};
  };
  assert.deepEqual(await consultarEspaciosCarga(solicitar),[{codigo:"C1",estado:"Disponible"},{codigo:"W1",estado:"Ocupado"},{codigo:"L1",estado:"Disponible"}]);
  await comprobarEspacioCarga("C1",solicitar);
  await comprobarEspacioCarga("L1",solicitar);
  await assert.rejects(()=>comprobarEspacioCarga("W1",solicitar),/ya no está disponible/);
  assert.equal(llamadas,4);
});
test("el error HTTP no se interpreta como espacio ocupado",async()=>{
  await assert.rejects(()=>consultarEspaciosCarga(async()=>({ok:false,status:403})),/HTTP 403/);
});
