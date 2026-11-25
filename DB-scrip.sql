-- =====================================================================
-- DB-scrip.sql -- Script general e idempotente de la BD activa "Pepokita"
-- Generado desde el esquema real (PostgreSQL 18, esquema public) el 2026-11-25.
-- Cubre: ENUM, 14 tablas, 58 restricciones (PK/UNIQUE/FK/CHECK),
--        indices de apoyo, backfill CR-01 y datos semilla de catalogos.
--
-- SEGURIDAD: NO incluye contrasenas, hashes de acceso, correos, documentos
-- ni datos personales reales. Los usuarios de prueba se crean aparte con
-- Data/cr01_descuentos_vendedor.sql (no se repite aqui ningun secreto).
-- Los catalogos se insertan por clave natural con ON CONFLICT DO NOTHING:
-- se puede ejecutar las veces que se necesite sin duplicar ni borrar datos.
--
-- Uso: psql -h localhost -p 5432 -U postgres -d Pepokita -v ON_ERROR_STOP=1 -f DB-scrip.sql
-- =====================================================================

BEGIN;

-- ---------- 1. Tipo ENUM ----------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_type
        WHERE typname = 'estado_orden_enum'
          AND typnamespace = 'public'::regnamespace
    ) THEN
        CREATE TYPE public.estado_orden_enum AS ENUM
            ('pendiente', 'en_proceso', 'listo', 'entregado', 'cancelado');
    END IF;
END $$;

-- ---------- 2. Tablas (serial = integer NOT NULL DEFAULT nextval) ----------
CREATE TABLE IF NOT EXISTS public.roles (
    id     serial NOT NULL,
    nombre varchar(50) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.clientes (
    id             serial NOT NULL,
    documento      varchar(20)  NOT NULL,
    nombre         varchar(80)  NOT NULL,
    apellido       varchar(80)  NOT NULL,
    telefono       varchar(20)  NOT NULL,
    correo         varchar(120),
    direccion      text,
    activo         boolean NOT NULL DEFAULT true,
    tipo_cliente   varchar(20) NOT NULL DEFAULT 'Particular',
    fecha_registro date NOT NULL DEFAULT CURRENT_DATE
);

CREATE TABLE IF NOT EXISTS public.empleados (
    id        serial NOT NULL,
    rol_id    integer NOT NULL,
    documento varchar(20) NOT NULL,
    nombre    varchar(80) NOT NULL,
    apellido  varchar(80) NOT NULL,
    telefono  varchar(20) NOT NULL,
    activo    boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS public.usuarios (
    id                serial NOT NULL,
    empleado_id       integer NOT NULL,
    nombre_usuario    varchar(50) NOT NULL,
    contrasena_hash   varchar(255) NOT NULL,
    activo            boolean NOT NULL DEFAULT true,
    intentos_fallidos integer NOT NULL DEFAULT 0,
    bloqueado         boolean NOT NULL DEFAULT false,
    ultimo_acceso     timestamp
);

CREATE TABLE IF NOT EXISTS public.tipos_vehiculo (
    id     serial NOT NULL,
    nombre varchar(50) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.vehiculos (
    id               serial NOT NULL,
    cliente_id       integer NOT NULL,
    tipo_vehiculo_id integer NOT NULL,
    placa            varchar(20) NOT NULL,
    marca            varchar(50) NOT NULL,
    modelo           varchar(50) NOT NULL,
    color            varchar(30),
    activo           boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS public.servicios (
    id                     serial NOT NULL,
    nombre                 varchar(100) NOT NULL,
    descripcion            text,
    duracion_estimada_min  integer NOT NULL,
    activo                 boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS public.precios_servicio (
    id               serial NOT NULL,
    servicio_id      integer NOT NULL,
    tipo_vehiculo_id integer NOT NULL,
    precio           numeric(10,2) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.ordenes_servicio (
    id                  serial NOT NULL,
    vehiculo_id         integer NOT NULL,
    empleado_id         integer,
    fecha_recepcion     timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
    fecha_finalizacion  timestamp,
    estado              public.estado_orden_enum NOT NULL DEFAULT 'pendiente'::public.estado_orden_enum,
    observaciones       text,
    total_pagar         numeric(10,2) NOT NULL DEFAULT 0.00
);

CREATE TABLE IF NOT EXISTS public.orden_detalles (
    id             serial NOT NULL,
    orden_id       integer NOT NULL,
    servicio_id    integer NOT NULL,
    precio_cobrado numeric(10,2) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.pagos (
    id           serial NOT NULL,
    orden_id     integer NOT NULL,
    monto_pagado numeric(10,2) NOT NULL,
    metodo_pago  varchar(30) NOT NULL DEFAULT 'Efectivo',
    fecha_pago   timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS public.productos (
    id     serial NOT NULL,
    nombre varchar(100) NOT NULL,
    precio numeric(10,2) NOT NULL,
    activo boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS public.ventas (
    id                   serial NOT NULL,
    fecha                timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
    total                numeric(10,2) NOT NULL,
    id_cliente           integer NOT NULL,
    vendedor             varchar(100) NOT NULL DEFAULT 'Sin registrar',
    id_vendedor          integer,
    subtotal_bruto       numeric(10,2) NOT NULL,
    porcentaje_descuento numeric(5,2) NOT NULL DEFAULT 0,
    monto_descuento      numeric(10,2) NOT NULL DEFAULT 0,
    total_final          numeric(10,2) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.detalle_ventas (
    id              serial NOT NULL,
    id_venta        integer NOT NULL,
    id_producto     integer NOT NULL,
    cantidad        integer NOT NULL,
    precio_unitario numeric(10,2) NOT NULL,
    subtotal        numeric(10,2) NOT NULL
);

-- ---------- 3. Restricciones (se anaden solo si no existen) ----------
DO $$
DECLARE item record;
BEGIN
    FOR item IN
        SELECT * FROM (VALUES
            ('clientes_pkey', 'ALTER TABLE public.clientes ADD CONSTRAINT clientes_pkey PRIMARY KEY (id)'),
            ('detalle_ventas_pkey', 'ALTER TABLE public.detalle_ventas ADD CONSTRAINT detalle_ventas_pkey PRIMARY KEY (id)'),
            ('empleados_pkey', 'ALTER TABLE public.empleados ADD CONSTRAINT empleados_pkey PRIMARY KEY (id)'),
            ('orden_detalles_pkey', 'ALTER TABLE public.orden_detalles ADD CONSTRAINT orden_detalles_pkey PRIMARY KEY (id)'),
            ('ordenes_servicio_pkey', 'ALTER TABLE public.ordenes_servicio ADD CONSTRAINT ordenes_servicio_pkey PRIMARY KEY (id)'),
            ('pagos_pkey', 'ALTER TABLE public.pagos ADD CONSTRAINT pagos_pkey PRIMARY KEY (id)'),
            ('precios_servicio_pkey', 'ALTER TABLE public.precios_servicio ADD CONSTRAINT precios_servicio_pkey PRIMARY KEY (id)'),
            ('productos_pkey', 'ALTER TABLE public.productos ADD CONSTRAINT productos_pkey PRIMARY KEY (id)'),
            ('roles_pkey', 'ALTER TABLE public.roles ADD CONSTRAINT roles_pkey PRIMARY KEY (id)'),
            ('servicios_pkey', 'ALTER TABLE public.servicios ADD CONSTRAINT servicios_pkey PRIMARY KEY (id)'),
            ('tipos_vehiculo_pkey', 'ALTER TABLE public.tipos_vehiculo ADD CONSTRAINT tipos_vehiculo_pkey PRIMARY KEY (id)'),
            ('usuarios_pkey', 'ALTER TABLE public.usuarios ADD CONSTRAINT usuarios_pkey PRIMARY KEY (id)'),
            ('vehiculos_pkey', 'ALTER TABLE public.vehiculos ADD CONSTRAINT vehiculos_pkey PRIMARY KEY (id)'),
            ('ventas_pkey', 'ALTER TABLE public.ventas ADD CONSTRAINT ventas_pkey PRIMARY KEY (id)'),

            ('clientes_documento_key', 'ALTER TABLE public.clientes ADD CONSTRAINT clientes_documento_key UNIQUE (documento)'),
            ('empleados_documento_key', 'ALTER TABLE public.empleados ADD CONSTRAINT empleados_documento_key UNIQUE (documento)'),
            ('pagos_orden_id_key', 'ALTER TABLE public.pagos ADD CONSTRAINT pagos_orden_id_key UNIQUE (orden_id)'),
            ('precios_servicio_servicio_id_tipo_vehiculo_id_key', 'ALTER TABLE public.precios_servicio ADD CONSTRAINT precios_servicio_servicio_id_tipo_vehiculo_id_key UNIQUE (servicio_id, tipo_vehiculo_id)'),
            ('productos_nombre_key', 'ALTER TABLE public.productos ADD CONSTRAINT productos_nombre_key UNIQUE (nombre)'),
            ('roles_nombre_key', 'ALTER TABLE public.roles ADD CONSTRAINT roles_nombre_key UNIQUE (nombre)'),
            ('servicios_nombre_key', 'ALTER TABLE public.servicios ADD CONSTRAINT servicios_nombre_key UNIQUE (nombre)'),
            ('tipos_vehiculo_nombre_key', 'ALTER TABLE public.tipos_vehiculo ADD CONSTRAINT tipos_vehiculo_nombre_key UNIQUE (nombre)'),
            ('usuarios_empleado_id_key', 'ALTER TABLE public.usuarios ADD CONSTRAINT usuarios_empleado_id_key UNIQUE (empleado_id)'),
            ('usuarios_nombre_usuario_key', 'ALTER TABLE public.usuarios ADD CONSTRAINT usuarios_nombre_usuario_key UNIQUE (nombre_usuario)'),
            ('vehiculos_placa_key', 'ALTER TABLE public.vehiculos ADD CONSTRAINT vehiculos_placa_key UNIQUE (placa)'),

            ('detalle_ventas_id_producto_fkey', 'ALTER TABLE public.detalle_ventas ADD CONSTRAINT detalle_ventas_id_producto_fkey FOREIGN KEY (id_producto) REFERENCES public.productos(id) ON DELETE RESTRICT'),
            ('detalle_ventas_id_venta_fkey', 'ALTER TABLE public.detalle_ventas ADD CONSTRAINT detalle_ventas_id_venta_fkey FOREIGN KEY (id_venta) REFERENCES public.ventas(id) ON DELETE CASCADE'),
            ('empleados_rol_id_fkey', 'ALTER TABLE public.empleados ADD CONSTRAINT empleados_rol_id_fkey FOREIGN KEY (rol_id) REFERENCES public.roles(id)'),
            ('orden_detalles_orden_id_fkey', 'ALTER TABLE public.orden_detalles ADD CONSTRAINT orden_detalles_orden_id_fkey FOREIGN KEY (orden_id) REFERENCES public.ordenes_servicio(id) ON DELETE CASCADE'),
            ('orden_detalles_servicio_id_fkey', 'ALTER TABLE public.orden_detalles ADD CONSTRAINT orden_detalles_servicio_id_fkey FOREIGN KEY (servicio_id) REFERENCES public.servicios(id)'),
            ('ordenes_servicio_empleado_id_fkey', 'ALTER TABLE public.ordenes_servicio ADD CONSTRAINT ordenes_servicio_empleado_id_fkey FOREIGN KEY (empleado_id) REFERENCES public.empleados(id)'),
            ('ordenes_servicio_vehiculo_id_fkey', 'ALTER TABLE public.ordenes_servicio ADD CONSTRAINT ordenes_servicio_vehiculo_id_fkey FOREIGN KEY (vehiculo_id) REFERENCES public.vehiculos(id)'),
            ('pagos_orden_id_fkey', 'ALTER TABLE public.pagos ADD CONSTRAINT pagos_orden_id_fkey FOREIGN KEY (orden_id) REFERENCES public.ordenes_servicio(id)'),
            ('precios_servicio_servicio_id_fkey', 'ALTER TABLE public.precios_servicio ADD CONSTRAINT precios_servicio_servicio_id_fkey FOREIGN KEY (servicio_id) REFERENCES public.servicios(id)'),
            ('precios_servicio_tipo_vehiculo_id_fkey', 'ALTER TABLE public.precios_servicio ADD CONSTRAINT precios_servicio_tipo_vehiculo_id_fkey FOREIGN KEY (tipo_vehiculo_id) REFERENCES public.tipos_vehiculo(id)'),
            ('usuarios_empleado_id_fkey', 'ALTER TABLE public.usuarios ADD CONSTRAINT usuarios_empleado_id_fkey FOREIGN KEY (empleado_id) REFERENCES public.empleados(id)'),
            ('vehiculos_cliente_id_fkey', 'ALTER TABLE public.vehiculos ADD CONSTRAINT vehiculos_cliente_id_fkey FOREIGN KEY (cliente_id) REFERENCES public.clientes(id)'),
            ('vehiculos_tipo_vehiculo_id_fkey', 'ALTER TABLE public.vehiculos ADD CONSTRAINT vehiculos_tipo_vehiculo_id_fkey FOREIGN KEY (tipo_vehiculo_id) REFERENCES public.tipos_vehiculo(id)'),
            ('ventas_id_cliente_fkey', 'ALTER TABLE public.ventas ADD CONSTRAINT ventas_id_cliente_fkey FOREIGN KEY (id_cliente) REFERENCES public.clientes(id) ON DELETE RESTRICT'),
            ('ventas_id_vendedor_fkey', 'ALTER TABLE public.ventas ADD CONSTRAINT ventas_id_vendedor_fkey FOREIGN KEY (id_vendedor) REFERENCES public.empleados(id) ON DELETE RESTRICT'),

            ('detalle_ventas_cantidad_check', 'ALTER TABLE public.detalle_ventas ADD CONSTRAINT detalle_ventas_cantidad_check CHECK ((cantidad > 0))'),
            ('detalle_ventas_precio_unitario_check', 'ALTER TABLE public.detalle_ventas ADD CONSTRAINT detalle_ventas_precio_unitario_check CHECK ((precio_unitario >= (0)::numeric))'),
            ('detalle_ventas_subtotal_check', 'ALTER TABLE public.detalle_ventas ADD CONSTRAINT detalle_ventas_subtotal_check CHECK ((subtotal = round(((cantidad)::numeric * precio_unitario), 2)))'),
            ('orden_detalles_precio_cobrado_check', 'ALTER TABLE public.orden_detalles ADD CONSTRAINT orden_detalles_precio_cobrado_check CHECK ((precio_cobrado >= (0)::numeric))'),
            ('ordenes_servicio_check', 'ALTER TABLE public.ordenes_servicio ADD CONSTRAINT ordenes_servicio_check CHECK (((fecha_finalizacion IS NULL) OR (fecha_finalizacion >= fecha_recepcion)))'),
            ('ordenes_servicio_total_pagar_check', 'ALTER TABLE public.ordenes_servicio ADD CONSTRAINT ordenes_servicio_total_pagar_check CHECK ((total_pagar >= (0)::numeric))'),
            ('pagos_metodo_pago_check', 'ALTER TABLE public.pagos ADD CONSTRAINT pagos_metodo_pago_check CHECK (((metodo_pago)::text = ANY ((ARRAY[''Efectivo''::character varying, ''Tarjeta''::character varying, ''Transferencia''::character varying, ''Yape''::character varying, ''Plin''::character varying, ''Cheque''::character varying])::text[])))'),
            ('pagos_monto_pagado_check', 'ALTER TABLE public.pagos ADD CONSTRAINT pagos_monto_pagado_check CHECK ((monto_pagado > (0)::numeric))'),
            ('precios_servicio_precio_check', 'ALTER TABLE public.precios_servicio ADD CONSTRAINT precios_servicio_precio_check CHECK ((precio >= (0)::numeric))'),
            ('productos_precio_check', 'ALTER TABLE public.productos ADD CONSTRAINT productos_precio_check CHECK ((precio >= (0)::numeric))'),
            ('servicios_duracion_estimada_min_check', 'ALTER TABLE public.servicios ADD CONSTRAINT servicios_duracion_estimada_min_check CHECK ((duracion_estimada_min > 0))'),
            ('usuarios_intentos_fallidos_check', 'ALTER TABLE public.usuarios ADD CONSTRAINT usuarios_intentos_fallidos_check CHECK ((intentos_fallidos >= 0))'),
            ('ck_ventas_descuento_rango', 'ALTER TABLE public.ventas ADD CONSTRAINT ck_ventas_descuento_rango CHECK (((porcentaje_descuento >= (0)::numeric) AND (porcentaje_descuento <= (20)::numeric)))'),
            ('ck_ventas_montos_coherentes', 'ALTER TABLE public.ventas ADD CONSTRAINT ck_ventas_montos_coherentes CHECK (((subtotal_bruto >= (0)::numeric) AND (monto_descuento = round(((subtotal_bruto * porcentaje_descuento) / (100)::numeric), 2)) AND (total_final = (subtotal_bruto - monto_descuento)) AND (total = total_final)))'),
            ('ck_ventas_vendedor', 'ALTER TABLE public.ventas ADD CONSTRAINT ck_ventas_vendedor CHECK (((length(btrim((vendedor)::text)) > 0) AND ((porcentaje_descuento = (0)::numeric) OR ((vendedor)::text <> ''Sin registrar''::text))))'),
            ('ventas_total_check', 'ALTER TABLE public.ventas ADD CONSTRAINT ventas_total_check CHECK ((total >= (0)::numeric))')
        ) AS v(nombre, ddl)
    LOOP
        IF NOT EXISTS (
            SELECT 1 FROM pg_constraint
            WHERE connamespace = 'public'::regnamespace
              AND conname = item.nombre
        ) THEN
            EXECUTE item.ddl;
        END IF;
    END LOOP;
END $$;

-- ---------- 4. Indices de apoyo (los de PK/UNIQUE ya los creo el motor) ----------
CREATE INDEX IF NOT EXISTS idx_detalle_ventas_venta      ON public.detalle_ventas USING btree (id_venta);
CREATE INDEX IF NOT EXISTS idx_detalle_ventas_producto   ON public.detalle_ventas USING btree (id_producto);
CREATE INDEX IF NOT EXISTS idx_empleados_rol             ON public.empleados USING btree (rol_id);
CREATE INDEX IF NOT EXISTS idx_detalles_orden            ON public.orden_detalles USING btree (orden_id);
CREATE INDEX IF NOT EXISTS idx_orden_detalles_servicio   ON public.orden_detalles USING btree (servicio_id);
CREATE INDEX IF NOT EXISTS idx_ordenes_vehiculo          ON public.ordenes_servicio USING btree (vehiculo_id);
CREATE INDEX IF NOT EXISTS idx_ordenes_empleado          ON public.ordenes_servicio USING btree (empleado_id);
CREATE INDEX IF NOT EXISTS idx_ordenes_estado            ON public.ordenes_servicio USING btree (estado);
CREATE INDEX IF NOT EXISTS idx_precios_servicio_servicio ON public.precios_servicio USING btree (servicio_id);
CREATE INDEX IF NOT EXISTS idx_precios_servicio_tipo     ON public.precios_servicio USING btree (tipo_vehiculo_id);
CREATE INDEX IF NOT EXISTS idx_vehiculos_cliente         ON public.vehiculos USING btree (cliente_id);
CREATE INDEX IF NOT EXISTS idx_ventas_fecha              ON public.ventas USING btree (fecha DESC);
CREATE INDEX IF NOT EXISTS idx_ventas_cliente            ON public.ventas USING btree (id_cliente);
CREATE INDEX IF NOT EXISTS idx_ventas_vendedor           ON public.ventas USING btree (vendedor);
CREATE INDEX IF NOT EXISTS idx_ventas_id_vendedor        ON public.ventas USING btree (id_vendedor);

-- ---------- 5. Backfill CR-01 (ventas antiguas, sin tocar las ya migradas) ----------
UPDATE public.ventas
   SET subtotal_bruto       = COALESCE(subtotal_bruto, total),
       total_final          = COALESCE(total_final, total),
       vendedor             = COALESCE(NULLIF(BTRIM(vendedor), ''), 'Sin registrar'),
       porcentaje_descuento = COALESCE(porcentaje_descuento, 0),
       monto_descuento      = COALESCE(monto_descuento, 0)
 WHERE subtotal_bruto IS NULL
    OR total_final IS NULL
    OR vendedor IS NULL
    OR BTRIM(vendedor) = '';

-- ---------- 6. Datos semilla de catalogos (sin secretos ni PII) ----------
INSERT INTO public.roles (nombre) VALUES
    ('Administrador'), ('Recepcionista'), ('Operario'), ('Vendedor')
ON CONFLICT (nombre) DO NOTHING;

INSERT INTO public.tipos_vehiculo (nombre) VALUES
    ('Automovil'), ('Motocicleta'), ('SUV'), ('Pickup')
ON CONFLICT (nombre) DO NOTHING;

INSERT INTO public.servicios (nombre, descripcion, duracion_estimada_min, activo) VALUES
    ('Lavado Exterior',     'Lavado de carroceria y llantas',                     20, true),
    ('Lavado Completo',     'Exterior mas limpieza interior',                    45, true),
    ('Encerado y Pulido',   'Cera y pulido de carroceria',                       60, true),
    ('Limpieza de Tapiz',   'Limpieza profunda de asientos y alfombras',          75, true),
    ('Shampoo de Motor',    'Lavado y desengrasado del compartimiento del motor', 40, true),
    ('Aromatizacion',       'Aromatizacion interior del vehiculo',               10, true),
    ('Aspirado Interior',   'Aspirado de asientos, alfombras y tablero',          25, true),
    ('Sanitizado Interior', 'Desinfeccion de superficies interiores',             30, true)
ON CONFLICT (nombre) DO NOTHING;

INSERT INTO public.productos (nombre, precio, activo) VALUES
    ('Lavado Exterior',     30.00, true),
    ('Lavado Completo',     60.00, true),
    ('Encerado y Pulido',   90.00, true),
    ('Limpieza de Tapiz',   75.00, true),
    ('Shampoo de Motor',    50.00, true),
    ('Aromatizacion',       10.00, true),
    ('Aspirado Interior',   20.00, true),
    ('Sanitizado Interior', 35.00, true)
ON CONFLICT (nombre) DO NOTHING;

INSERT INTO public.precios_servicio (servicio_id, tipo_vehiculo_id, precio)
SELECT s.id, t.id, v.precio
  FROM (VALUES
      ('Lavado Exterior',     'Automovil',   30.00::numeric),
      ('Lavado Exterior',     'Motocicleta', 15.00::numeric),
      ('Lavado Exterior',     'SUV',         37.50::numeric),
      ('Lavado Exterior',     'Pickup',      36.00::numeric),
      ('Lavado Completo',     'Automovil',   60.00::numeric),
      ('Lavado Completo',     'Motocicleta', 30.00::numeric),
      ('Lavado Completo',     'SUV',         75.00::numeric),
      ('Lavado Completo',     'Pickup',      72.00::numeric),
      ('Encerado y Pulido',   'Automovil',   90.00::numeric),
      ('Encerado y Pulido',   'Motocicleta', 63.00::numeric),
      ('Encerado y Pulido',   'SUV',        112.50::numeric),
      ('Encerado y Pulido',   'Pickup',     108.00::numeric),
      ('Limpieza de Tapiz',   'Automovil',   75.00::numeric),
      ('Limpieza de Tapiz',   'Motocicleta', 52.50::numeric),
      ('Limpieza de Tapiz',   'SUV',         93.75::numeric),
      ('Limpieza de Tapiz',   'Pickup',      90.00::numeric),
      ('Shampoo de Motor',    'Automovil',   50.00::numeric),
      ('Shampoo de Motor',    'Motocicleta', 35.00::numeric),
      ('Shampoo de Motor',    'SUV',         62.50::numeric),
      ('Shampoo de Motor',    'Pickup',      60.00::numeric),
      ('Aromatizacion',       'Automovil',   10.00::numeric),
      ('Aromatizacion',       'Motocicleta',  7.00::numeric),
      ('Aromatizacion',       'SUV',         12.50::numeric),
      ('Aromatizacion',       'Pickup',      12.00::numeric),
      ('Aspirado Interior',   'Automovil',   20.00::numeric),
      ('Aspirado Interior',   'Motocicleta', 14.00::numeric),
      ('Aspirado Interior',   'SUV',         25.00::numeric),
      ('Aspirado Interior',   'Pickup',      24.00::numeric),
      ('Sanitizado Interior', 'Automovil',   35.00::numeric),
      ('Sanitizado Interior', 'Motocicleta', 24.50::numeric),
      ('Sanitizado Interior', 'SUV',         43.75::numeric),
      ('Sanitizado Interior', 'Pickup',      42.00::numeric)
  ) AS v(servicio, tipo, precio)
  JOIN public.servicios s ON s.nombre = v.servicio
  JOIN public.tipos_vehiculo t ON t.nombre = v.tipo
ON CONFLICT (servicio_id, tipo_vehiculo_id) DO NOTHING;

-- ---------- 7. Sincronizar secuencias con los IDs existentes ----------
DO $$
DECLARE t text;
BEGIN
    FOREACH t IN ARRAY ARRAY[
        'clientes', 'detalle_ventas', 'empleados', 'orden_detalles',
        'ordenes_servicio', 'pagos', 'precios_servicio', 'productos',
        'roles', 'servicios', 'tipos_vehiculo', 'usuarios', 'vehiculos', 'ventas'
    ] LOOP
        EXECUTE format(
            'SELECT setval(pg_get_serial_sequence(%L, %L), GREATEST((SELECT COALESCE(MAX(id), 0) FROM public.%I), 1), true)',
            'public.' || t, 'id', t);
    END LOOP;
END $$;

COMMIT;

-- Fin de DB-scrip.sql
