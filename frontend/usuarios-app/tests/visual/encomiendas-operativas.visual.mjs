// Chrome real + backend SQL real de pruebas. Cámara/descriptor y comprobantes son fixtures controlados.
// No invoca la tablet, hardware ni WhatsApp. No conserva PIN/contraseñas en capturas o informes.
import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { chromium } from "playwright";

const web = process.env.SYNCHRO_ENCOMIENDAS_WEB || "https://127.0.0.1:5185";
const admin = process.env.SYNCHRO_ENCOMIENDAS_ADMIN || "https://127.0.0.1:5186";
const destino = "node_modules/.cache/synchro-visual/encomiendas-operativas";
mkdirSync(destino, { recursive: true });
const usuario = "qa_env_" + randomUUID().replaceAll("-", ""),
  clave = "SYNCHRO-Fixture-" + randomUUID() + "!";
const pruebas = [],
  errores = [];
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});
const contexto = await browser.newContext({
  ignoreHTTPSErrors: true,
  reducedMotion: "reduce",
  viewport: { width: 1440, height: 960 },
});
const administrativo = await browser.newContext({
  ignoreHTTPSErrors: true,
  reducedMotion: "reduce",
  viewport: { width: 1440, height: 960 },
});
const page = await contexto.newPage(),
  panel = await administrativo.newPage();
for (const p of [page, panel]) {
  p.setDefaultTimeout(15000);
  p.setDefaultNavigationTimeout(20000);
}
for (const p of [page, panel])
  p.on("pageerror", (e) => errores.push(e.message));
function fixtureAdmin(activar) {
  const script = String.raw`
Add-Type -AssemblyName System.Data
$c=New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\SYNCHRO_Aislado;Database=SynchroDB_Pruebas;Integrated Security=True;Encrypt=False')
$c.Open()
if($c.Database -ne 'SynchroDB_Pruebas' -or $c.DataSource -ne '(localdb)\SYNCHRO_Aislado'){throw 'Destino incorrecto'}
$q=$c.CreateCommand()
if($env:QA_ACCION -eq 'activar'){
$q.CommandText='DECLARE @id INT; INSERT dbo.Administradores(NombreUsuario,PasswordHash,Activo) SELECT @usuario,PasswordHash,1 FROM dbo.ClientesWebPrototipo WHERE NombreUsuario=@usuario; SET @id=SCOPE_IDENTITY(); IF @id IS NULL THROW 59111,''No existe fixture propio.'',1; INSERT dbo.AccesosAdministrativosGestionados(AdministradorId,VersionSesion) VALUES(@id,1); INSERT dbo.RolesAdministrativosPrototipo(AdministradorId,Rol) VALUES(@id,N''Administrador'');'
}else{$q.CommandText='UPDATE dbo.Administradores SET Activo=0 WHERE NombreUsuario=@usuario AND NombreUsuario LIKE N''qa_env_%'';'}
[void]$q.Parameters.AddWithValue('@usuario',$env:QA_USUARIO)
[void]$q.ExecuteNonQuery()
$c.Dispose()
`;
  const r = spawnSync("powershell", ["-NoProfile", "-Command", script], {
    encoding: "utf8",
    env: {
      ...process.env,
      QA_USUARIO: usuario,
      QA_ACCION: activar ? "activar" : "desactivar",
    },
  });
  assert.equal(r.status, 0, r.stderr);
}
async function post(ruta, datos, extra = {}) {
  const token = await contexto.request.get(
    web + "/api/web/clientes/csrf-token",
  );
  assert.equal(token.status(), 200);
  return contexto.request.post(web + ruta, {
    data: datos,
    headers: { "X-CSRF-TOKEN": (await token.json()).csrfToken, ...extra },
  });
}
async function sinDesbordamiento(p) {
  assert.ok(
    await p.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth + 1,
    ),
    "Desbordamiento horizontal de la página",
  );
}
try {
  await page.goto(web + "/acceso");
  const token = await contexto.request.get(
    web + "/api/web/clientes/acceso/csrf-token",
  );
  assert.equal(token.status(), 200);
  const registro = await contexto.request.post(
    web + "/api/web/clientes/acceso/registro",
    {
      data: {
        nombreUsuario: usuario,
        contrasena: clave,
        telefono: "+999123456711",
      },
      headers: { "X-CSRF-TOKEN": (await token.json()).csrfToken },
    },
  );
  assert.equal(registro.status(), 200);
  await page.goto(web + "/reservar/encomienda");
  await page
    .getByRole("heading", { name: "Nueva encomienda", exact: true })
    .waitFor();
  assert.equal(
    await page
      .getByRole("button", { name: "Continuar con el destinatario" })
      .isDisabled(),
    true,
  );
  await page
    .getByText("Registrar mi referencia facial", { exact: true })
    .click();
  assert.equal(
    await page
      .getByRole("button", { name: "Activar cámara", exact: true })
      .isDisabled(),
    true,
  );
  await page.evaluate(() => {
    navigator.mediaDevices.getUserMedia = async () => {
      throw new DOMException(
        "Cámara denegada en prueba controlada",
        "NotAllowedError",
      );
    };
  });
  await page
    .getByLabel("Acepto el tratamiento de mi referencia facial")
    .check();
  await page
    .getByRole("button", { name: "Activar cámara", exact: true })
    .click();
  await page
    .getByRole("alert")
    .filter({ hasText: "Cámara denegada" })
    .waitFor();
  pruebas.push(
    "Consentimiento obligatorio, controles de cámara y error controlado de permiso",
  );
  const png = await page.evaluate(() => {
    const c = document.createElement("canvas");
    c.width = c.height = 64;
    const x = c.getContext("2d");
    x.fillStyle = "#141414";
    x.fillRect(0, 0, 64, 64);
    x.fillStyle = "#e6e6e6";
    x.fillRect(0, 0, 32, 64);
    return c.toDataURL("image/png").split(",")[1];
  });
  const cfg = await (
    await contexto.request.get(web + "/api/web/encomiendas/configuracion")
  ).json();
  const ref = await post(
    "/api/web/encomiendas/referencia-facial",
    {
      imagenPng: png,
      descriptor: Array.from({ length: 1024 }, (_, i) => Math.sin(i * 0.17)),
      modelo: cfg.modelo,
      versionConsentimiento: cfg.versionConsentimiento,
      contrasena: clave,
    },
    { "X-SYNCHRO-CONSENTIMIENTO": "ACEPTO" },
  );
  assert.equal(ref.status(), 200, await ref.text());
  await page.reload();
  await page
    .getByRole("button", { name: "Continuar con el destinatario" })
    .click();
  await page.getByLabel("WhatsApp del destinatario").fill("+999123456712");
  await page.getByRole("button", { name: "Revisar encomienda" }).click();
  await page
    .getByRole("button", { name: "Crear encomienda", exact: true })
    .click();
  await page.waitForURL(/\/encomiendas\/\d+/);
  const id = Number(new URL(page.url()).pathname.split("/").at(-1));
  await page
    .getByRole("heading", { name: `Encomienda #${id}`, exact: true })
    .waitFor();
  assert.equal(
    await page
      .getByRole("link", { name: "Autorizar por WhatsApp", exact: true })
      .count(),
    1,
  );
  let detalle = await (
    await contexto.request.get(web + "/api/web/encomiendas/" + id)
  ).json();
  assert.equal(detalle.pago.estado, "Pendiente");
  assert.equal(detalle.remitente.consentimiento, "Pendiente");
  pruebas.push(
    "Creación guiada real, participantes distintos y seguimiento sin confirmaciones ficticias",
  );
  fixtureAdmin(true);
  await panel.goto(admin + "/login");
  await panel.locator("#admin-usuario").fill(usuario);
  await panel.locator("#admin-clave").fill(clave);
  await panel
    .getByRole("button", { name: "Iniciar sesión", exact: true })
    .click();
  await panel.waitForURL(/\/admin$/);
  await panel.goto(admin + "/admin/encomiendas");
  await panel
    .getByRole("button", { name: "Abrir encomienda " + id, exact: true })
    .click();
  assert.equal(
    await panel
      .getByRole("button", { name: "Confirmar pago recibido", exact: true })
      .isDisabled(),
    true,
  );
  await panel.getByRole('link',{name:'Gestionar WhatsApp y PIN de esta encomienda',exact:true}).click();
  assert.equal(
    await panel
      .getByRole("button", { name: "Emitir PIN de depósito", exact: true })
      .isDisabled(),
    true,
  );
  for (const [i, rol] of ["remitente", "destinatario"].entries()) {
    const seccion = panel.locator(".enc-participants>.enc-card").nth(i);
    assert.equal(
      await seccion
        .getByRole("button", { name: "Confirmar consentimiento", exact: true })
        .isDisabled(),
      true,
    );
    await seccion
      .getByLabel("Número desde el que se recibió")
      .fill(detalle[rol].telefono);
    if (rol === "destinatario") await seccion
      .getByLabel("Número de encomienda del mensaje recibido")
      .fill(detalle[rol].codigo);
    await seccion.getByLabel("Texto recibido en WhatsApp").fill(detalle[rol].texto);
    await seccion
      .getByLabel("Referencia comprobable del mensaje")
      .fill("WA-FIXTURE-" + randomUUID());
    await seccion.getByLabel("He comprobado el mensaje").check();
    if (i === 1) await seccion.getByLabel("El destinatario aceptó").check();
    await seccion
      .getByRole("button", { name: "Confirmar consentimiento", exact: true })
      .click();
    await panel.waitForFunction(
      (i) =>
        !document
          .querySelectorAll(".enc-participants>.enc-card")
          [i].querySelector("form"),
      i,
    );
  }
  await panel.getByRole('link',{name:'Abrir operación de encomienda #'+id,exact:true}).click();
  await panel.getByLabel("Método").selectOption("Transferencia");
  await panel
    .getByLabel("Importe comprobado (HNL)", { exact: true })
    .fill("22.00");
  await panel
    .getByLabel("Referencia del comprobante", { exact: true })
    .fill("PAGO-FICTICIO-AISLADO-" + randomUUID());
  await panel
    .getByLabel("Justificación del importe sin tarifa configurada")
    .fill(
      "Fixture automatizado aislado: no representa dinero recibido ni tarifa definitiva.",
    );
  await panel.getByLabel("He verificado que este importe").check();
  await panel
    .getByRole("button", { name: "Confirmar pago recibido", exact: true })
    .click();
  await panel.getByText("Confirmado por", { exact: true }).waitFor();
  await panel.getByRole('link',{name:'Gestionar WhatsApp y PIN de esta encomienda',exact:true}).click();
  await panel
    .getByRole("button", { name: "Emitir PIN de depósito", exact: true })
    .click();
  await panel.locator(".enc-pin-value").waitFor();
  assert.match(await panel.locator(".enc-pin-value").innerText(), /^\d{6}$/);
  const enlace = await panel
    .getByRole("link", { name: "Abrir WhatsApp para entrega manual" })
    .getAttribute("href");
  assert.equal(enlace, "https://wa.me/999123456711");
    // Volver de WhatsApp debe permitir registrar el envío sin volver a exponer el PIN.
    await panel.evaluate(() => {
      Object.defineProperty(document, "hidden", { configurable: true, get: () => true });
      document.dispatchEvent(new Event("visibilitychange"));
      delete document.hidden;
    });
    await panel.locator(".enc-pin-value").waitFor({ state: "detached" });
    assert.equal(await panel.locator(".enc-pin").count(), 1);
    assert.equal(await panel.getByRole("link", { name: "Abrir WhatsApp para entrega manual" }).count(), 0);
  await panel
    .getByLabel("Referencia del mensaje enviado")
    .fill("ENVIO-FICTICIO-" + randomUUID());
  await panel.getByLabel("Comprobé la entrega manual").check();
  await panel
    .getByRole("button", { name: "Registrar entrega del PIN", exact: true })
    .click();
    await panel.locator(".enc-pin").waitFor({ state: "detached" });
  detalle = await (
    await contexto.request.get(web + "/api/web/encomiendas/" + id)
  ).json();
  assert.equal(detalle.pago.estado, "ConfirmadoManual");
  assert.ok(
    detalle.pines.some(
      (p) => p.finalidad === "Deposito" && p.estado === "Entregado",
    ),
  );
  assert.equal(detalle.estado, "Preparacion");
  assert.equal(detalle.condiciones.tabletIntegrada, false);
  pruebas.push(
    "Ficha administrativa real: dos consentimientos explícitos, pago manual ficticio aislado, PIN temporal y entrega auditada",
  );
  for (const width of [1440, 834, 390]) {
    await page.setViewportSize({ width, height: 900 });
    await panel.setViewportSize({ width, height: 900 });
    for (const tema of ["claro", "oscuro"]) {
      await page.evaluate((t) => {
        localStorage.setItem("synchro-usuarios-tema", t);
      }, tema);
      await page.goto(web + "/mi-cuenta");
      await page
        .getByRole("heading", { name: "Mis encomiendas", exact: true })
        .waitFor();
      await sinDesbordamiento(page);
      await page.screenshot({
        path: `${destino}/panel-${width}-${tema}.png`,
        fullPage: true,
      });
      await page.goto(web + "/encomiendas/" + id);
      await page
        .getByRole("heading", { name: `Encomienda #${id}`, exact: true })
        .waitFor();
      await sinDesbordamiento(page);
      await page.screenshot({
        path: `${destino}/detalle-${width}-${tema}.png`,
        fullPage: true,
      });
      assert.equal(
        await page
          .getByRole("button", {
            name: "Verificar identidad y PIN",
            exact: true,
          })
          .count(),
        0,
        "No ofrece apertura sin solicitud de terminal",
      );
    }
    await sinDesbordamiento(panel);
    await panel.screenshot({
      path: `${destino}/admin-${width}.png`,
      fullPage: true,
    });
  }
  pruebas.push(
    "Computadora/tablet/celular: panel y detalle en ambos temas, ficha administrativa sin desbordamientos",
  );
  const revocada = await contexto.request.delete(
    web + "/api/web/encomiendas/referencia-facial",
    {
      headers: {
        "X-CSRF-TOKEN": (
          await (
            await contexto.request.get(web + "/api/web/clientes/csrf-token")
          ).json()
        ).csrfToken,
      },
    },
  );
  assert.equal(revocada.status(), 200);
  assert.deepEqual(errores, []);
  writeFileSync(
    `${destino}/resultados.json`,
    JSON.stringify(
      {
        fecha: new Date().toISOString(),
        usuarioFixture: usuario,
        encomiendaFixture: id,
        pruebas,
        errores,
        capturaFacial:
          "Controlada; no valida reconocimiento con una persona real",
        tablet: "Sin modificar ni invocar",
        hardware: "No validado; apertura simulada",
      },
      null,
      2,
    ),
  );
  console.log(
    JSON.stringify(
      { correctas: pruebas.length, capturas: 15, encomiendaFixture: id },
      null,
      2,
    ),
  );
} finally {
  await page
    .screenshot({ path: `${destino}/ultima-vista-usuario.png`, fullPage: true })
    .catch(() => {});
  await panel
    .screenshot({
      path: `${destino}/ultima-vista-admin.png`,
      fullPage: true,
      mask: [panel.locator(".enc-pin")],
    })
    .catch(() => {});
  try {
    fixtureAdmin(false);
  } catch (e) {
    console.error(
      "Revisar desactivación del administrador fixture:",
      e.message,
    );
  }
  await browser.close();
}
