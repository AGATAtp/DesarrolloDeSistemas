# Actividad 9 – Parte 1: Depuración, excepciones y mensajes UX

Proyecto: Sistema de Autolavado (ASP.NET Core MVC + Npgsql). Log técnico: `errores_sistema.log` (carpeta del proyecto) y `Console.Error`.

## Paso 1 – Auditoría de riesgos (4+ fallos reales)

| # | Tipo | Escenario |
|---|------|-----------|
| F1 | Infraestructura | PostgreSQL apagado al guardar cliente / registrar venta / iniciar sesión |
| F2 | Integridad SQL | Documento de cliente duplicado (UNIQUE `23505`) |
| F3 | Integridad SQL | Venta con cliente o servicio que dejó de existir (FOREIGN KEY `23503`) |
| F4 | Negocio / entrada | Venta sin servicios, descuento > 20 %, cantidad fuera de 1–9999 |
| F5 | Referencia | Editar/Quitar/Dar de baja sin fila seleccionada (id vacío o 0) |

## Paso 2 – Depuración interactiva (breakpoints sugeridos, F5)

| Fallo | Breakpoint (línea previa al fallo) | Qué inspeccionar |
|-------|------------------------------------|------------------|
| F1/F2 | `Data/ClienteDAO.cs` línea 71 `comando.ExecuteNonQuery();` | Locales `c`, `conexion.State`, `exPg.SqlState`; Call Stack: `ClienteController.Guardar → ClienteDAO.Insertar` |
| F3 | `Data/VentaDAO.cs` dentro de `RegistrarVenta` (bucle `foreach (var item in venta.Detalles)`) | `idVentaGenerado`, `item.IdProducto` |
| F4 | `Controllers/VentaController.cs` línea 131 `venta.AplicarDescuento(...)` | `peticion.PorcentajeDescuento` |
| F5 | `Data/ClienteDAO.cs` `Eliminar`, primera línea (guarda `id <= 0`) | `id` |

Tomar captura del IDE detenido en el breakpoint mostrando **Variables locales** y **Pila de llamadas**.

## Paso 3 – Qué se implementó

- **Cláusulas de guarda** al inicio de `ClienteDAO.Insertar/Actualizar/Eliminar/ObtenerPorId/ExisteDocumento`, `VentaDAO.RegistrarVenta`, `UsuarioDAO.ObtenerPorNombreUsuario`, y en los controladores (formulario/petición nula, detalle vacío, cliente inválido).
- **try-catch-finally ordenado** `PostgresException → NpgsqlException → Exception` (con `throw;` en el último) en `ClienteDAO` (Insertar, Actualizar, Eliminar, ObtenerTodos) y `VentaDAO` (RegistrarVenta, ObtenerHistorial). El resto de consultas usa `Data/ProteccionBD.cs` con el mismo orden.
- **finally**: cierra la conexión; en `RegistrarVenta` además hace Rollback si no se confirmó (maestro + detalle atómicos).
- **Log técnico**: `Services/RegistroErrores.cs` (archivo + consola, nunca lanza, no duplica al relanzar). Ningún `catch` queda vacío.
- **Traducción a UX**: `Services/TraductorErroresBD.cs` (SqlState → mensaje), `ErrorOperacionException` / `ValidacionNegocioException` (su `Message` ya es apto para pantalla) y `MensajesUX` como red de seguridad en controladores.
- **Pantalla de error global**: `UseExceptionHandler("/Home/Error")` también en Development (nunca se ve el StackTrace); `HomeController.Error` registra la excepción en el log.
- **Frontend** (`ventas.js`, `clientes.js`): respuestas 400/500, JSON ilegible y caída de red muestran avisos orientadores; "Quitar/Editar/Baja" sin selección se controlan.

## Plantilla A – Operaciones protegidas y mensajes

| ID | Operación / Módulo | Falla potencial | Excepción(es) | Mensaje técnico crudo (evitado) | Mensaje UX implementado |
|----|--------------------|-----------------|---------------|----------------------------------|-------------------------|
| OP-1 | Guardar cliente (`ClienteDAO.Insertar`) | Documento duplicado | `PostgresException` 23505 | `duplicate key value violates unique constraint "clientes_documento_key"` | "El documento '10658776' ya pertenece a otro cliente. Búscalo en la lista o ingresa un documento distinto." |
| OP-2 | Guardar/Editar cliente | PostgreSQL apagado / red caída | `NpgsqlException` | `Failed to connect to 127.0.0.1:5432` | "No fue posible conectar con la base de datos en este momento. Verifica que el servicio de PostgreSQL esté activo e inténtalo nuevamente; tus datos no se modificaron." |
| OP-3 | Registrar venta maestro-detalle (`VentaDAO.RegistrarVenta`) | Cliente/servicio inexistente | `PostgresException` 23503 | `insert or update on table "ventas" violates foreign key constraint...` | "No se registró la venta porque el cliente o alguno de los servicios ya no está disponible. Actualiza la página, revisa la selección e inténtalo de nuevo; no se guardó nada." |
| OP-4 | Aplicar descuento / validar venta | Descuento > 20 % o sin servicios | `ArgumentOutOfRangeException`, guardas | `Specified argument was out of the range of valid values` / CHECK `23514` | "El descuento máximo permitido es 20%. Ingresa un valor entre 0% y 20% para continuar." / "Agrega al menos un producto antes de confirmar la venta." |
| OP-5 | Dar de baja cliente | Sin selección / ya dado de baja | Guarda (`ValidacionNegocioException`), `ErrorOperacionException` | `NullReferenceException` / 0 filas afectadas | "Selecciona un cliente de la lista antes de darlo de baja." / "No encontramos al cliente seleccionado; es posible que ya haya sido dado de baja..." |
| OP-6 | Consultar listados (clientes, historial, panel) e iniciar sesión | BD caída | `NpgsqlException` | `Failed to connect...` | Aviso de conexión (mismo texto de OP-2) en la propia pantalla, sin romper la página |

## Plantilla B – Pruebas de verificación (completar con el resultado real y capturas)

| ID | Op. | Escenario / acción para forzar el fallo | ANTES (sin protección) | DESPUÉS (esperado) | Estado |
|----|-----|------------------------------------------|------------------------|--------------------|--------|
| PR-01 | OP-2 | Detener el servicio PostgreSQL y pulsar "Guardar cliente" | Mensaje genérico de conexión sin log técnico | App abierta; aviso orientador de conexión; entrada en `errores_sistema.log` | ☐ |
| PR-02 | OP-1 | Crear dos clientes con el mismo documento | Error en bruto del motor / mensaje genérico | Aviso "El documento '…' ya pertenece a otro cliente…" y log 23505 | ☐ |
| PR-03 | OP-3 | Registrar venta con PostgreSQL detenido | Excepción no controlada | Aviso de conexión; no se guarda nada; log | ☐ |
| PR-04 | OP-3 | Venta con servicio eliminado de la BD (FK) tras abrir la pantalla | Excepción SQL cruda | Aviso "ya no está disponible…"; rollback; log 23503 | ☐ |
| PR-05 | OP-4 | Descuento 25 % y confirmar; venta sin servicios | – | Avisos orientadores del descuento / detalle vacío | ☐ |
| PR-06 | OP-5 | Dar de baja un cliente ya dado de baja en otra pestaña | Éxito silencioso falso | Aviso "No encontramos al cliente…" | ☐ |
| PR-07 | OP-6 | Abrir `/Cliente` y `/Venta/Historial` con PostgreSQL detenido | Pantalla de excepción | Página visible con aviso de conexión | ☐ |
| PR-08 | – | Forzar una excepción inesperada | StackTrace en pantalla | Pantalla "Algo salió mal" + log | ☐ |

## Paso 5 – Commit

```bash
git add .
git commit -m "fix(resiliencia): implementa manejo de excepciones y mensajes UX"
git push
```
