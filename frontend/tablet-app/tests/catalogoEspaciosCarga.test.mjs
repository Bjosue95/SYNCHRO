import test from "node:test";
import assert from "node:assert/strict";
import { espaciosPara, infoCarga } from "../src/services/catalogoEspaciosCarga.js";
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