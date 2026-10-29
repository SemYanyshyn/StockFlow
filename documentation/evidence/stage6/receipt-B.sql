SET application_name='sf_65ec4ff6_receipt_b';
BEGIN ISOLATION LEVEL SERIALIZABLE;
SELECT quantity FROM stock WHERE product_id=10 AND warehouse_id=2 FOR UPDATE;
\echo B_ABSENCE_READ
INSERT INTO stock_movements
    (number,type,product_id,warehouse_id,supplier_id,user_id,quantity,movement_date,note)
    VALUES ('PROBE-65ec4ff6-RECEIPT-B','receipt',10,2,1,2,7,
            TIMESTAMP '2026-10-30 12:00:00','isolated Serializable mechanism probe');
INSERT INTO stock(product_id,warehouse_id,quantity,min_threshold)
    VALUES (10,2,7,0)
    ON CONFLICT (product_id,warehouse_id) DO UPDATE SET quantity=EXCLUDED.quantity;
COMMIT;
\q