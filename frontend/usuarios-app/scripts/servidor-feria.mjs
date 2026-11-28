// Publicacion local de SOLO LECTURA. No es un proxy generico.
import http from "node:http";
import https from "node:https";
import { createReadStream } from "node:fs";
import { stat, realpath } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import path from "node:path";

const root = await realpath(fileURLToPath(new URL("../dist-feria/", import.meta.url)));
const tipos = {
  ".html":"text/html; charset=utf-8", ".js":"text/javascript; charset=utf-8",
  ".css":"text/css; charset=utf-8", ".json":"application/json; charset=utf-8",
  ".png":"image/png", ".jpg":"image/jpeg", ".jpeg":"image/jpeg", ".webp":"image/webp",
  ".svg":"image/svg+xml", ".ico":"image/x-icon", ".woff":"font/woff", ".woff2":"font/woff2",
  ".mp4":"video/mp4", ".avif":"image/avif"
};
let cache = { hasta: 0, conectado: false };
let comprobando;
function estado() {
  if (Date.now() < cache.hasta) return Promise.resolve(cache.conectado);
  if (comprobando) return comprobando;
  comprobando = new Promise(resolve => {
    const req = https.get("https://localhost:7278/api/publico/estado", { timeout: 4000 }, res => {
      let cuerpo = "";
      res.setEncoding("utf8");
      res.on("data", fragmento => {
        cuerpo += fragmento;
        if (cuerpo.length > 1024) res.destroy();
      });
      res.on("end", () => {
        try { resolve(res.statusCode === 200 && JSON.parse(cuerpo).conectado === true); }
        catch { resolve(false); }
      });
      res.on("error", () => resolve(false));
      res.on("close", () => resolve(false));
    });
    req.on("timeout", () => req.destroy());
    req.on("error", () => resolve(false));
  }).then(conectado => {
    cache = { hasta: Date.now() + 10000, conectado };
    return conectado;
  }).finally(() => { comprobando = undefined; });
  return comprobando;
}
const server = http.createServer(async (req,res) => {
  res.setHeader("X-Content-Type-Options","nosniff");
  res.setHeader("Referrer-Policy","no-referrer");
  res.setHeader("X-Frame-Options","DENY");
  res.setHeader("Permissions-Policy","camera=(), microphone=(), geolocation=()");
  res.setHeader("Cache-Control","no-store");
  if (!["GET","HEAD"].includes(req.method)) { res.writeHead(405); return res.end(); }
  try {
    const ruta = decodeURIComponent(new URL(req.url,"http://localhost").pathname);
    if (ruta === "/api/publico/estado") {
      const conectado = await estado();
      res.writeHead(conectado ? 200 : 503, {"Content-Type":"application/json; charset=utf-8"});
      return res.end(req.method === "HEAD" ? undefined : JSON.stringify({conectado}));
    }
    // Solo archivos compilados permitidos y las dos rutas informativas.
    if (ruta.startsWith("/api") || ruta.includes("\\") || ruta.split("/").some(x => x.startsWith("."))) {
      res.writeHead(404); return res.end();
    }
    const relativa = ["/","/politica-de-cookies"].includes(ruta) ? "index.html" : ruta.slice(1);
    const tipo = tipos[path.extname(relativa).toLowerCase()];
    if (!tipo) { res.writeHead(404); return res.end(); }
    const archivo = await realpath(path.resolve(root, relativa));
    if (!archivo.startsWith(root + path.sep)) { res.writeHead(404); return res.end(); }
    const info = await stat(archivo);
    if (!info.isFile()) { res.writeHead(404); return res.end(); }
    res.writeHead(200, {"Content-Type":tipo, "Content-Length":info.size});
    if (req.method === "HEAD") return res.end();
    const stream = createReadStream(archivo);
    stream.on("error",()=>res.destroy());
    stream.pipe(res);
  } catch {
    if (!res.headersSent) res.writeHead(404);
    res.end();
  }
});
server.requestTimeout = 10000;
server.headersTimeout = 5000;
server.maxConnections = 80;
server.listen(5180,"127.0.0.1",()=>console.log("SYNCHRO feria: http://127.0.0.1:5180"));
