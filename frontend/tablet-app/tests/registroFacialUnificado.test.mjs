import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const leer=p=>readFileSync(new URL("../src/views/"+p+".vue",import.meta.url),"utf8").replace(/\r\n/g,"\n");
test("pantallas faciales operativas usan el mismo título y no piden activar V3",()=>{
  for(const nombre of ["VerificacionFacialView","VerificacionFacialCargaView","RetiroCargaRostroView"]){
    const s=leer(nombre);
    assert.match(s,/<h1>Registro facial<\/h1>/);
    assert.doesNotMatch(s,/type="checkbox"|aceptaDescriptorV3|aceptaRetiroV3|VITE_FACIAL_V3_PRUEBA/);
    assert.match(s,/const consentimiento = ref\(false\)/);
    assert.match(s,/aria-describedby="aviso-facial"/);
    assert.match(s,/<details [^>]*class="privacidad-facial">/);
    assert.match(s,/<summary data-preview-permitido>Privacidad<\/summary>/);
    assert.match(s,/"Iniciar escaneo"/);
    assert.doesNotMatch(s,/Autorizar e iniciar escaneo/);
    assert.match(s,/<div class="retroalimentacion"[^>]*>[\s\S]*?<p v-else>\{\{ estado \}\}<\/p>\s*<details/);
    assert.match(s,/\.retroalimentacion:has\(\.privacidad-facial\) \{[\s\S]*?column-gap: 8px;/);
    assert.match(s,/\.pantalla-rostro:has\(\.privacidad-facial\) \.pie-facial \{[\s\S]*?height: 96px;/);
    assert.doesNotMatch(s,/<footer class="pie-facial">\s*<details/);
    assert.doesNotMatch(s,/<details[^>]*\sopen(?:\s|=|>)/);
    assert.match(s,/consentimiento\.value = true/);
  }
  assert.match(leer("VerificacionFacialView"),/v-if="!retirando && !esReservaWeb && casillero !== 'Sin seleccionar'"/);
  assert.match(leer("RetiroCargaRostroView"),/<header class="titulo-paso">\s*<h1>Registro facial<\/h1>\s*<\/header>/);
});
test("el tipo de servicio determina si se usa descriptor o solo fotografía",()=>{
  const s=leer("VerificacionFacialView");
  assert.match(s,/const usarDescriptor = computed\(\(\) => !esRetiroEncomienda\.value && \(!esReservaWeb\.value \|\| reservaPersonal\.value\)\)/);
  assert.match(s,/description: \{ enabled: usarDescriptor\.value \}/);
  assert.match(s,/await validarRostroV3Retiro\(descriptorV3\)/);
  assert.doesNotMatch(s,/validarRostroSimuladoRetiro/);
  assert.match(s,/registrarFotoRetiroEncomienda\(foto\[1\]\)/);
  assert.match(s,/consentimientoV3: usarDescriptor\.value && consentimiento\.value && !retirando\.value/);
  assert.match(leer("VerificacionWhatsappView"),/if \(meta\.consentimientoV3 !== true\) throw/);
  for(const p of ["VerificacionFacialCargaView","RetiroCargaRostroView"])assert.match(leer(p),/const modoV3 = !esVistaPrevia/);
});
