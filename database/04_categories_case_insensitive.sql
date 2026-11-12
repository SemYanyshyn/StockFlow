-- Apply to existing databases. Does not merge, rename, or remove any records.
SET client_encoding = 'UTF8';
BEGIN;
LOCK TABLE categories IN SHARE ROW EXCLUSIVE MODE;
CREATE COLLATION IF NOT EXISTS stockflow_case_insensitive
    (provider = icu, locale = 'und-u-ks-level2', deterministic = false);

DO $$
DECLARE conflicts text;
BEGIN
    IF NOT ('Напої' COLLATE stockflow_case_insensitive = 'напої' COLLATE stockflow_case_insensitive)
       OR ('Напої' COLLATE stockflow_case_insensitive = 'Напоі' COLLATE stockflow_case_insensitive) THEN
        RAISE EXCEPTION 'ICU collation does not provide the required Ukrainian case/accent behavior';
    END IF;
    SELECT string_agg(ids, '; ') INTO conflicts FROM (
        SELECT string_agg(id::text || ': ' || name, ', ' ORDER BY id) AS ids
        FROM categories GROUP BY name COLLATE stockflow_case_insensitive HAVING count(*) > 1
    ) duplicates;
    IF conflicts IS NOT NULL THEN
        RAISE EXCEPTION 'Category names conflict (case insensitive). Rename the listed categories and rerun migration.'
            USING DETAIL = conflicts;
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS categories_name_case_insensitive_key
    ON categories (name COLLATE stockflow_case_insensitive);
COMMIT;
