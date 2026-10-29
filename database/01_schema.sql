BEGIN;

CREATE TABLE categories (
    id   SERIAL       PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE
);

CREATE TABLE products (
    id          SERIAL       PRIMARY KEY,
    name        VARCHAR(100) NOT NULL,
    unit        VARCHAR(10)  NOT NULL CHECK (unit IN ('piece', 'kg', 'liter')),
    category_id INT          NOT NULL REFERENCES categories (id) ON DELETE RESTRICT
);

CREATE TABLE warehouses (
    id      SERIAL       PRIMARY KEY,
    name    VARCHAR(100) NOT NULL,
    address VARCHAR(255) NULL
);

CREATE TABLE suppliers (
    id    SERIAL       PRIMARY KEY,
    name  VARCHAR(100) NOT NULL,
    phone VARCHAR(30)  NULL,
    email VARCHAR(255) NULL
);

CREATE TABLE users (
    id            SERIAL       PRIMARY KEY,
    username      VARCHAR(50)  NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    full_name     VARCHAR(100) NOT NULL,
    role          VARCHAR(20)  NOT NULL CHECK (role IN ('employee', 'admin')),
    is_active     BOOLEAN      NOT NULL DEFAULT TRUE
);

CREATE TABLE stock (
    product_id    INT NOT NULL REFERENCES products (id) ON DELETE RESTRICT,
    warehouse_id  INT NOT NULL REFERENCES warehouses (id) ON DELETE RESTRICT,
    quantity      INT NOT NULL DEFAULT 0 CHECK (quantity >= 0),
    min_threshold INT NOT NULL DEFAULT 0 CHECK (min_threshold >= 0),
    PRIMARY KEY (product_id, warehouse_id)
);

CREATE TABLE stock_movements (
    id            SERIAL      PRIMARY KEY,
    number        VARCHAR(50) NOT NULL UNIQUE,
    type          VARCHAR(20) NOT NULL CHECK (type IN ('receipt', 'write_off')),
    product_id    INT         NOT NULL REFERENCES products (id) ON DELETE RESTRICT,
    warehouse_id  INT         NOT NULL REFERENCES warehouses (id) ON DELETE RESTRICT,
    supplier_id   INT         NULL REFERENCES suppliers (id) ON DELETE RESTRICT,
    user_id       INT         NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    quantity      INT         NOT NULL CHECK (quantity > 0),
    movement_date TIMESTAMP   NOT NULL,
    note          TEXT        NULL,
    CHECK (
        (type = 'receipt' AND supplier_id IS NOT NULL) OR
        (type = 'write_off' AND supplier_id IS NULL)
    )
);

COMMIT;
