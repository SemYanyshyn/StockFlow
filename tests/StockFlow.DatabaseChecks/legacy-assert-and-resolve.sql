SET client_encoding = 'UTF8';
DO $$ BEGIN
    IF (SELECT count(*) FROM categories) <> 5 OR
       (SELECT count(*) FROM stock_movements WHERE number='legacy-history' AND user_id=2 AND supplier_id=1) <> 1 OR
       EXISTS(SELECT 1 FROM users u JOIN check_snapshot s USING(id)
              WHERE u.username <> s.username OR u.full_name <> s.full_name OR u.role <> s.role OR u.is_active <> s.is_active) OR
       (SELECT password_hash='placeholder-not-a-hash' FROM users WHERE username='admin') OR
       (SELECT u.password_hash<>s.password_hash FROM users u JOIN check_snapshot s USING(id) WHERE u.username='employee') OR
       to_regclass('categories_name_case_insensitive_key') IS NOT NULL THEN
        RAISE EXCEPTION 'Legacy migration changed protected data or failed to roll back';
    END IF;
END $$;
-- Explicit resolution: rename only the conflicting directory row, retain its ID and references.
UPDATE categories SET name='Безалкогольні напої' WHERE name='напої' COLLATE "C";
