# Етап 6: підготовлені повідомлення, не надіслані

## Саші

У main вже є твій Stock/Warehouse/контракт із PR124. У PR125 пропонуємо точне
GetAll(int? warehouseId) без транзакції; GetForUpdate/Save — синхронні IDbTransaction.
Підтверди, будь ласка: новий Stock з Quantity0/threshold0 за відсутності рядка,
Save UPSERT змінює лише quantity, обидва методи не володіють переданим connection.
Пропонуємо Serializable на всю Register без автоматичного retry, з відмовою
«Спробуйте ще раз» на40001/40P01. Дві реальні перекривні SQL-проби дали40001;
SELECT FOR UPDATE не блокує відсутній рядок. Чи погоджуєш цей варіант?

Потрібен твій мінімальний справжній StockRepository; модуль рухів не реєструє
заглушку. Після реєстрації IStockRepository можна викликати AddStockMovementServices.
У review124 є2коментарі; відновлення AddStockFlow/конфігурації/DI validation таDispose
підготовлено в сервісномуPR146 зі збереженням усієї оболонки/навігації.

## Мар'яну: захист main уже налаштовано

Після фактично успішного mainCI40968466801 й окремого підтвердження Семена
до чинного Main branch protection додано обов'язковий build-and-test саме від
GitHub Actions. Зберегли PR,2approvals,заборони force push/видалення й bypass[].
Після save effective rules main перевірені. Дубля classic protection немає.
Прошу review домену139 й наступних залежних PR за ротацією; потрібен ще другий approval.

## Мар'яну: номерогенерація для SE-8, ще не застосована

Потрібна погоджена sequence для автоматичного нередагованого НК-017 після НК-016.
Пропозиція: окрема bigint sequence, nextval у межах реєстрації, формат «НК-» +
число мінімум3цифри. Під час одноразової міграції початкове значення визначати
з наявних номерів тільки під table lock; у звичайній операції MAX+1 не використовувати.
Sequence гарантує унікальність значень, але пропуски після rollback нормальні.
Міграцію й генератор поки не додано: потрібно погодження, реалізація в окремомуSE8PR.

## Тані: критерії US-05/US-06 і список

Перш ніж додавати форму, синхронізувати stories/issue30/32: автоматичний
нередагований унікальний номер, дата, quantity>0, Receipt із supplier,
WriteOff без supplier, створення тільки Employee, read-only відповідальна особа
із ICurrentUserContext. За недостатнього залишку жодного запису й зміни quantity.

Фактичний issue30 ще містить помилковий рядок:
`The form is not accessible to the Employee and Admin roles`.
Цільова заміна:
`Only Employee can open the creation form and register a stock receipt. Admin cannot create stock movements; the service also enforces this rule.`
Доповнення acceptance criteria:

- The form includes an automatically generated read-only unique number, date, product, warehouse, positive integer quantity, supplier for receipts, optional note, and the read-only current user.
- After a successful save the movement and stock quantity are committed in one transaction; failure leaves both unchanged.
- For write-offs the supplier is absent and a quantity above available stock is rejected.

Issuebody не змінено без узгодження. Exact-title issue `View the list of stock movements`
не знайдено у поточному пошуку; US-21 не є GitHub issue21 (той стосується товарів).
Задача наступної ітерації #133 вже відкрита; перед формою узгодити/створити відповідну
історію списку й прив'язати її, не закриваючи #30/#32 проміжними PR.
