import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const leer=p=>readFileSync(new URL("../../../"+p,import.meta.url),"utf8").replace(/\r\n/g,"\n");
test("tarifas de feria persistentes y completas por servicio",()=>{
  const c=JSON.parse(leer("tools/TarifasFeria-Pruebas.json"));
  assert.equal(c.ResguardoPersonal.MinutosBase,60);
  assert.equal(c.ResguardoPersonal.MinutosPorBloque,30);
  assert.deepEqual(c.ResguardoPersonal.Tarifas,{Pequeno:10,Mediano:15,Grande:20});
  assert.deepEqual(c.ResguardoPersonal.Adicionales,{Pequeno:5,Mediano:8,Grande:10});
  assert.deepEqual(c.Carga.map(t=>[t.Modalidad,t.MinutosBase,t.PrecioBase,t.MinutosAdicionales,t.PrecioAdicional]),[
    ["Inalambrica",60,10,30,5],["Cable",60,10,30,5],["Laptop",60,20,30,10]
  ]);
});
test("arranque verifica tarifas sin sobrescribir SQL ni operaciones",()=>{
  const inicio=leer("tools/Iniciar-BackendCargaSegura.ps1");
  const config=leer("tools/Configurar-TarifasFeria-Pruebas.ps1");
  assert.match(inicio,/Configurar-TarifasFeria-Pruebas\.ps1'\) -SoloVerificar/);
  assert.match(config,/if \(-not \$SoloVerificar\) \{\s+\$tx = \$db\.BeginTransaction/);
  assert.match(config,/CobroRetiroPersonal__Obligatorio='true'/);
  assert.match(config,/UPDATE dbo\.TarifasCargaSynchro/);
  assert.match(config,/INSERT dbo\.TarifasCargaSynchro/);
  assert.doesNotMatch(config,/(?:UPDATE|DELETE FROM|INSERT INTO) dbo\.(?:CobrosRetiro|SesionesCarga|UsosCasillero|PreReservas)/);
});
