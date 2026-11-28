// Todos los accesos administrativos van al backend ASP.NET Core vía /api.
// No guardar cookies, contraseñas ni PIN en localStorage.
export async function apiAdmin(url, opciones = {}) {
  const r = await fetch(url, {
    credentials: "same-origin",
    cache: "no-store",
    ...opciones,
  });
  const datos = await r.json().catch(() => null);
  if (!r.ok) {
    const error = new Error(datos?.mensaje || datos?.title || `Solicitud rechazada (${r.status}).`);
    error.status = r.status;
    throw error;
  }
  return datos;
}

export async function consultarSesion() {
  const datos = await apiAdmin("/api/admin/mi-sesion");
  const roles = Array.isArray(datos?.roles) ? datos.roles : [];
  if (!roles.some((rol) => rol === "Administrador" || rol === "Operador" || rol === "Auditor")) {
    const error = new Error("Tu cuenta no dispone de permisos administrativos.");
    error.status = 403;
    throw error;
  }
  return datos;
}

export async function cerrarSesion() {
  const datos = await apiAdmin("/api/admin/csrf-token");
  if (!datos?.csrfToken) throw new Error("No se obtuvo el token de seguridad.");
  return apiAdmin("/api/admin/logout", {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": datos.csrfToken },
    body: JSON.stringify({}),
  });
}

export async function operarEncomienda(ruta, datos) {
  const seguridad = await apiAdmin("/api/admin/csrf-token");
  if (!seguridad?.csrfToken) throw new Error("No se obtuvo el token de seguridad.");
  return apiAdmin(`/api/admin/encomiendas/operativas${ruta}`, {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": seguridad.csrfToken },
    body: JSON.stringify(datos),
  });
}
