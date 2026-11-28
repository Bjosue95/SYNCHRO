import test from "node:test";
import assert from "node:assert/strict";
import { enviarCotizacionEmail } from "../src/services/cotizacionesEmail.js";

const datos = { nombre: " Cliente Ejemplo ", correo: "cliente@example.com",
  telefono: "", organizacion: "", ciudad: " Comayagua ", descripcion: " Una estación de prueba " };
const env = { VITE_EMAILJS_SERVICE_ID: "service_test", VITE_EMAILJS_TEMPLATE_ID: "template_test", VITE_EMAILJS_PUBLIC_KEY: "public_test" };

test("envía solo parámetros de la plantilla sin destinatario dinámico ni secretos", async () => {
  await enviarCotizacionEmail(datos, "Módulos", env, async (url, opciones) => {
    assert.equal(url, "https://api.emailjs.com/api/v1.0/email/send");
    assert.equal(opciones.method, "POST");
    assert.deepEqual(JSON.parse(opciones.body), {
      service_id: "service_test", template_id: "template_test", user_id: "public_test",
      template_params: { nombre: "Cliente Ejemplo", correo: "cliente@example.com",
        telefono: "No indicado", organizacion: "No indicada", solucion: "Módulos",
        ciudad: "Comayagua", descripcion: "Una estación de prueba" }
    });
    return { status: 200 };
  });
});
test("sin configuración no hace una petición", async () => {
  await assert.rejects(enviarCotizacionEmail(datos, "Módulos", {}, () => {
    assert.fail("No debe enviar");
  }), { codigo: "CONFIGURACION" });
});
test("rechazos del proveedor y errores de red no se anuncian como éxito", async () => {
  for (const status of [400, 403, 429, 500])
    await assert.rejects(enviarCotizacionEmail(datos, "Módulos", env, async () => ({ status })));
  await assert.rejects(enviarCotizacionEmail(datos, "Módulos", env, async () => { throw new Error("Red"); }));
});
