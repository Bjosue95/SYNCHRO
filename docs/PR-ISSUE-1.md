# Borrador: completar módulos del prototipo y corregir encomiendas

Base obligatoria: `refactor/arquitectura-backend`.
Cabeza: `feat/issue-1-encomiendas-end-to-end`. Nunca main.

Las encomiendas solo se reservan desde una cuenta web registrada. La terminal procesa
reservas existentes y exige consentimiento firmado WhatsApp, captura de rostro simulada
de la persona correspondiente y PIN temporal para retirar. Se retiraron altas de visitantes,
formulario presencial y rutas demo que permitían eludir esos controles. No se requiere
biometría profesional ni proveedor facial externo.

Conecta depósito/retiro transaccionales, PIN/outbox cifrados, recuperación idempotente del
PIN con nueva verificación telefónica, referencias visuales cifradas/vencidas/eliminables,
servicios prepagados de carga/Personal/frío/calor, incidencias/tarifas/reportes, usuarios/roles,
revocación de sesiones y OpenAPI. Incorpora instalación local protegida del esquema mínimo
con un único origen y migraciones ordenadas, sin inicializar ni borrar bases históricas.

Validación Cloud Linux: API/tests/AdminSetup compilan sin advertencias/errores; tablet
compila. Suite completa: 295 casos, 248 aprobados, 43 fallidos, 4 omisiones anteriores.
Los fallos son 25 LocalDB no soportado, 5 secrets.json ausente, 2 HTTP500 derivados de
LocalDB y 11 casos SQL externos sin conexión. No se agregaron Skips. Los tests HTTP usan
repositorios de prueba; no demuestran las transacciones SQL reales. El comparador PNG
de demostración sí se ejecutó junto con cookies, permisos, CSRF, consentimiento y PIN.

Mantener como borrador hasta comprobar migraciones y casos SQL en Windows, integrar con
el esquema histórico de verificación telefónica y comprobar WhatsApp con credenciales/
plantillas. No se afirma entrega real WhatsApp, prueba de vida, temperatura/carga física
ni apertura de hardware. La UI de cámara y operaciones completas necesita integración
y pruebas de navegador. No está terminado todo el backend ni listo para fusionar.

Detalles y resultados por caso: docs/ISSUE-1-AVANCE.md,
docs/ENCOMIENDAS-CLIENTES-Y-ROSTRO-SIMULADO.md,
docs/INSTALACION-PROTOTIPO-AISLADO.md y
docs/resultados/issue-1-requisitos-corregidos-20261110.json.
