import { test, expect } from "@playwright/test";

const BASE = process.env.SYNCHRO_TABLET_URL || "https://localhost:5173";

// Es únicamente la ruta de desarrollo de la tablet: no se reciben pagos reales.
test.use({ ignoreHTTPSErrors: true });

for (const metodo of ["Efectivo", "Tarjeta", "NFC"]) {
  test(`la demostración de ${metodo} llega al final sin llamar al backend`, async ({ page }) => {
    const solicitudesApi = [];
    await page.route("**/api/**", async route => {
      solicitudesApi.push(route.request().url());
      await route.abort();
    });

    await page.goto(`${BASE}/vista-previa/pago`);
    await page.locator(".celda-metodo").filter({ hasText: metodo }).click();
    await expect(page.getByRole("button", { name: "Continuar" })).toBeVisible();
    await page.getByRole("button", { name: "Continuar" }).click();

    await expect(page).toHaveURL(new RegExp(`\\/vista-previa\\/pago-resumen\\?metodo=${metodo}`));
    await expect(page.getByRole("heading", { name: "Resumen de tu consumo" })).toBeVisible();
    await expect(page.locator(".pantalla-pago")).toContainText(metodo === "NFC" ? "NFC del celular" : metodo);

    await page.getByRole("button", { name: "Continuar al pago (demo)" }).click();
    const titulo = metodo === "NFC" ? "Acerca tu teléfono" :
      metodo === "Tarjeta" ? "Coloca tu tarjeta" : "Introduce el efectivo";
    await expect(page.getByRole("heading", { name: titulo })).toBeVisible();

    const lectura = metodo === "NFC" ? "Simular lectura NFC" :
      metodo === "Tarjeta" ? "Simular lectura de tarjeta" : "Simular depósito en efectivo";
    await page.getByRole("button", { name: lectura }).click();
    await page.getByRole("button", { name: "Simular confirmación del operador" }).click();
    await page.getByRole("button", { name: "Finalizar retiro (demo)" }).click();

    await expect(page.getByRole("heading", { name: "Retiro completado" })).toBeVisible();
    await expect(page.locator(".tarjeta-final")).toContainText(metodo === "NFC" ? "NFC del celular" : metodo);
    await expect(page.locator(".tarjeta-final")).toContainText("Disponible (demo)");
    await expect(page.locator(".tarjeta-final")).toContainText("Total simulado");
    expect(solicitudesApi).toEqual([]);
  });
}
