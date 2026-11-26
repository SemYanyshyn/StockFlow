SET client_encoding = 'UTF8';
-- Simulate the original UNIQUE(name) schema and placeholder seed on an isolated DB only.
DROP INDEX categories_name_case_insensitive_key;
INSERT INTO categories(name) VALUES ('напої');
UPDATE users SET password_hash='placeholder-not-a-hash' WHERE username='admin';
UPDATE users SET full_name='Збережене ім’я', is_active=false WHERE username='employee';
INSERT INTO stock_movements(number,type,product_id,warehouse_id,supplier_id,user_id,quantity,movement_date)
VALUES('legacy-history','receipt',1,1,1,2,1,CURRENT_TIMESTAMP);
CREATE TABLE check_snapshot AS SELECT u.id, u.username, u.password_hash, u.full_name, u.role, u.is_active FROM users u;
