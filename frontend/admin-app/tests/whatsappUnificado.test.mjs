import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const leer=p=>readFileSync(new URL("../src/"+p,import.meta.url),"utf8");
test("Tablet, Depósito web y Destinatario comparten el panel de PIN",()=>{
 const wa=leer("views/WhatsAppView.vue"),tablet=leer("views/WhatsAppManualAdminView.vue"),panel=leer("components/PanelEnvioPin.vue");
 assert.match(wa,/nombre:'Depósito web'/);
 assert.match(wa,/<PanelEnvioPin/);
 assert.match(tablet,/<PanelEnvioPin/);
 assert.match(tablet,/Atender solicitud/);
 assert.match(tablet,/class="formulario" @submit.prevent="confirmar\(detalle\)"/);
 assert.match(panel,/Copiar mensaje completo/);
 assert.match(panel,/Abrir WhatsApp/);
 assert.match(panel,/Ya envié el PIN por WhatsApp/);
 assert.match(panel,/:disabled="ocupado \|\| !copiado"/);
 assert.match(tablet,/!copiado.value/);
 assert.match(tablet,/envioComprobado: true/);
 assert.match(wa,/envioComprobado: true/);
});

test("la bandeja Tablet excluye solicitudes vencidas y envíos ya completados",()=>{
 const tablet=leer("views/WhatsAppManualAdminView.vue");
 assert.match(tablet,/const pendientes=computed\(\(\)=>operaciones\.value\.filter\(x=>\["Pendiente","Reservado","PinEmitido"\]\.includes\(x\.estado\)\)\)/);
 assert.match(tablet,/operaciones\.filter\(x => x\.estado === "Enviado"\)/);
});


test("El deposito presencial envia texto y referencia interna al confirmar WhatsApp",()=>{
  const vista=leer("views/WhatsAppManualAdminView.vue");
  assert.match(vista,/textoMensaje: `SYNCHRO ACEPTO \$\{item\.codigoAutorizacion\} RECIBIR PIN DE RETIRO POR WHATSAPP`/);
  assert.match(vista,/mensajeReferencia: "DeclaracionManual:" \+ crypto\.randomUUID\(\)/);
  assert.match(vista,/mensajeComprobado: true/);
});

test("El envio presencial declara el telefono autorizado y una referencia interna",()=>{
  const vista=leer("views/WhatsAppManualAdminView.vue");
  const envio=vista.slice(vista.indexOf('await ejecutar("/api/admin/whatsapp-manual/marcar-enviado"'));
  assert.match(envio,/telefonoCliente: pinActual\.value\.telefono/);
  assert.match(envio,/mensajeReferencia: "DeclaracionManual:" \+ crypto\.randomUUID\(\)/);
  assert.match(envio,/envioComprobado: true/);
});
