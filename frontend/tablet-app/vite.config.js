import { fileURLToPath, URL } from "node:url";

import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import vueDevTools from "vite-plugin-vue-devtools";
import { readFileSync } from "node:fs";
import { join } from "node:path";

const apiTarget = process.env.SYNCHRO_API_TARGET || "http://localhost:5229";

// El certificado solo se carga al servir la tablet; compilar no requiere
// acceso al certificado privado de la laptop.
export default defineConfig(({ command }) => {
  let https;
  if (command === "serve") {
    const certificado = process.env.SYNCHRO_CERT_PATH ||
      (process.env.LOCALAPPDATA && join(process.env.LOCALAPPDATA, "SYNCHRO", "certificados", "localhost.pfx"));
    if (!certificado) throw new Error("Configura SYNCHRO_CERT_PATH para servir la tablet por HTTPS.");
    https = { pfx: readFileSync(certificado), passphrase: process.env.SYNCHRO_CERT_PASSWORD };
  }
  return {
    server: {
      https,

      proxy: {
        "/api": {
          target: apiTarget,
          changeOrigin: true,
        },
      },
    },

    plugins: [vue(), vueDevTools()],
    resolve: {
      alias: {
        "@": fileURLToPath(new URL("./src", import.meta.url)),
      },
    },
  };
});
