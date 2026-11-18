# SYNCHRO — cierre del retiro personal en la tablet

## Recorrido implementado

1. Reconocimiento facial simulado y PIN de seis dígitos.
2. Prevalidación protegida y creación de orden de cobro **pendiente**, calculada por SQL Server.
3. Elección del método de pago y presentación del **Resumen de tu consumo**.
4. Pago simulado pendiente: un operador autenticado lo comprueba y confirma desde la administración.
5. La tablet consulta la finalización protegida. El backend verifica el cobro y autoriza el retiro **solo si se ha confirmado**.
6. La tablet presenta **Retiro completado**, código y disponibilidad del casillero. El prototipo indica que la apertura física es manual.
7. **Finalizar** vuelve al menú y la autorización temporal se elimina de memoria.

En caso de un cobro pendiente, fallo del backend o autorización vencida, no debe mostrarse un retiro exitoso.

## Limitaciones actuales

- No se cobra dinero real ni se abre una cerradura física de forma automática.
- La administración de pruebas permite confirmar **Efectivo** y **Tarjeta**. NFC todavía no se confirma en el módulo administrativo. Si el usuario lo escoge, la pantalla advierte la limitación y permite **Cambiar método** sin reiniciar el reconocimiento, conservando solo la orden efímera.
- El método elegido en la tablet es una **preferencia visual**, no un pago confirmado: el operador comprueba el método real. No confiar en la selección de la tablet para marcar un cobro como pagado.
- El backend manual de cobros solo se registra en **Development**, con **SynchroDB_Pruebas** en `(localdb)\SYNCHRO_Aislado`, `WhatsAppManual:Habilitado` y la configuración adicional exigida por el backend.
- `CobroRetiroPersonal:Obligatorio` debe estar habilitado en ese entorno de pruebas para permitir la finalización por pago.
- `VITE_RETIRO_PAGO_HABILITADO` controla el recorrido pagado de la tablet. `.env.e2e` lo habilita **solo para pruebas E2E**; no activa automáticamente el entorno de desarrollo normal.
- No activar estos indicadores en una base real ni asumir que los procedimientos SQL ya se han aplicado. La prueba Playwright utiliza respuestas simuladas y **no verifica SQL Server real**.

## Verificación local

Desde `frontend/tablet-app`:

```powershell
npm.cmd test
npm.cmd run build
npm.cmd run test:e2e
```

Prueba protegida principal: `tests/e2e/retiro-pago-completo.spec.js`.
Los tests verifican que la tablet no dé por confirmado un cobro pendiente, que se pueda cambiar desde NFC a Efectivo sin perder la sesión y que la finalización aparezca después de la confirmación administrativa.

## Pendiente de integración real

Validar en la computadora de desarrollo la configuración conjunta frontend/backend, las migraciones SQL de prevalidación y cobro, la cuenta de operador, el error por pago pendiente, el estado definitivo del casillero y la apertura **manual** del prototipo.
