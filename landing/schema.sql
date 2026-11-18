CREATE TABLE IF NOT EXISTS solicitudes (
  id TEXT PRIMARY KEY NOT NULL,
  creado TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  tipo TEXT NOT NULL CHECK (tipo IN ('empresa', 'particular')),
  nombre TEXT NOT NULL,
  correo TEXT NOT NULL,
  telefono TEXT,
  empresa TEXT,
  modulo TEXT,
  mensaje TEXT NOT NULL
);
