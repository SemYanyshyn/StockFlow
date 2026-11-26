# Накладні: перевірка інтеграції SE7-2–SE7-6

Draft [#258](https://github.com/SemYanyshyn/StockFlow/pull/258) — лише для CI
й демо. Не зливати його в `main`. Окремі залежності: репозиторій рухів
[#140](https://github.com/SemYanyshyn/StockFlow/pull/140), сервіс
[#146](https://github.com/SemYanyshyn/StockFlow/pull/146) і справжній репозиторій
залишків Саші [#254](https://github.com/SemYanyshyn/StockFlow/pull/254), перевірений
у цій гілці на `e8cdc46`. Зміни взаємозалежних PR переносимо окремо після
злиття їхніх основ. Жодна перевірка не використовує робочу БД.

| Що показати | Де перевіряється |
| --- | --- |
| Репозиторій зберігає й читає накладні, не обходячи обмежень PostgreSQL | `tests/StockFlow.DatabaseChecks/InvoiceDatabaseChecks.cs` |
| Недостатній залишок: `Add` / `Save` не викликано | `StockMovementServiceTests.InsufficientStockDoesNotAddOrSaveAndReleasesTransaction` |
| Після збою `Save` попередній `Add` відкочується | `StockMovementServiceTests.SaveFailureRollsBackStagedFakeMovementAndStock` |
| Збій `Add`: відкіт, без викликів `Save` та `Commit` | `StockMovementServiceTests.AddFailureRollsBackWithoutCallingSaveOrCommit` |
| Списання 8 і 8 із 10; два перші надходження; послідовний контроль | `tests/StockFlow.DatabaseChecks/InvoiceConcurrencyChecks.cs` |
| Форма, роль, постачальник, помилка, збереження й оновлення списку | `tests/StockFlow.WindowsChecks/Program.Invoices.cs` |

## SE7-6: до і після

До: `Register` перевіряв користувача, збирав рух, відкривав транзакцію,
перехоплював різні помилки і дублював виклик `Rollback`.

Після: `Register` перевіряє користувача й описує послідовність
`GetForUpdate → Apply → Add → Save`. Метод `InTransaction` відповідає за
`Begin / Commit / Rollback / Dispose`; `SafeMessage` зіставляє винятки
з тими самими безпечними повідомленнями. Рівень ізоляції й SQL не змінено.

До та після пройшли ті самі **24** тести сервісу й DI:

```sh
dotnet test tests/StockFlow.Tests/StockFlow.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~StockMovementServiceTests|FullyQualifiedName~StockMovementDependencyInjectionTests'
```

[CI до рефакторингу](https://github.com/SemYanyshyn/StockFlow/actions/runs/45892344583) ·
[CI готового кандидата](https://github.com/SemYanyshyn/StockFlow/pull/258/checks)

## SE7-4: як повторити

На .NET SDK 8.0.427 / PostgreSQL 17.12 пройшли Release-збірка з `-warnaserror`,
417 unit-тестів і 198 перевірок БД. Сценарії конкуренції пройшли **20/20**
прогонів поспіль. Для кожної пари створено окремий товар; обидва працівники
отримують знімки у двох справжніх транзакціях до продовження за бар'єром.
Немає повторних спроб, які приховують збої. Є 10-секундний тайм-аут на кожну
задачу. При одночасних перших надходженнях одна транзакція фіксується, друга
отримує «Спробуйте ще раз», а невдалий рух відсутній у БД.

Встановіть `STOCKFLOW_TEST_CONNECTION` для локальної
`stockflow_isolated_*` бази, ініціалізованої `database/01_schema.sql` і
`database/02_seed.sql`:

```sh
dotnet build tests/StockFlow.DatabaseChecks -c Release -warnaserror
for run in $(seq 1 20); do
  dotnet run --project tests/StockFlow.DatabaseChecks -c Release --no-build -- --invoice-concurrency-only || exit 1
done
```

## SE7-5: номер і демо

Номер «НК-…» призначається з наявної PostgreSQL-послідовності; пропуски
можливі після скасування форми. Унікальність додатково захищає обмеження БД.
Окремий тест перевіряє 32 одночасні резервування номера.

На демо зайдіть Працівником, відкрийте «Накладні» → «Створити накладну».
У надходженні виберіть постачальника; у списанні це поле ховається.
Спробуйте списати понад залишок, потім збережіть коректний рух і покажіть
оновлений список. Під Адміном кнопку створення не видно.

На Windows перевірки можна повторити командою
`.\scripts\Verify-Isolated.ps1 -PostgreSqlBin $env:PGBIN`; скрипт
створює окремі тестові БД. WindowsChecks запускає WPF програмно,
це не ручне клацання в настільному застосунку.
