import { test, expect } from "@playwright/test";

// Pruebas de interfaz con respuestas simuladas.
// No se conecta a SQL Server ni confirma dinero real.

const COBRO_ID = "11111111-1111-4111-8111-111111111111";

async function prepararServidorSimulado(page, rechazar = false) {
  const estado = {
    confirmado: false,
    confirmaciones: []
  };

  await page.route("**/api/**", async (route) => {
    const solicitud = route.request();
    const url = new URL(solicitud.url()).pathname;

    const responder = (status, datos) =>
      route.fulfill({
        status,
        contentType: "application/json",
        body: JSON.stringify(datos)
      });

    if (url === "/api/admin/mi-sesion") {
      return responder(200, {
        rol: "Operador",
        roles: ["Operador"]
      });
    }

    if (url === "/api/admin/csrf-token") {
      return responder(200, {
        csrfToken: "csrf-solo-pruebas"
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
        cobros: estado.confirmado ? [] : [
          {
            cobroId: COBRO_ID,
            codigoCasillero: "M01",
            minutosUtilizados: 60,
            importeHnl: 1,
            descripcionTarifa: "Tarifa ficticia de prueba",
            creadoUtc: "2026-11-16T20:00:00Z"
          }
        ]
      });
    }

    if (
      url ===
      "/api/admin/whatsapp-manual/cobros/retiro/confirmar"
    ) {
      const datos = solicitud.postDataJSON();

      estado.confirmaciones.push({
        datos,
        csrf: solicitud.headers()["x-csrf-token"]
      });

      if (rechazar) {
        return responder(409, {
          mensaje: "El cobro ya no permite confirmacion."
        });
      }

      if (
        datos.cobroId !== COBRO_ID ||
        datos.metodoPago !== "Efectivo" ||
        datos.pagoComprobado !== true ||
        solicitud.headers()["x-csrf-token"] !==
          "csrf-solo-pruebas"
      ) {
        return responder(400, {
          mensaje: "Solicitud de prueba incorrecta."
        });
      }

      estado.confirmado = true;

      return responder(200, {
        cobroId: COBRO_ID,
        codigoCasillero: "M01",
        importeHnl: 1,
        metodoPago: "EfectivoSimulado",
        estadoPago: "ConfirmadoSimulado",
        pagoConfirmado: true,
        pagoSimulado: true,
        retiroConfirmado: false
      });
    }

    // No fabricar exitos para rutas desconocidas.
    return responder(503, {
      mensaje: "Endpoint no simulado."
    });
  });

  return estado;
}

test("el operador comprueba y confirma un cobro simulado", async ({
  page
}) => {
  const estado = await prepararServidorSimulado(page);

  await page.goto(
    "http://127.0.0.1:4174/admin/whatsapp-manual"
  );

  await expect(
    page.getByRole("heading", {
      name: "Cobros pendientes de retiro personal"
    })
  ).toBeVisible();

  await expect(
    page.getByRole("heading", {
      name: "Casillero M01"
    })
  ).toBeVisible();

  const boton = page.getByRole("button", {
    name: "Confirmar pago simulado"
  });

  // Sin comprobar el pago, no se puede confirmar.
  await expect(boton).toBeDisabled();
  expect(estado.confirmaciones).toHaveLength(0);

  await page.getByRole("checkbox", {
    name: /Confirmo que he comprobado/
  }).check();

  await expect(boton).toBeEnabled();

  page.once("dialog", (dialog) => dialog.accept());

  await boton.click();

  await expect(
    page.getByText(/confirmado y auditado/)
  ).toBeVisible();

  await expect(
    page.getByText(
      "No hay cobros pendientes de confirmación."
    )
  ).toBeVisible();

  expect(estado.confirmaciones).toHaveLength(1);

  expect(estado.confirmaciones[0].datos).toEqual({
    cobroId: COBRO_ID,
    metodoPago: "Efectivo",
    pagoComprobado: true
  });

  expect(estado.confirmaciones[0].csrf)
    .toBe("csrf-solo-pruebas");
});

test("un pago rechazado conserva el cobro pendiente", async ({
  page
}) => {
  const estado = await prepararServidorSimulado(page, true);

  await page.goto(
    "http://127.0.0.1:4174/admin/whatsapp-manual"
  );

  const boton = page.getByRole("button", {
    name: "Confirmar pago simulado"
  });

  await page.getByRole("checkbox", {
    name: /Confirmo que he comprobado/
  }).check();

  page.once("dialog", (dialog) => dialog.accept());

  await boton.click();

  await expect(
    page.getByRole("alert")
  ).toContainText("El cobro ya no permite confirmacion.");

  await expect(
    page.getByRole("heading", {
      name: "Casillero M01"
    })
  ).toBeVisible();

  expect(estado.confirmaciones).toHaveLength(1);
  expect(estado.confirmado).toBe(false);
});