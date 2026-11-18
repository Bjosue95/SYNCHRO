import { test, expect } from "@playwright/test";

const BASE = "http://127.0.0.1:4174";
const COBRO_ID = "11111111-1111-4111-8111-111111111111";

test("rostro, PIN, pago administrativo y retiro", async ({
  page, context
}) => {
  test.setTimeout(60000);

  const estado = {
    confirmado: false,
    retirado: false,
    solicitudes: []
  };

  // Cámara y detector simulados. Sin imágenes personales.
  await page.addInitScript(() => {
    window.Human = class {
      async load() {}
      async warmup() {}
      async detect() {
        return {
          width: 640,
          height: 480,
          face: [{ box: [180, 80, 240, 280] }]
        };
      }
    };

    Object.defineProperty(
      navigator.mediaDevices,
      "getUserMedia",
      {
        configurable: true,
        value: async () => {
          const canvas = document.createElement("canvas");
          canvas.width = 640;
          canvas.height = 480;

          window.__camaraPrueba = canvas;

          const ctx = canvas.getContext("2d");

          function pintar() {
            ctx.fillStyle = "#6f9eb5";
            ctx.fillRect(0, 0, 640, 480);
            requestAnimationFrame(pintar);
          }

          pintar();
          return canvas.captureStream(15);
        }
      }
    );
  });

  await context.route("**/api/**", async route => {
    const solicitud = route.request();
    const url = new URL(solicitud.url()).pathname;

    estado.solicitudes.push(url);

    const responder = (status, datos) =>
      route.fulfill({
        status,
        contentType: "application/json",
        body: JSON.stringify(datos)
      });

    if (
      url ===
      "/api/terminales/guardar/whatsapp/csrf-token" ||
      url === "/api/admin/csrf-token"
    ) {
      return responder(200, {
        csrfToken: "csrf-prueba"
      });
    }

    if (
      url ===
      "/api/terminales/guardar/whatsapp/retiro/rostro"
    ) {
      return responder(200, {
        verificacionSimulada: true,
        tokenFacial: "token-facial-prueba"
      });
    }

    if (
      url ===
      "/api/terminales/guardar/whatsapp/retiro/pin/prevalidar"
    ) {
      const datos = solicitud.postDataJSON();

      if (datos.pin !== "123456") {
        return responder(400, {
          mensaje: "PIN incorrecto."
        });
      }

      return responder(200, {
        pinValidado: true,
        pagoPendiente: true,
        retiroConfirmado: false,
        autorizacionPago: "autorizacion-temporal-prueba"
      });
    }

    if (
      url ===
      "/api/terminales/guardar/whatsapp/retiro/cobro/preparar"
    ) {
      return responder(200, {
        cobroId: COBRO_ID,
        codigoCasillero: "M01",
        importeHnl: 1,
        minutosUtilizados: 60,
        descripcionTarifa: "Tarifa ficticia de prueba",
        estadoPago: "Pendiente",
        pagoConfirmado: false,
        retiroConfirmado: false
      });
    }

    if (
      url ===
      "/api/terminales/guardar/whatsapp/retiro/cobro/finalizar"
    ) {
      if (!estado.confirmado) {
        return responder(409, {
          mensaje: "El pago simulado aun no esta confirmado."
        });
      }

      estado.retirado = true;

      return responder(200, {
        retiroConfirmado: true,
        pagoAplicado: true,
        estadoCasillero: "Disponible",
        codigoCasillero: "M01"
      });
    }

    if (url === "/api/admin/mi-sesion") {
      return responder(200, {
        rol: "Operador",
        roles: ["Operador"]
      });
    }

    if (url === "/api/admin/whatsapp-manual/pendientes") {
      return responder(200, {
        operaciones: []
      });
    }

    if (
      url ===
      "/api/admin/whatsapp-manual/cobros/retiro/pendientes"
    ) {
      return responder(200, {
        cobros: estado.confirmado ? [] : [{
          cobroId: COBRO_ID,
          codigoCasillero: "M01",
          minutosUtilizados: 60,
          importeHnl: 1,
          descripcionTarifa: "Tarifa ficticia de prueba",
          creadoUtc: "2026-11-16T20:00:00Z"
        }]
      });
    }

    if (
      url ===
      "/api/admin/whatsapp-manual/cobros/retiro/confirmar"
    ) {
      const datos = solicitud.postDataJSON();

      if (
        datos.cobroId !== COBRO_ID ||
        datos.metodoPago !== "Efectivo" ||
        datos.pagoComprobado !== true
      ) {
        return responder(400, {
          mensaje: "Confirmacion invalida."
        });
      }

      estado.confirmado = true;

      return responder(200, {
        cobroId: COBRO_ID,
        estadoPago: "ConfirmadoSimulado",
        pagoConfirmado: true,
        retiroConfirmado: false
      });
    }

    return responder(503, {
      mensaje: "Endpoint no simulado."
    });
  });

  // 1. Verificación facial simulada.
  await page.goto(`${BASE}/retirar/rostro`);

  await page.getByRole("button", {
    name: "Comenzar escaneo"
  }).click();

  await expect(page.getByRole("heading", {
    name: "Rostro capturado"
  })).toBeVisible({ timeout: 15000 });

  await page.getByRole("button", {
    name: "Continuar",
    exact: true
  }).click();

  await expect(page).toHaveURL(/\/retirar\/pin$/);

  // 2. PIN incorrecto: introducirlo con el teclado virtual real.
  for (const digito of "000000") {
    await page.getByRole("button", {
      name: digito, exact: true
    }).click();
  }
  await expect(page.getByRole("button", {
    name: "Verificar PIN"
  })).toBeEnabled();
  await page.getByRole("button", {
    name: "Verificar PIN"
  }).click();

  await expect(
    page.locator(".pantalla-pin .mensaje")
  ).toContainText("PIN incorrecto");

  await expect(page).toHaveURL(/\/retirar\/pin$/);

  // 3. PIN correcto: prepara cobro, no retira.
  for (const digito of "123456") {
    await page.getByRole("button", {
      name: digito, exact: true
    }).click();
  }
  await expect(page.getByRole("button", {
    name: "Verificar PIN"
  })).toBeEnabled();
  await page.getByRole("button", {
    name: "Verificar PIN"
  }).click();

  // La tablet permite elegir un metodo sin confirmar el cobro.
  await expect(page).toHaveURL(/\/retirar\/pago$/);
  await expect(page.getByRole("heading", {
    name: "¿Cómo deseas pagar?"
  })).toBeVisible();
  await page.locator(".celda-metodo").first().click();
  await page.getByRole("button", {
    name: "Continuar"
  }).click();

  await expect(page).toHaveURL(/\/retirar\/pago\/resumen$/);
  await expect(page.getByRole("heading", {
    name: "Resumen de tu consumo"
  })).toBeVisible();

  expect(estado.retirado).toBe(false);

  // 4. Intento de retiro antes del pago: rechazado.
  await page.getByRole("button", {
    name: "Verificar pago y retirar"
  }).click();

  await expect(page.getByText(
    /El pago simulado aun no esta confirmado/
  )).toBeVisible();

  expect(estado.retirado).toBe(false);

  // 5. El operador confirma el pago.
  const operador = await context.newPage();

  await operador.goto(`${BASE}/admin/whatsapp-manual`);

  await expect(operador.getByRole("heading", {
    name: "Cobros pendientes de retiro personal"
  })).toBeVisible();

  await operador.getByRole("checkbox", {
    name: /Confirmo que he comprobado/
  }).check();

  operador.once("dialog", dialog => dialog.accept());

  await operador.getByRole("button", {
    name: "Confirmar pago simulado"
  }).click();

  await expect(operador.getByText(
    /confirmado y auditado/
  )).toBeVisible();

  expect(estado.confirmado).toBe(true);
  expect(estado.retirado).toBe(false);

  // 6. La tablet finaliza después de la confirmación.
  await page.getByRole("button", {
    name: "Verificar pago y retirar"
  }).click();

  await expect(page.getByRole("heading", {
    name: "Retiro completado"
  })).toBeVisible();

  expect(estado.retirado).toBe(true);

  // La tablet jamás utilizó el endpoint antiguo
  // de retiro directo.
  expect(estado.solicitudes).not.toContain(
    "/api/terminales/guardar/whatsapp/retiro/pin"
  );

  await operador.close();
});