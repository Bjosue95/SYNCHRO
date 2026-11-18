import { test, expect } from "@playwright/test";

// Estas pruebas cubren la tablet actual. Las rutas de servicios y
// encomiendas del prototipo anterior no deben reintroducirse.
const casilleros = [
  { codigo: "S01", tamano: "Pequeno", tipo: "Normal", estado: "Disponible" },
  { codigo: "M03", tamano: "Mediano", tipo: "Normal", estado: "Disponible" },
  { codigo: "M04", tamano: "Mediano", tipo: "Normal", estado: "Disponible" },
  { codigo: "M06", tamano: "Mediano", tipo: "Normal", estado: "Disponible" },
  { codigo: "M07", tamano: "Mediano", tipo: "Normal", estado: "Ocupado" },
  { codigo: "L05", tamano: "Grande", tipo: "Normal", estado: "Disponible" },
];

async function simularDisponibilidad(page, datos = casilleros, estado = 200) {
  await page.route("**/api/terminales/casilleros-prototipo", route =>
    route.fulfill({
      status: estado,
      contentType: "application/json",
      body: JSON.stringify(datos)
    })
  );
}

for (const viewport of [
  { width: 1280, height: 900 },
  { width: 768, height: 1024 },
  { width: 390, height: 844 }
]) {
  test("menú y tarjetas reales sin desbordamiento " + viewport.width, async ({ page }) => {
    const errores = [];
    page.on("pageerror", error => errores.push(error.message));
    await page.setViewportSize(viewport);
    await page.goto("/menu");

    await expect(page.getByRole("heading", {
      name: "¿Qué deseas hacer?"
    })).toBeVisible();

    await expect(page.locator(".servicios .tarjeta-servicio")).toHaveCount(4);
    await expect(page.locator(".servicios h2")).toHaveText([
      "Guardar", "Retirar", "Tengo una reserva", "Cargar dispositivo"
    ]);

    // Solo Guardar y Retirar tienen una ruta implementada.
    await expect(page.locator(".servicios button")).toHaveCount(2);
    await expect(page.locator(".servicios > div.tarjeta-servicio")).toHaveCount(2);
    for (const boton of await page.locator(".servicios button").all()) {
      await expect(boton).toBeEnabled();
    }
    await expect(page.locator(".servicios .tarjeta-servicio").nth(3)
      .locator("img.icono-claro"))
      .toHaveAttribute("src", "/iconos/04_carga_inalambrica.svg");

    expect(await page.evaluate(() =>
      document.documentElement.scrollWidth <= window.innerWidth
    )).toBe(true);
    expect(errores).toEqual([]);
  });
}

test("modo oscuro y ayuda siguen disponibles", async ({ page }) => {
  await page.goto("/menu");
  await page.getByRole("button", { name: "Activar modo oscuro" }).click();
  await expect(page.locator(".synchro-app")).toHaveClass(/tema-oscuro/);

  await page.getByRole("button", { name: "? Ayuda", exact: true }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.getByText("¿Cómo envío una encomienda?").click();
  await expect(page.getByRole("dialog")).toContainText(
    "cuenta de cliente en la web"
  );
  await page.getByRole("button", { name: "Cerrar ayuda" }).click();

  await page.getByRole("button", { name: "Activar modo claro" }).click();
  await expect(page.locator(".synchro-app")).not.toHaveClass(/tema-oscuro/);
});

test("rutas antiguas no abren operaciones inexistentes", async ({ page }) => {
  for (const ruta of [
    "/terminal/encomiendas/reserva",
    "/terminal/encomiendas/retiro",
    "/guardar/encomienda-visitante",
    "/servicios/personal",
    "/servicios/carga",
    "/servicios/frio",
    "/servicios/calor",
    "/terminal/reserva-web"
  ]) {
    await page.goto(ruta);
    await expect(page).toHaveURL(/\/menu$/);
    await expect(page.getByRole("heading", {
      name: "¿Qué deseas hacer?"
    })).toBeVisible();
  }
});

test("Guardar exige seleccionar pertenencia antes de continuar", async ({ page }) => {
  await page.goto("/menu");
  await page.locator(".servicios button").first().click();
  await expect(page).toHaveURL(/\/guardar$/);
  await expect(page.locator(".cuadricula .tarjeta")).toHaveCount(4);

  const continuar = page.getByRole("button", { name: "Continuar", exact: true });
  await expect(continuar).toBeDisabled();

  await page.getByRole("button", { name: /Mochila o bolso/ }).click();
  await expect(page.getByRole("button", { name: /Mochila o bolso/ }))
    .toHaveAttribute("aria-pressed", "true");
  await expect(continuar).toBeEnabled();
  await continuar.click();
  await expect(page).toHaveURL(/\/guardar\/tamano\?objeto=mochila$/);
});

test("tamaños permitidos y disponibilidad del servidor", async ({ page }) => {
  await simularDisponibilidad(page);
  await page.goto("/guardar/tamano?objeto=mochila");
  await expect(page.getByRole("heading", {
    name: "Selecciona el tamaño del casillero"
  })).toBeVisible();
  await expect(page.locator(".tarjeta-tamano")).toHaveCount(2);
  const continuar = page.getByRole("button", { name: "Continuar", exact: true });
  await expect(continuar).toBeDisabled();

  const mediano = page.locator(".tarjeta-tamano").filter({ hasText: "Mediano" });
  await expect(mediano).toBeEnabled();
  await mediano.click();
  await expect(mediano).toHaveAttribute("aria-pressed", "true");
  await expect(continuar).toBeEnabled();
  await continuar.click();
  await expect(page).toHaveURL(/\/guardar\/casillero\?objeto=mochila&tamano=M$/);
});

test("sin casilleros normales disponibles no se puede continuar", async ({ page }) => {
  await simularDisponibilidad(page, [
    { codigo: "M03", tamano: "Mediano", tipo: "Normal", estado: "Ocupado" },
    { codigo: "L05", tamano: "Grande", tipo: "Especial", estado: "Disponible" }
  ]);
  await page.goto("/guardar/tamano?objeto=mochila");
  await expect(page.locator(".tarjeta-tamano")).toHaveCount(2);
  for (const boton of await page.locator(".tarjeta-tamano").all()) {
    await expect(boton).toBeDisabled();
  }
  await expect(page.getByRole("button", { name: "Continuar", exact: true }))
    .toBeDisabled();
});

test("error del API no fabrica disponibilidad", async ({ page }) => {
  await simularDisponibilidad(page, { mensaje: "Falla simulada" }, 503);
  await page.goto("/guardar/tamano?objeto=mochila");
  await expect(page.getByText(/No se pudo consultar la disponibilidad/))
    .toBeVisible();
  await expect(page.getByRole("button", { name: "Continuar", exact: true }))
    .toBeDisabled();
  await expect(page.getByRole("button", { name: "Reintentar" }))
    .toBeVisible();
});

test("casilleros excluye los ocupados y códigos no autorizados", async ({ page }) => {
  await simularDisponibilidad(page);
  await page.goto("/guardar/casillero?objeto=mochila&tamano=M");
  await expect(page.getByRole("heading", { name: "Elige tu casillero" }))
    .toBeVisible();
  await expect(page.locator(".cuadricula-casilleros .casillero"))
    .toHaveCount(2);
  await expect(page.locator(".cuadricula-casilleros")).not.toContainText("M04");
  await expect(page.locator(".cuadricula-casilleros")).not.toContainText("M07");

  const continuar = page.getByRole("button", { name: "Continuar", exact: true });
  await expect(continuar).toBeDisabled();
  await page.locator(".casillero").filter({ hasText: "M03" }).click();
  await expect(continuar).toBeEnabled();
  await expect(page.locator(".pie")).toContainText("Aún no está reservado");
  await continuar.click();
  await expect(page).toHaveURL(/\/guardar\/verificar-rostro\?.*casillero=M03/);
});

test("Retirar no activa la encomienda no implementada", async ({ page }) => {
  await page.goto("/retirar");
  await expect(page.getByRole("heading", { name: "¿Qué deseas retirar?" }))
    .toBeVisible();
  await expect(page.locator(".cuadricula .tarjeta")).toHaveCount(2);
  await expect(page.getByRole("button", {
    name: "Una encomienda, no disponible"
  })).toBeDisabled();

  const continuar = page.getByRole("button", { name: "Continuar", exact: true });
  await expect(continuar).toBeDisabled();
  await page.getByRole("button", { name: "Mis pertenencias" }).click();
  await expect(continuar).toBeEnabled();
  await continuar.click();
  await expect(page).toHaveURL(/\/retirar\/rostro$/);
  await expect(page.getByRole("heading", { name: "Registro facial" }))
    .toBeVisible();
});

test("PIN directo exige captura facial previa", async ({ page }) => {
  await page.goto("/retirar/pin");
  await expect(page).toHaveURL(/\/retirar\/rostro$/);
  await expect(page.getByRole("heading", { name: "Registro facial" }))
    .toBeVisible();
});

test("modo demo no publica el catálogo de desarrollo normal", async ({ page }) => {
  await page.goto("/vista-previa");
  await expect(page).toHaveURL(/\/menu$/);
});

test("compilación E2E no publica el catálogo ni autoriza sin sesión", async ({ page }) => {
  await page.goto("http://127.0.0.1:4174/vista-previa");
  await expect(page).toHaveURL(/\/menu$/);

  await page.goto("http://127.0.0.1:4174/retirar/pago/resumen");
  await expect(page).toHaveURL(/\/retirar\/rostro$/);
});

test("compilación E2E no simula respuesta exitosa si el API falla", async ({ page }) => {
  await page.route("**/api/terminales/casilleros-prototipo", route =>
    route.fulfill({
      status: 503,
      contentType: "application/json",
      body: JSON.stringify({ mensaje: "API no disponible" })
    })
  );
  await page.goto("http://127.0.0.1:4174/guardar/tamano?objeto=mochila");
  await expect(page.getByText(/No se pudo consultar la disponibilidad/))
    .toBeVisible();
  await expect(page.getByRole("button", { name: "Continuar", exact: true }))
    .toBeDisabled();
});
