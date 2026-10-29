SET client_encoding = 'UTF8';

BEGIN;

INSERT INTO users (username, password_hash, full_name, role) VALUES
    ('admin',    'placeholder-not-a-hash', 'Адміністратор',    'admin'),
    ('employee', 'placeholder-not-a-hash', 'Працівник складу', 'employee');

INSERT INTO categories (name) VALUES
    ('Молочні продукти'),
    ('Напої'),
    ('Овочі та фрукти'),
    ('Бакалія');

INSERT INTO products (name, unit, category_id) VALUES
    ('Молоко 2,5%',          'liter', (SELECT id FROM categories WHERE name = 'Молочні продукти')),
    ('Кефір',                'liter', (SELECT id FROM categories WHERE name = 'Молочні продукти')),
    ('Сир твердий',          'kg',    (SELECT id FROM categories WHERE name = 'Молочні продукти')),
    ('Сік апельсиновий',     'liter', (SELECT id FROM categories WHERE name = 'Напої')),
    ('Вода мінеральна',      'liter', (SELECT id FROM categories WHERE name = 'Напої')),
    ('Яблука',               'kg',    (SELECT id FROM categories WHERE name = 'Овочі та фрукти')),
    ('Картопля',             'kg',    (SELECT id FROM categories WHERE name = 'Овочі та фрукти')),
    ('Гречка',               'kg',    (SELECT id FROM categories WHERE name = 'Бакалія')),
    ('Шоколад молочний',     'piece', (SELECT id FROM categories WHERE name = 'Бакалія')),
    ('Печиво вівсяне',       'piece', (SELECT id FROM categories WHERE name = 'Бакалія'));

INSERT INTO warehouses (name, address) VALUES
    ('Основний склад', 'м. Київ, вул. Складська, 1'),
    ('Склад №2',       NULL);

INSERT INTO suppliers (name, phone, email) VALUES
    ('ТОВ «Молочна ферма»', '+380 44 000 00 01', 'sales@dairy.example'),
    ('ФОП Овочевий двір',   '+380 44 000 00 02', NULL),
    ('ТОВ «Бакалія Опт»',   NULL,                'orders@grocery.example');

INSERT INTO stock (product_id, warehouse_id, quantity, min_threshold) VALUES
    ((SELECT id FROM products WHERE name = 'Молоко 2,5%'),      (SELECT id FROM warehouses WHERE name = 'Основний склад'), 40,  10),
    ((SELECT id FROM products WHERE name = 'Кефір'),            (SELECT id FROM warehouses WHERE name = 'Основний склад'), 4,   10),
    ((SELECT id FROM products WHERE name = 'Гречка'),           (SELECT id FROM warehouses WHERE name = 'Основний склад'), 120, 20),
    ((SELECT id FROM products WHERE name = 'Шоколад молочний'), (SELECT id FROM warehouses WHERE name = 'Основний склад'), 3,   5),
    ((SELECT id FROM products WHERE name = 'Яблука'),           (SELECT id FROM warehouses WHERE name = 'Склад №2'),       15,  0),
    ((SELECT id FROM products WHERE name = 'Картопля'),         (SELECT id FROM warehouses WHERE name = 'Склад №2'),       60,  25);

COMMIT;
