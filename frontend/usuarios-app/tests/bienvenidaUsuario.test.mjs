import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const leer=(ruta)=>readFileSync(new URL('../'+ruta,import.meta.url),'utf8');

test('al ingresar se abre la bienvenida, excepto cuando existe un destino protegido',()=>{
  const acceso=leer('src/views/AccesoView.vue');
  const rutas=leer('src/router/index.js');
  assert.match(acceso,/\? destino : "\/bienvenida"/);
  assert.match(acceso,/route\.query\.volver/);
  assert.match(acceso,/await router\.replace\(destinoSeguro\(\)\)/);
  assert.match(rutas,/path: "\/bienvenida", name: "bienvenida"/);
  assert.match(rutas,/meta: \{ privada: true, titulo: "Mi espacio" \}/);
  assert.match(rutas,/BienvenidaView\.vue/);
});

test('bienvenida explica servicios y permite reservar o consultar sin inventar operaciones',()=>{
  const vista=leer('src/views/BienvenidaView.vue');
  const shell=leer('src/components/layout/AppShell.vue');
  assert.match(vista,/Bienvenido/);
  assert.match(vista,/to="\/reservar"/);
  assert.match(vista,/to="\/mis-reservas"/);
  assert.match(vista,/query:\{servicio:'Personal'\}/);
  assert.match(vista,/query:\{servicio:'Encomienda'\}/);
  assert.match(vista,/Guardar pertenencias/);
  assert.match(vista,/Enviar una encomienda/);
  assert.match(vista,/Consultar tus reservas/);
  assert.match(shell,/ruta: "\/bienvenida", nombre: "Inicio"/);
  assert.match(shell,/to="\/bienvenida" class="enlace-marca"/);
  assert.doesNotMatch(vista,/Math\.random|reservas simuladas|total de reservas:/);
});
