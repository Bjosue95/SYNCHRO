import { fileURLToPath, URL } from "node:url";

import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import vueDevTools from "vite-plugin-vue-devtools";
import { readFileSync } from "node:fs";
import { join } from "node:path";

const apiTarget = process.env.SYNCHRO_API_TARGET || "http://localhost:5229";

// https://vite.dev/config/
export default defineConfig({
  server: {
    https: {
      pfx: readFileSync(
        join(
          process.env.LOCALAPPDATA,
          "SYNCHRO",
          "certificados",
          "localhost.pfx",
        ),
      ),
      passphrase: process.env.SYNCHRO_CERT_PASSWORD,
    },

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
});
