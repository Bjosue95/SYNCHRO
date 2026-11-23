import { readFileSync } from "node:fs";
import { join } from "node:path";
import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

const apiTarget = process.env.SYNCHRO_API_TARGET || "https://localhost:7278";

// Certificado compartido solo en desarrollo. No se copia al repositorio.
export default defineConfig(({ command }) => {
  let https;
  if (command === "serve") {
    const certPath = process.env.SYNCHRO_CERT_PATH ||
      join(process.env.LOCALAPPDATA || "", "SYNCHRO", "certificados", "localhost.pfx");
    try {
      https = {
        pfx: readFileSync(certPath),
        passphrase: process.env.SYNCHRO_CERT_PASSWORD
      };
    } catch {
      throw new Error(
        "No se encontró el certificado HTTPS local de SYNCHRO. " +
        "Revisa SYNCHRO_CERT_PATH o la configuración compartida de la tablet."
      );
    }
  }

  return {
    plugins: [vue()],
    server: {
      host: "127.0.0.1",
      port: 5175,
      strictPort: true,
      https,
      proxy: {
        "/api": {
          target: apiTarget,
          changeOrigin: true,
          secure: false // Únicamente certificado local del backend en desarrollo.
        }
      }
    }
  };
});
