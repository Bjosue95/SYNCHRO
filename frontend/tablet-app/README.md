# SYNCHRO · Interfaz de tablet

Vue 3 + Vue Router + Vite. Mantiene los componentes, logotipos, iconos y colores del
proyecto. Guía de implementación y pruebas: [INTERFAZ-TABLET.md](../../docs/INTERFAZ-TABLET.md).

El menú principal conserva cuatro opciones: **Guardar**, **Retirar**,
**Tengo una reserva web** y **Cargar dispositivo**. Guardar abre `/guardar` exclusivamente para pertenencias; las encomiendas se depositan desde Tengo una reserva web.

## Ver y recorrer la demostración

```bash
npm ci
npm run demo
```

Abrir **http://localhost:4173/menu**. El modo demo muestra datos ficticios y NO llama
al backend, abre casilleros, cobra dinero ni envía WhatsApp. No necesita SQL ni certificados.
Para la cámara, usar localhost o un contexto HTTPS con permiso del navegador. También
hay una captura de ejemplo, exclusivamente en demo.

- Depósito: «Tengo una reserva web» → «Encomienda» → «Rellenar datos de ejemplo» → continuar → aceptar
  captura → usar ejemplo → confirmar casillero E01.
- Retiro de encomienda: Retirar → Encomiendas → rellenar ejemplo → verificar acceso
  → continuar → captura → PIN ficticio **654321**.
- Cargar dispositivo abre el flujo de carga. Para pruebas de pertenencias también se
  conserva `/servicios/personal`: espacio/tiempo → pago simulado → inicio.
- Frío/calor: «Tengo una reserva web» → «Reserva de frío» o «Reserva de calor» →
  «Usar reserva web de ejemplo» → consultar → confirmar objetos guardados → comenzar.
  Son reservas YA creadas y pagadas en la web; esta tablet no permite contratarlas.
  Las antiguas rutas `/servicios/frio` y `/servicios/calor` redirigen a reservas existentes.
- Retiro temporal: Retirar → Pertenencias y dispositivos → rellenar ejemplo → confirmar.

## Conectar la API

```bash
npm run dev
```

Requiere certificado PFX local mediante `SYNCHRO_CERT_PATH` y `SYNCHRO_CERT_PASSWORD`.
En Windows conserva el valor predeterminado de ruta:
`%LOCALAPPDATA%/SYNCHRO/certificados/localhost.pfx`.
La API predeterminada es **https://localhost:7278** (perfil https de launchSettings.json).
Puede configurarse con `SYNCHRO_API_TARGET`. Node debe confiar en el certificado de la API;
si corresponde, usar `NODE_EXTRA_CA_CERTS` con su certificado público PEM. No deshabilitar TLS.
La API requiere Development, SynchroDB_Pruebas, migraciones, terminal vinculada, configuración
del módulo temporal y claves HMAC. Cada referencia de rostro se registra previamente desde
la sesión web de su persona. El destinatario necesita su sesión telefónica verificada.

En modo normal no hay datos de ejemplo ni sustitución automática de fallos. Si falta API,
consentimiento, cuenta, referencia facial o PIN válido, la operación se detiene. No se
habilitan altas de encomiendas en la terminal.

## Compilación y pruebas

```bash
npm run build
npm test
npx playwright install chromium
npm run test:e2e
```

En Linux se aprovecha `/usr/bin/chromium` si existe; alternativamente configurar
`SYNCHRO_CHROMIUM` con la ruta del ejecutable. Playwright usa puertos 4173/4174 para
demo y compilación normal. Las pruebas emplean datos y vídeo sintéticos, no SQL real.

Con el demo abierto, `npm run capturas` genera imágenes de menú, modo oscuro, cámara y
carga en `docs/capturas/`. Los resultados temporales de Playwright están ignorados en Git.
`npm run preview` solo sirve la compilación; no activa las fixtures ni es un despliegue.
