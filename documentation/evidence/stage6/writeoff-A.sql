SET application_name='sf_65ec4ff6_write_a';
BEGIN ISOLATION LEVEL SERIALIZABLE;
SELECT quantity FROM stock WHERE product_id=1 AND warehouse_id=1 FOR UPDATE;
\echo A_ROW_LOCKED
INSERT INTO stock_movements
    (number,type,product_id,warehouse_id,supplier_id,user_id,quantity,movement_date,note)
    VALUES ('PROBE-65ec4ff6-WRITE-A','write_off',1,1,NULL,2,8,
            TIMESTAMP '2026-10-30 12:00:00','isolated Serializable mechanism probe');
INSERT INTO stock(product_id,warehouse_id,quantity,min_threshold)
    VALUES (1,1,2,0)
    ON CONFLICT (product_id,warehouse_id) DO UPDATE SET quantity=EXCLUDED.quantity;
COMMIT;
\q