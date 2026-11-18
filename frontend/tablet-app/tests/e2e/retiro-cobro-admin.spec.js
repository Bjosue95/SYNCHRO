import { test, expect } from "@playwright/test";

// El administrador original vive en frontend/admin-app: puerto 5174.
// La tablet no debe exponer una segunda interfaz de administración.
test("la antigua ruta administrativa no existe en la tablet", async ({ page }) => {
  await page.goto("http://127.0.0.1:4174/admin/whatsapp-manual");

  await expect(page).toHaveURL("http://127.0.0.1:4174/menu");
  await expect(
    page.getByRole("heading", { name: "Cobros pendientes de retiro personal" })
  ).toHaveCount(0);
  await expect(
    page.getByRole("heading", { name: "Acceso administrativo" })
  ).toHaveCount(0);
});
