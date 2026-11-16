# SYNCHRO - Continuidad 16 noviembre 2026

## Componentes
- Backend ASP.NET Core.
- Tablet Vue: frontend/tablet-app.
- Administrador Vue: frontend/admin-app.
- SQL Server LocalDB: SYNCHRO_Aislado.
- Base de datos: SynchroDB_Pruebas.

## Avances confirmados
- Deposito y retiro personal con rostro simulado y PIN.
- Bloqueo de PIN al quinto intento incorrecto.
- Retiro confirmado y casillero liberado.
- Pantalla de finalizacion del retiro.
- Inventario del prototipo: 10 casilleros.
- Tablet y administrador con consultas filtradas.
- Ultima suite completa: 388 pruebas,
  387 aprobadas, 0 fallidas y 1 omitida.

## Pendiente
- Implementar resumen, tarifas y pago antes del deposito.
- Resolver prueba omitida de ConsultasPlataforma.
- Repetir pruebas tras los ultimos cambios de frontend.
- Verificar integracion completa desde tablet.

## Reglas de desarrollo
- Mantener el diseno aprobado.
- No crear ramas innecesarias.
- Trabajar directamente en el codigo existente.
- No subir secretos ni respaldos SQL a GitHub.
