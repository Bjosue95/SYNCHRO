// EmailJS REST: la plantilla fija destinatario y remitente en el proveedor.
// Las variables VITE_* son identificadores públicos; nunca incluir secretos.
export async function enviarCotizacionEmail(datos, solucion, env, solicitar = fetch) {
  const service_id = env.VITE_EMAILJS_SERVICE_ID?.trim();
  const template_id = env.VITE_EMAILJS_TEMPLATE_ID?.trim();
  const user_id = env.VITE_EMAILJS_PUBLIC_KEY?.trim();
  if (!service_id || !template_id || !user_id) {
    throw Object.assign(new Error("EmailJS no configurado"), { codigo: "CONFIGURACION" });
  }
  const template_params = {
    nombre: datos.nombre.trim(),
    correo: datos.correo.trim(),
    telefono: datos.telefono.trim() || "No indicado",
    organizacion: datos.organizacion.trim() || "No indicada",
    solucion,
    ciudad: datos.ciudad.trim() || "No indicada",
    descripcion: datos.descripcion.trim()
  };
  const respuesta = await solicitar("https://api.emailjs.com/api/v1.0/email/send", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ service_id, template_id, user_id, template_params }),
    signal: AbortSignal.timeout(20000)
  });
  if (respuesta.status !== 200) throw new Error("EmailJS no confirmó el envío");
}
