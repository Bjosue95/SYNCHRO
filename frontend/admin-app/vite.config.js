import { readFileSync } from "node:fs";
import { join } from "node:path";
import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

const apiTarget = process.env.SYNCHRO_API_TARGET || "https://localhost:7278";

export default defineConfig(({ command }) => {
  let https;
  if (command === "serve") {
    const certPath = process.env.SYNCHRO_CERT_PATH ||
      join(process.env.LOCALAPPDATA || "", "SYNCHRO", "certificados", "localhost.pfx");
    try {
      https = process.env.SYNCHRO_KEY_PATH
        ? { cert: readFileSync(certPath), key: readFileSync(process.env.SYNCHRO_KEY_PATH) }
        : { pfx: readFileSync(certPath), passphrase: process.env.SYNCHRO_CERT_PASSWORD };
    } catch {
      throw new Error("No se encontró el certificado de SYNCHRO. Revisa SYNCHRO_CERT_PATH o el certificado local de la tablet.");
    }
  }
  return {
    plugins: [vue()],
    server: {
      host: "127.0.0.1",
      port: 5174,
      strictPort: true,
      https,
      proxy: {
        "/api": {
          target: apiTarget,
          changeOrigin: true,
          secure: false, // Solo para el certificado HTTPS de desarrollo del backend.
        },
      },
    },
  };
});