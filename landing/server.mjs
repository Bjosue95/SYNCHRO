import { createServer } from "node:http";
import { randomUUID } from "node:crypto";
import { DatabaseSync } from "node:sqlite";
import { mkdirSync, readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

const root = new URL("./", import.meta.url);
const dir = new URL("./datos/", root);
const port = Number(process.env.PORT || 8080);
const origin = process.env.PUBLIC_ORIGIN ? new URL(process.env.PUBLIC_ORIGIN).origin : null;
const modules = new Set(["", "multiple", "maletas", "temperatura", "pertenencias", "pequenos"]);

function database(readOnly = false) {
  if (!readOnly) mkdirSync(dir, { recursive: true, mode: 0o700 });
  const db = new DatabaseSync(fileURLToPath(new URL("solicitudes.sqlite", dir)), { readOnly });
  if (!readOnly) db.exec(readFileSync(new URL("schema.sql", root), "utf8"));
  return db;
}

if (process.argv[2] === "--listar") {
  const db = database(true);
  console.log(JSON.stringify(db.prepare("SELECT * FROM solicitudes ORDER BY creado DESC, id DESC").all(), null, 2));
  db.close();
} else {
  const db = database();
  const insert = db.prepare(
    "INSERT INTO solicitudes (id, tipo, nombre, correo, telefono, empresa, modulo, mensaje) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
  );
  const files = new Map([
    ["/", ["index.html", "text/html; charset=utf-8"]],
    ["/index.html", ["index.html", "text/html; charset=utf-8"]],
    ["/styles.css", ["styles.css", "text/css; charset=utf-8"]],
    ["/app.js", ["app.js", "text/javascript; charset=utf-8"]],
    ...["logo-claro", "isotipo-oscuro", "nombre-blanco", "icono"].map(name => [
      "/assets/" + name + ".png", ["assets/" + name + ".png", "image/png"],
    ]),
    ...[...modules].filter(Boolean).map(name => [
      "/assets/" + name + ".webp", ["assets/" + name + ".webp", "image/webp"],
    ]),
  ]);

  const server = createServer(async (request, response) => {
    function send(code, type, data) {
      response.writeHead(code, {
        "Content-Type": type,
        "X-Content-Type-Options": "nosniff",
        "Cache-Control": "no-store",
        "Content-Security-Policy": "default-src 'self'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'",
      });
      response.end(data);
    }
    const json = (code, value) => send(code, "application/json", JSON.stringify(value));
    const path = (request.url || "").split("?")[0];

    try {
      if (path !== "/api/solicitudes") {
        const asset = files.get(path);
        if (!asset) return send(404, "text/plain; charset=utf-8", "Página no encontrada.");
        if (!["GET", "HEAD"].includes(request.method)) return send(405, "text/plain", "");
        return send(200, asset[1], request.method === "HEAD" ? undefined : readFileSync(new URL(asset[0], root)));
      }
      if (request.method !== "POST") return json(405, { error: "Método no permitido." });
      const requestOrigin = request.headers.origin;
      const allowedOrigins = origin ? [origin] : ["http://localhost:" + port, "http://127.0.0.1:" + port];
      if (requestOrigin && !allowedOrigins.includes(requestOrigin)) return json(403, { error: "Origen no permitido." });
      if (!request.headers["content-type"]?.startsWith("application/x-www-form-urlencoded")) {
        return json(415, { error: "Formato no admitido." });
      }
      const chunks = [];
      let size = 0;
      for await (const chunk of request) {
        size += chunk.length;
        if (size > 24_000) return json(413, { error: "Revisa la longitud de los datos del formulario." });
        chunks.push(chunk);
      }
      const fields = new URLSearchParams(Buffer.concat(chunks).toString("utf8"));
      const get = name => fields.get(name)?.trim() || "";
      const tipo = get("tipo");
      const nombre = get("nombre");
      const correo = get("correo");
      const telefono = get("telefono");
      const empresa = tipo === "empresa" ? get("empresa") : "";
      const modulo = get("modulo");
      const mensaje = get("mensaje");

      if (get("sitio") || get("consentimiento") !== "si" || !nombre || nombre.length > 100 ||
          correo.length > 200 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(correo) ||
          telefono.length > 32 || !["empresa", "particular"].includes(tipo) ||
          (tipo === "empresa" && !empresa) || empresa.length > 120 ||
          !modules.has(modulo) || !mensaje || mensaje.length > 1500) {
        return json(400, { error: "Revisa los campos obligatorios y el formato del correo." });
      }

      insert.run(randomUUID(), tipo, nombre, correo, telefono || null, empresa || null, modulo || null, mensaje);
      return json(201, { recibido: true });
    } catch (error) {
      console.error("Error al procesar la petición:", error.code || error.name);
      return json(500, { error: "No pudimos guardar la solicitud." });
    }
  });
  server.listen(port, process.env.HOST || "127.0.0.1", () => console.log("SYNCHRO · http://localhost:" + port));
}
