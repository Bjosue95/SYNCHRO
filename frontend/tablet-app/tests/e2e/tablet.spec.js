import { test, expect } from "@playwright/test";

for (const viewport of [
  { width: 1280, height: 900 },
  { width: 768, height: 1024 },
  { width: 390, height: 844 },
]) {
  test(`menú accesible sin desbordamiento ${viewport.width}`, async ({
    page,
  }) => {
    const errores = [];
    page.on("pageerror", (e) => errores.push(e.message));
    await page.setViewportSize(viewport);
    await page.goto("/menu");
    await expect(
      page.getByRole("heading", { name: "¿Qué deseas hacer?" }),
    ).toBeVisible();
    await expect(page.locator(".tablet-grid a")).toHaveCount(6);
    for (const tarjeta of await page.locator(".tablet-grid a").all())
      await expect(tarjeta).toHaveAttribute("href", /\//);
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true);
    expect(
      await page
        .locator("img")
        .evaluateAll((xs) => xs.every((x) => x.complete && x.naturalWidth > 0)),
    ).toBe(true);
    expect(errores).toEqual([]);
  });
}
test("tema oscuro, ayuda y ruta visitante conducen a reserva existente", async ({
  page,
}) => {
  await page.goto("/menu");
  await page.getByRole("button", { name: "Activar modo oscuro" }).click();
  await expect(page.locator(".tablet-layout")).toHaveClass(/tema-oscuro/);
  await page.getByRole("button", { name: "? Ayuda", exact: true }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.getByText("¿Cómo envío una encomienda?").click();
  await expect(page.getByRole("dialog")).toContainText(
    "cuenta de cliente en la web",
  );
  await page.getByRole("button", { name: "Cerrar ayuda" }).click();
  await page.goto("/guardar/encomienda-visitante");
  await expect(page).toHaveURL(/\/terminal\/encomiendas\/reserva$/);
  await expect(page.getByLabel("Teléfono del destinatario")).toHaveCount(0);
});
async function prepararDeposito(page) {
  await page.goto("/terminal/encomiendas/reserva");
  await page.getByRole("button", { name: "Rellenar datos de ejemplo" }).click();
  await page.getByRole("button", { name: "Continuar →", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Verifiquemos al remitente" }),
  ).toBeVisible();
}
async function muestra(page) {
  await page.getByRole("checkbox").check();
  await page.getByRole("button", { name: "Usar captura de ejemplo" }).click();
}
test("depósito de encomienda con captura y sin pedir de nuevo el teléfono", async ({
  page,
}) => {
  await prepararDeposito(page);
  await expect(
    page.getByRole("button", { name: "Usar captura de ejemplo" }),
  ).toBeDisabled();
  await muestra(page);
  await expect(
    page.getByRole("heading", { name: "Confirma el casillero" }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Confirmar depósito", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Depósito registrado", exact: true }),
  ).toBeVisible();
  await expect(page.locator("main")).toContainText("No se enviaron mensajes");
  await expect(page.getByLabel("Credencial de tu reserva")).toHaveCount(0);
});
test("retira encomienda solo con acceso, captura y PIN correcto", async ({
  page,
}) => {
  await page.goto("/terminal/encomiendas/retiro");
  await page.getByRole("button", { name: "Rellenar datos de ejemplo" }).click();
  await page.getByRole("button", { name: "Verificar mi acceso" }).click();
  await page.getByRole("button", { name: "Continuar →", exact: true }).click();
  await muestra(page);
  await page.getByLabel("PIN recibido por WhatsApp").fill("000000");
  await page
    .getByRole("button", { name: "Confirmar retiro", exact: true })
    .click();
  await expect(page.getByRole("alert")).toContainText("PIN incorrecto");
  await expect(
    page.getByRole("heading", { name: "Retiro registrado", exact: true }),
  ).toHaveCount(0);
  await page.getByLabel("PIN recibido por WhatsApp").fill("654321");
  await page
    .getByRole("button", { name: "Confirmar retiro", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Retiro registrado", exact: true }),
  ).toBeVisible();
});
test("cámara real del navegador detiene las pistas después de capturar", async ({
  page,
}) => {
  await page.addInitScript(() => {
    const original = navigator.mediaDevices.getUserMedia.bind(
      navigator.mediaDevices,
    );
    navigator.mediaDevices.getUserMedia = async (c) => {
      const s = await original(c);
      window.pistasCaptura = s.getTracks();
      return s;
    };
  });
  await prepararDeposito(page);
  await page.getByRole("checkbox").check();
  await page
    .getByRole("button", { name: "Activar cámara", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Capturar y verificar" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Capturar y verificar" }).click();
  await expect(
    page.getByRole("heading", { name: "Confirma el casillero" }),
  ).toBeVisible();
  expect(
    await page.evaluate(() =>
      window.pistasCaptura.every((t) => t.readyState === "ended"),
    ),
  ).toBe(true);
});
test("permiso de cámara denegado no permite avanzar", async ({ page }) => {
  await page.addInitScript(() => {
    navigator.mediaDevices.getUserMedia = async () => {
      throw new DOMException("Permiso denegado", "NotAllowedError");
    };
  });
  await prepararDeposito(page);
  await page.getByRole("checkbox").check();
  await page
    .getByRole("button", { name: "Activar cámara", exact: true })
    .click();
  await expect(page.getByRole("alert")).toContainText("No pudimos acceder");
  await expect(
    page.getByRole("heading", { name: "Confirma el casillero" }),
  ).toHaveCount(0);
});
for (const tipo of ["personal", "carga", "frio", "calor"]) {
  test(`reserva, prepago e inicio de ${tipo}`, async ({ page }) => {
    await page.goto(`/servicios/${tipo}`);
    await expect(page.locator(".compartimento").first()).toBeVisible();
    await page.getByRole("button", { name: "60 min", exact: true }).click();
    await page.getByRole("button", { name: "Continuar al pago →" }).click();
    await expect(page.locator("main")).toContainText("60 minutos de uso");
    await page.getByRole("button", { name: "Confirmar pago simulado" }).click();
    await page
      .getByRole("checkbox", {
        name: "He colocado mis objetos y cerrado la puerta.",
      })
      .check();
    await page.getByRole("button", { name: "Comenzar tiempo de uso" }).click();
    await expect(
      page.getByRole("heading", { name: /Todo listo en/ }),
    ).toBeVisible();
    await page
      .getByText("Ver datos necesarios para recuperar mis objetos")
      .click();
    await expect(page.locator(".recovery-details")).toContainText(
      "Credencial:",
    );
    await page
      .getByRole("link", { name: "Ya guardé mis datos · Finalizar" })
      .click();
    await expect(
      page.getByRole("heading", { name: "¿Qué deseas hacer?" }),
    ).toBeVisible();
    expect(await page.locator("body").innerText()).not.toContain("654321");
  });
}
test("laptop requiere confirmar que está cerrada", async ({ page }) => {
  await page.goto("/servicios/carga");
  await page.getByRole("button", { name: /L01/ }).click();
  await page.getByRole("button", { name: "Continuar al pago →" }).click();
  await page.getByRole("button", { name: "Confirmar pago simulado" }).click();
  await page
    .getByRole("checkbox", {
      name: "He colocado mis objetos y cerrado la puerta.",
    })
    .check();
  await page.getByRole("button", { name: "Comenzar tiempo de uso" }).click();
  await expect(page.getByRole("alert")).toContainText("laptop está cerrada");
  await page.getByRole("checkbox", { name: "La laptop está cerrada." }).check();
  await page.getByRole("button", { name: "Comenzar tiempo de uso" }).click();
  await expect(
    page.getByRole("heading", { name: "Todo listo en L01" }),
  ).toBeVisible();
});
test("cancelar libera el espacio y permite una nueva selección", async ({
  page,
}) => {
  await page.goto("/servicios/personal");
  await page.getByRole("button", { name: "Continuar al pago →" }).click();
  await page
    .getByRole("button", { name: "Cancelar reserva", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "1. Elige tu compartimento" }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Continuar al pago →" }),
  ).toBeEnabled();
});
test("retiro temporal no muestra PIN ni credencial al finalizar", async ({
  page,
}) => {
  await page.goto("/retirar/servicios");
  await page.getByRole("button", { name: "Rellenar datos de ejemplo" }).click();
  await page
    .getByRole("button", { name: "Confirmar retiro", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Retiro registrado", exact: true }),
  ).toBeVisible();
  await expect(page.getByLabel("PIN de retiro", { exact: true })).toHaveCount(
    0,
  );
  await expect(
    page.getByLabel("Credencial privada", { exact: true }),
  ).toHaveCount(0);
});
test("compilación normal no activa demo ni aprueba una reserva si falla la API", async ({
  page,
}) => {
  await page.route("**/api/**", (r) =>
    r.fulfill({
      status: 503,
      contentType: "application/json",
      body: '{"mensaje":"No disponible"}',
    }),
  );
  await page.goto("http://127.0.0.1:4174/terminal/encomiendas/reserva");
  await expect(
    page.getByRole("button", { name: "Rellenar datos de ejemplo" }),
  ).toHaveCount(0);
  await expect(
    page.getByText("Demostración · datos ficticios", { exact: true }),
  ).toHaveCount(0);
  await page.getByLabel("Número de reserva", { exact: true }).fill("1");
  await page
    .getByLabel("Credencial de tu reserva", { exact: false })
    .fill("A".repeat(64));
  await page.getByRole("button", { name: "Continuar →", exact: true }).click();
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Verifiquemos al remitente" }),
  ).toHaveCount(0);
});
