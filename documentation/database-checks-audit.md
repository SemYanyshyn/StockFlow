# Аудит обмежень PostgreSQL (MA7-4, #219)

Схема [`database/01_schema.sql`](../database/01_schema.sql). У стовпці «Назва перевірки» вказано текст із журналу. Назви для кількох колонок наведено за шаблоном з `<column>`. «Було» позначає покриття у [`Program.cs`](../tests/StockFlow.DatabaseChecks/Program.cs) на `b7683c7`. Нові перевірки містяться у [`SchemaConstraintChecks.cs`](../tests/StockFlow.DatabaseChecks/SchemaConstraintChecks.cs).

## Категорії, товари, склади, постачальники й користувачі

| Обмеження | Сценарій | Назва перевірки | Результат |
| --- | --- | --- | --- |
| `categories.id` SERIAL, PK, NOT NULL | Два згенеровані ID, повтор ID, NULL | `categories.id SERIAL generates distinct IDs`, `categories_pkey rejects duplicate ID`, `categories.id rejects NULL` | PASS |
| `categories.name` VARCHAR(100), NOT NULL | 100, 101 символ, NULL | `categories.name VARCHAR(100) accepts the limit / rejects limit plus one`, `categories.name rejects NULL` | PASS |
| `categories_name_key` | Точний дублікат, SQLSTATE 23505 і назва UNIQUE | `categories_name_key rejects exact duplicate` | PASS |
| `categories_name_case_insensitive_key` та ICU | Варіант регістру, українські літери, діакритика | Було `ICU unique index rejects Напої/напої in locale C`, `Ukrainian Cyrillic case mapping`, `ICU keeps Ukrainian accents distinct`, `category Update to Напої in other case: 23505 translated`. Додано `categories_name_case_insensitive_key rejects case variant` із точною назвою індексу | PASS |
| `products.id` SERIAL, PK, NOT NULL | Два ID, повтор, NULL | `products.id SERIAL generates distinct IDs`, `products_pkey rejects duplicate ID`, `products.id rejects NULL` | PASS |
| `products.name` VARCHAR(100), NOT NULL | 100, 101 символ, NULL | `products.name VARCHAR(100) accepts the limit / rejects limit plus one`, `products.name rejects NULL` | PASS |
| `products.unit` VARCHAR(10), NOT NULL | 11 символів, NULL | `products.unit VARCHAR(10) rejects limit plus one`, `products.unit rejects NULL` | PASS |
| `products_unit_check` | Приймаються `piece`, `kg`, `liter`. Відхиляється `box` | `products_unit_check rejects box`, позитивна вставка фікстури `piece`. Було `product Add and GetById map unit kg both ways`, `product Update saves name and unit` | PASS |
| `products.category_id` NOT NULL | NULL | `products.category_id rejects NULL` | PASS |
| `products_category_id_fkey` і RESTRICT | Неіснуюча категорія, видалення категорії з товаром | Було `product with missing category rejected by FK 23503`, `category with products: RESTRICT 23503 translated`. Додано `products_category_id_fkey rejects missing parent / declares ON DELETE RESTRICT / blocks parent deletion` | PASS |
| `warehouses.id` SERIAL, PK, NOT NULL | Два ID, повтор, NULL | `warehouses.id SERIAL generates distinct IDs`, `warehouses_pkey rejects duplicate ID`, `warehouses.id rejects NULL` | PASS |
| `warehouses.name` VARCHAR(100), NOT NULL | 100, 101 символ, NULL | `warehouses.name VARCHAR(100) accepts the limit / rejects limit plus one`, `warehouses.name rejects NULL` | PASS |
| `warehouses.address` VARCHAR(200), NULL | 200, 201 символ, пропущена адреса | `warehouses.address VARCHAR(200) accepts the limit / rejects limit plus one`, `warehouses.address may be NULL` | PASS |
| `suppliers.id` SERIAL, PK, NOT NULL | Два ID, повтор, NULL | `suppliers.id SERIAL generates distinct IDs`, `suppliers_pkey rejects duplicate ID`, `suppliers.id rejects NULL` | PASS |
| `suppliers.name` VARCHAR(100), NOT NULL | 100, 101 символ, NULL | `suppliers.name VARCHAR(100) accepts the limit / rejects limit plus one`, `suppliers.name rejects NULL` | PASS |
| `suppliers.phone` VARCHAR(30), NULL | 30, 31 символ, SQL NULL | `suppliers.phone VARCHAR(30) accepts the limit / rejects limit plus one`. Було `optional contacts are SQL NULL` | PASS |
| `suppliers.email` VARCHAR(100), NULL | 100, 101 символ, SQL NULL | `suppliers.email VARCHAR(100) accepts the limit / rejects limit plus one`. Було `optional contacts are SQL NULL` | PASS |
| `users.id` SERIAL, PK, NOT NULL | Два ID, повтор, NULL | `users.id SERIAL generates distinct IDs`, `users_pkey rejects duplicate ID`, `users.id rejects NULL` | PASS |
| `users.username` VARCHAR(50), NOT NULL | 50, 51 символ, NULL | `users.username VARCHAR(50) accepts the limit / rejects limit plus one`, `users.username rejects NULL` | PASS |
| `users_username_key` | Точний дублікат відхиляється, інший регістр приймається | `users_username_key rejects exact duplicate`. Було `Користувач із таким логіном уже існує.`, `existing case-sensitive UNIQUE(username) rule preserved` | PASS |
| `users.password_hash` VARCHAR(255), NOT NULL | 255, 256 символів, NULL | `users.password_hash VARCHAR(255) accepts the limit / rejects limit plus one`, `users.password_hash rejects NULL` | PASS |
| `users.full_name` VARCHAR(100), NOT NULL | 100, 101 символ, NULL | `users.full_name VARCHAR(100) accepts the limit / rejects limit plus one`, `users.full_name rejects NULL` | PASS |
| `users.role` VARCHAR(20), NOT NULL | 21 символ, NULL | `users.role VARCHAR(20) rejects limit plus one`, `users.role rejects NULL` | PASS |
| `users_role_check` | `admin` і `employee` приймаються, `owner` відхиляється | `users_role_check rejects owner`. Було `seed admin login and role`, `seed employee login and role` | PASS |
| `users.is_active` DEFAULT TRUE, NOT NULL | Пропустити колонку, передати NULL | `users.is_active defaults to TRUE`, `users.is_active rejects NULL` | PASS |

## Залишки й рухи

| Обмеження | Сценарій | Назва перевірки | Результат |
| --- | --- | --- | --- |
| `stock_pkey (product_id, warehouse_id)` | Дублікат пари, той самий товар на іншому складі, інший товар на тому самому складі | `stock_pkey rejects duplicate pair`, `stock_pkey accepts the same product in another warehouse`, `stock_pkey accepts another product in the same warehouse` | PASS |
| `stock` NOT NULL | NULL окремо для `product_id`, `warehouse_id`, `quantity`, `min_threshold` | `stock.<column> rejects NULL` | PASS |
| `stock.quantity` INT, DEFAULT 0, `stock_quantity_check` | Пропущене поле, 0, 2147483647, 2147483648, −1 | `stock.quantity defaults to zero / accepts explicit zero / accepts INT maximum / rejects INT maximum plus one`, `stock_quantity_check rejects -1` | PASS |
| `stock.min_threshold` INT, DEFAULT 0, `stock_min_threshold_check` | Пропущене поле, 0, 2147483647, 2147483648, −1 | `stock.min_threshold defaults to zero / accepts explicit zero / accepts INT maximum / rejects INT maximum plus one`, `stock_min_threshold_check rejects -1` | PASS |
| `stock_product_id_fkey` і RESTRICT | Неіснуючий товар, видалення товару із залишком | Було `product with stock: RESTRICT 23503 translated`. Додано `stock_product_id_fkey rejects missing parent / declares ON DELETE RESTRICT / blocks parent deletion` | PASS |
| `stock_warehouse_id_fkey` і RESTRICT | Неіснуючий склад, видалення складу із залишком | `stock_warehouse_id_fkey rejects missing parent / declares ON DELETE RESTRICT / blocks parent deletion` | PASS |
| `stock_movements.id` SERIAL, PK | Два ID, повтор ID | `stock_movements.id SERIAL generates distinct IDs`, `stock_movements_pkey rejects duplicate ID` | PASS |
| `stock_movements` NOT NULL | NULL окремо для `id`, `number`, `type`, `product_id`, `warehouse_id`, `user_id`, `quantity`, `movement_date` | `stock_movements.<column> rejects NULL` | PASS |
| `stock_movements.number` VARCHAR(50), UNIQUE | 50, 51 символ, дублікат номера | `stock_movements.number VARCHAR(50) accepts the limit / rejects limit plus one`, `stock_movements_number_key rejects duplicate number` | PASS |
| `stock_movements.type` VARCHAR(20) | 21 символ | `stock_movements.type VARCHAR(20) rejects limit plus one` | PASS |
| `stock_movements_type_check` | `receipt` і `write_off` у справжній таблиці, `transfer` у тимчасовій копії з окремим CHECK | `stock_movements accepts receipt with supplier, quantity 1 and NULL note`, `stock_movements accepts write_off without supplier`, `stock_movements_type_check rejects transfer` | PASS |
| `stock_movements_quantity_check` | 1 приймається, 0 і −1 відхиляються | `stock_movements accepts receipt with supplier, quantity 1 and NULL note`, `stock_movements_quantity_check rejects 0 / rejects -1` | PASS |
| `stock_movements_check` і nullable `supplier_id` | Надходження з постачальником і списання без нього приймаються. Надходження без постачальника й списання з ним відхиляються | `stock_movements accepts receipt with supplier, quantity 1 and NULL note`, `stock_movements accepts write_off without supplier`, `stock_movements_check rejects receipt without supplier / rejects write_off with supplier` | PASS |
| `stock_movements.note` NULL | Рух без примітки | `stock_movements accepts receipt with supplier, quantity 1 and NULL note` | PASS |
| `stock_movements_product_id_fkey` і RESTRICT | Неіснуючий товар, видалення товару з рухом | Було `product with movement: RESTRICT 23503 translated`. Додано `stock_movements_product_id_fkey rejects missing parent / declares ON DELETE RESTRICT / blocks parent deletion` | PASS |
| `stock_movements_warehouse_id_fkey` і RESTRICT | Неіснуючий склад, видалення складу з рухом | `stock_movements_warehouse_id_fkey rejects missing parent / declares ON DELETE RESTRICT / blocks parent deletion` | PASS |
| `stock_movements_supplier_id_fkey` і RESTRICT | Неіснуючий постачальник, видалення постачальника з рухом | Було `supplier FK 23503 translated by service`. Додано `stock_movements_supplier_id_fkey rejects missing parent / declares ON DELETE RESTRICT / blocks parent deletion` | PASS |
| `stock_movements_user_id_fkey` і RESTRICT | Неіснуючий користувач, видалення користувача з рухом | Було `user FK 23503 translated as secondary protection`. Додано `stock_movements_user_id_fkey rejects missing parent / declares ON DELETE RESTRICT / blocks parent deletion` | PASS |

Кожний негативний SQL-сценарій звіряє SQLSTATE. Для CHECK, UNIQUE і FK він також звіряє назву обмеження, для NOT NULL — назву колонки. Для `ON DELETE RESTRICT` додатково перевіряється дія FK в каталозі PostgreSQL. Невідомий тип руху порушує і CHECK типу, і CHECK постачальника. Щоб перевірити саме CHECK типу, використовується тимчасова таблиця, створена через `LIKE … INCLUDING CONSTRAINTS`, з якої прибирається тільки CHECK постачальника.

Перевірено з .NET SDK 8.0.427 на чистих `stockflow_isolated_*` з `01_schema.sql` і `02_seed.sql`. Windows, PostgreSQL 16.3 та `Verify-Isolated.ps1` — 169 перевірок БД, 27 WPF, міграції пройшли. Ubuntu 24.04 під WSL, PostgreSQL 16.15 з ICU та locale C — 169 перевірок БД.
