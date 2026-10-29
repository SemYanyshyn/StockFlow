# SE-2: ізольований прототип правил руху товару

Це **не інтегрований production-модуль** і не завершена SE-2. У main та доступних
remote-гілках ще немає спільного `Stock` Саші. Користувач окремо підтвердив підготовку
прототипу замість другого production-типу. `StockFlow.sln` і п'ять production/test
проєктів не змінено.

`Domain/StockMovement.cs` — кандидат для майбутнього перенесення у Domain. Він
компілюється **тільки** ізольованим тестовим проєктом. `Tests/Fixtures/Stock.cs` —
явно тестова модель запропонованого контракту; production-код на неї не посилається.
Наявний `MovementType` використовується зі справжнього `StockFlow.Domain`.

Контракт UML збережено: `bool Validate(Stock stock)`, `void Apply(Stock stock)`.
Додатковий `GetValidationError(Stock? stock)` містить єдине джерело правил і
українських повідомлень. `Apply` використовує його, обчислює кількість до присвоєння
та кидає `StockMovementValidationException` без часткової зміни залишку.
Номер перевіряється на довжину 50 за схемою відкритого командного PR #123;
унікальність номера залишається відповідальністю БД/репозиторію.

## Перевірка без БД

З кореня репозиторію із SDK .NET 8:

```sh
dotnet restore StockFlow.sln
dotnet build StockFlow.sln -c Release --no-restore
dotnet test tests/StockFlow.Tests/StockFlow.Tests.csproj -c Release --no-build --no-restore
dotnet test prototypes/invoices/Tests/StockFlow.Invoices.Prototype.Tests.csproj -c Release
```

На поточному Mac тимчасовий SDK доступний як
`/private/tmp/stockflow-dotnet8/dotnet`. UI використовує свій
`EnableWindowsTargeting=true`; жоден із цих тестів не запускає WPF або PostgreSQL.
Прототипні тести запускаються окремою командою й поки не входять до стандартного
`dotnet test StockFlow.sln` чи CI.

Фактична перевірка 2026-10-30, macOS ARM64, SDK 8.0.427: Release build Solution
пройшов із 0 warnings / 0 errors; усі 54 наявні командні/інфраструктурні тести й
42 окремі доменні тести прототипу пройшли (0 failed, 0 skipped). Це перевірка
правил з тестовим Stock, а не доказ інтеграції зі Stock Саші або запуску WPF.

## Умови перенесення до production

1. Отримати погодження синхронного контракту й справжній єдиний
   `StockFlow.Domain.Stock` Саші, зокрема `ProductId`, `WarehouseId`, читання/зміну
   `Quantity`. Уточнити його конструктор і setter замість нав'язування тестового класу.
2. Перенести кандидат `StockMovement.cs` у `src/StockFlow.Domain` та тести у звичайний
   `StockFlow.Tests`. Видалити тестовий `Stock` і linked Compile кандидата, щоб не
   залишилося другого типу/джерела правил.
3. Виконати всі тести команди й домену з реальним `Stock`, перевірити DI і CI.
   Лише після цього знімати draft та вважати інтеграційну частину SE-2 готовою до рев'ю.

Прототип не реалізує SQL, репозиторій залишків, транзакції, UI або чужі модулі.
Історії #30/#32 не закриваються цим проміжним PR.
