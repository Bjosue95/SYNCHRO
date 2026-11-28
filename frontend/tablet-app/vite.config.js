import { fileURLToPath, URL } from "node:url";

import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import vueDevTools from "vite-plugin-vue-devtools";
import { readFileSync } from "node:fs";
import { join } from "node:path";

const apiTarget = process.env.SYNCHRO_API_TARGET || "https://localhost:7278";

// El certificado solo se carga al servir la tablet; compilar no requiere
// acceso al certificado privado de la laptop.
export default defineConfig(({ command, mode, isPreview }) => {
  let https;
  if (command === "serve" && !isPreview && mode !== "demo") {
    const certificado =
      process.env.SYNCHRO_CERT_PATH ||
      (process.env.LOCALAPPDATA &&
        join(
          process.env.LOCALAPPDATA,
          "SYNCHRO",
          "certificados",
          "localhost.pfx",
        ));
    if (!certificado)
      throw new Error(
        "Configura SYNCHRO_CERT_PATH para servir la tablet por HTTPS.",
      );
    https = process.env.SYNCHRO_KEY_PATH ? { cert: readFileSync(certificado), key: readFileSync(process.env.SYNCHRO_KEY_PATH) } : {
      pfx: readFileSync(certificado),
      passphrase: process.env.SYNCHRO_CERT_PASSWORD,
    };
  }
  const definitiva = process.env.SYNCHRO_TABLET_DEFINITIVA === "1";
  let host, clave;
  if (definitiva && command === "serve" && !isPreview) {
    // Tailscale Serve termina HTTPS en el nombre de la tailnet y reenvia
    // al servidor local. No exponer el proxy autenticado directamente a la red.
    host = "127.0.0.1";
    if (!https) throw new Error("La tablet definitiva exige HTTPS.");
    if (apiTarget !== "https://localhost:7278")
      throw new Error("La tablet definitiva requiere el backend local.");
    clave = readFileSync(join(process.env.LOCALAPPDATA, "SYNCHRO", "Definitivo", "tablet-proxy.key"), "utf8").trim();
    if (!/^[0-9a-fA-F]{64}$/.test(clave)) throw new Error("Credencial de estacion invalida.");
  }
  return {
    server: {
      ...(definitiva && host ? {host, port:5173, strictPort:true} : {}),
      https,

      proxy: {
        "/api": {
          target: apiTarget,
          changeOrigin: true,
          secure: false,
          ...(definitiva && clave ? { configure(proxy) {
            proxy.on("proxyReq", (req, incoming) => {
              req.removeHeader("X-Synchro-Estacion-Privada");
              if ((incoming.url || "").startsWith("/api/terminales/"))
                req.setHeader("X-Synchro-Estacion-Privada", clave);
            });
          } } : {}),
        },
      },
    },

    plugins: [vue(), ...(mode === "demo" ? [] : [vueDevTools()])],
    resolve: {
      alias: {
        "@": fileURLToPath(new URL("./src", import.meta.url)),
      },
    },
  };
});
