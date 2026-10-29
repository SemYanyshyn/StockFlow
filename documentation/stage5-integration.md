# Етап 5: інтеграція частини Семена

Стан на 2026-10-29. Цей файл доповнює лише технічну частину Семена;
загальний README залишається відповідальністю Тані.

## Гілки та порядок PR

1. `feature/solution-skeleton` → `main`: [PR #114](https://github.com/SemYanyshyn/StockFlow/pull/114).
   Лише Solution, п'ять проєктів, посилання, пакети, .gitignore та стандартне WPF-вікно.
   У порожньому тестовому проєкті тестів ще немає; це не виконання доменних тестів.
2. `feature/semen-stage5-data-domain` → `feature/solution-skeleton` до злиття каркаса.
   Це залежна гілка з фабрикою, конфігурацією, DI, `MovementType`, тестами та документацією.
   Не відкривати її незалежно в main з повторним diff каркаса.
   Після merge першого PR — перевірити спільну історію й diff, потім змінити базу на main.
   Якщо перший PR squash/rebase-merged, найпростіше без force push створити нову гілку
   від актуального main і cherry-pick лише коміт другої гілки; закрити старий draft PR.
   При звичайному merge commit достатньо перевірити diff і змінити base PR.

Другий PR залишається draft до інтеграції `StockMovement` та його тестів із реальним
`Stock` Саші. Обидві гілки компілюються. Main, workflow і правила GitHub не змінювалися.

## Фабрика та конфігурація

`StockFlow.DataAccess.DbConnectionFactory(IConfiguration)` читає
`ConnectionStrings:StockFlow`. `CreateConnection(): NpgsqlConnection` щоразу повертає
нове **закрите** підключення. Ні створення фабрики, ні побудова DI-контейнера не
підключаються до сервера. Некоректна/відсутня конфігурація дає очищений від значень
`InvalidOperationException` без первинного винятку, який міг містити секрети.
Перевіряються синтаксис Npgsql, Host, Database, Username, діапазон Port і placeholder
пароля з прикладу. Пароль не є обов'язковим у коді: можливі інші способи автентифікації
PostgreSQL. Фактична доступність сервера й права перевіряються лише при Open/OpenAsync.

Репозиторій відкриває і звільняє своє підключення для окремої операції через
`using`/`await using`. Якщо сервіс об'єднує репозиторії в транзакцію, він володіє
підключенням і транзакцією, передає їх репозиторіям, виконує commit/rollback і Dispose;
репозиторії не закривають передане ззовні підключення. Фабрика транзакціями не керує.

`services.AddStockFlow(configuration)` у BusinessLogic реєструє singleton фабрики
ліниво. UI викликає тільки цей метод; до DataAccess не звертається. Справжні
репозиторії/сервіси власники модулів додадуть тут після реалізації. Заглушок немає.

`StockFlowConfiguration.Load()` читає необов'язковий `appsettings.json` біля executable
через `AppContext.BaseDirectory`, а потім змінні з префіксом `STOCKFLOW_`.
Ключ `STOCKFLOW_ConnectionStrings__StockFlow` перевизначає JSON. Перевантаження з
явною директорією призначене для контрольованого host/test оточення. Парсер не
використовує випадковий current working directory й не завантажує example-файл.

Для локального підключення на Windows скопіювати
`src/StockFlow.UI/appsettings.example.json` у `src/StockFlow.UI/appsettings.json`
і замінити `<set-locally>` своїм локальним значенням. Локальний файл ігнорується Git
і копіюється в build/publish output лише коли існує. Реальних секретів у Git немає.
При видаленні раніше скопійованого локального файла виконати `dotnet clean`, щоб
прибрати його стару копію з output. Чистий checkout не містить конфігурації і
збирається; фабрика потрібна лише при майбутніх операціях з даними.

## Контракт, потрібний від Саші

`Stock` відсутній у main і всіх доступних remote-гілках на час перевірки.
Узгодити та додати **єдиний** production-тип `StockFlow.Domain.Stock`, мінімально:

```csharp
namespace StockFlow.Domain;

// Контракт для узгодження; цей фрагмент не є реалізацією чи production-заглушкою.
public class Stock
{
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public int Quantity { get; set; }
    public int MinThreshold { get; set; }
}
```

Для `StockMovement` потрібні читання `ProductId`, `WarehouseId`, `Quantity` та
зміна `Quantity`. `MinThreshold` належить UML Саші, рух його не змінює.
Використовуємо PascalCase C#; назви колонок PostgreSQL лишаються snake_case.
Якщо Саша обере конструктор або обмежений setter, він має надати еквівалентне
створення нульового залишку та атомарну зміну кількості. Погодити це до реалізації
`StockMovement`, не створюючи паралельний Stock, dynamic або object.

В DataAccess потрібен `StockFlow.DataAccess.IStockRepository` із погодженими
синхронними ADO.NET-сигнатурами (асинхронний варіант узгодити окремо):

```csharp
Stock GetForUpdate(int productId, int warehouseId, System.Data.IDbTransaction transaction);
void Save(Stock stock, System.Data.IDbTransaction transaction);
```

Обидва методи використовують підключення переданої транзакції. `GetForUpdate`
читає/блокує **наявний** рядок. Якщо його немає, повертає новий Stock для тієї ж
пари з Quantity=0; `Save` створює рядок. `SELECT FOR UPDATE` не блокує відсутній
рядок. Для конкурентного першого надходження потрібно погодити серіалізацію пари
(наприклад, транзакційний advisory lock до читання) або SERIALIZABLE з повтором
всієї транзакції. Один лише UNIQUE не запобігає втраті паралельного збільшення.
SQL і реалізація репозиторію залишаються Саші/Мар'яну.

## Що залишається реалізувати в StockMovement

`MovementType` уже є в Domain: `Receipt`, `WriteOff`. Репозиторій має явно
відображати їх у `receipt` / `write_off`; не покладатися на числові значення enum.
Повний `StockMovement` поки не додано, бо не можна виконати UML-контракт без Stock.

Поля за UML та ER:

| C# | Тип | Зв'язок / правило |
|---|---|---|
| Id | int | ідентифікатор запису, 0 до збереження |
| Number | string | непорожній номер; унікальність забезпечують БД/репозиторій |
| Type | MovementType | тільки Receipt / WriteOff |
| Quantity | int | > 0 |
| MovementDate | DateTime | дата руху, не default |
| Note | string? | необов'язкова примітка |
| ProductId | int | > 0, FK products |
| WarehouseId | int | > 0, FK warehouses |
| UserId | int | > 0, FK users |
| SupplierId | int? | > 0 для Receipt; null для WriteOff за ER |

FK ідентифікатори представляють зв'язки без створення чужих сутностей-заглушок.
Ціна, рядки накладної, SQL і збереження до цього класу не належать.

Майбутні сигнатури лишаються `bool Validate(Stock stock)` і `void Apply(Stock stock)`.
Validate перевіряє всі поля, ту ж пару товар/склад, невід'ємний поточний залишок,
правило постачальника й допустиму кількість. Для Receipt також перевіряється
переповнення int; для WriteOff — достатність залишку. Validate не мутує стан.
Apply повторно перевіряє рух, обчислює нову кількість до присвоєння і лише потім
змінює Quantity; для некоректного руху кидає InvalidOperationException без змін Stock.

Після появи справжнього Stock додати виконувані тести: перше Receipt від 0,
Receipt із залишком, WriteOff частини й усього залишку, перевищення залишку,
кількості 0/-1, неправильні товар/склад, неправильний постачальник, невідомий enum,
переповнення int, відсутність мутації в Validate та незмінність Stock при відмові Apply.
Це поки критерії приймання, а не запущені або вимкнені тести.

## Команди перевірки

Потрібні SDK .NET 8 (8.0.4xx+) та runtime .NET 8. На цьому Mac вони встановлені
окремо у `/private/tmp/stockflow-dotnet8`; системний SDK .NET 10 не змінено.
Тимчасовий SDK може зникнути після очищення /tmp; для постійної роботи встановити
SDK .NET 8 офіційним installer або dotnet-install у користувацьку директорію.

macOS (ARM64), з кореня репозиторію:

```sh
export PATH="/private/tmp/stockflow-dotnet8:$PATH"
dotnet sln StockFlow.sln list
dotnet restore StockFlow.sln -p:EnableWindowsTargeting=true
dotnet build StockFlow.sln -c Release --no-restore -p:EnableWindowsTargeting=true
dotnet test tests/StockFlow.Tests/StockFlow.Tests.csproj -c Release --no-build --no-restore
```

EnableWindowsTargeting уже увімкнено лише в UI.csproj, тому прапорець у командах
не обов'язковий. Решта чотири проєкти лишаються net8.0. Тести не потребують
PostgreSQL, локальної конфігурації, WindowsDesktop runtime або мережі після restore.

Windows (PowerShell), з кореня репозиторію з установленим SDK .NET 8:

```powershell
dotnet sln StockFlow.sln list
dotnet restore StockFlow.sln
dotnet build StockFlow.sln -c Release --no-restore
dotnet test tests/StockFlow.Tests/StockFlow.Tests.csproj -c Release --no-build --no-restore
dotnet run --project src/StockFlow.UI/StockFlow.UI.csproj -c Release --no-build
```

Запуск WPF на Windows **ще не перевірено**. Cross-build на macOS не є запуском UI.
Після інтеграції оболонки Саші потрібен окремий Windows demo-прогін.

## Фактичні результати локальних перевірок

- SDK 8.0.427, macOS ARM64: Solution містить рівно п'ять проєктів.
- Restore і Release build успішні, 0 warnings / 0 errors.
- Виконано 22 юніт-тести: 22 passed, 0 failed, 0 skipped. Це тести фабрики,
  конфігурації та DI; доменних тестів StockMovement поки немає.
- У тимчасовій копії UI звернення до DbConnectionFactory дає CS0234.
  Та сама перевірка компілюється з DisableTransitiveProjectReferences=false;
  після видалення проби нормальна збірка з обмеженням знову проходить.
- Чистий snapshot із Git tree (без bin/obj, результатів тестів і appsettings.json)
  успішно пройшов окремі restore, Release build і запуск усіх 22 тестів.
- У цій тимчасовій копії перевірено: відсутній локальний JSON не заважає build;
  синтетичний appsettings.json копіюється до output, коли існує. Після перевірки видалений.
- git diff --cached --check проходить; перевірено всі п'ять наборів прямих посилань,
  ігнорування bin/obj, локальної конфігурації й секретів. Інші розділи architecture.md
  побайтово збігаються з main. Загальний README не змінено.

## Передача команді

- Саші: base першого PR, стандартні App/MainWindow для заміни оболонкою,
  AddStockFlow для DI та наведений контракт Stock/IStockRepository. Потрібен його
  PR або commit із реальним Stock, щоб завершити домен і тести Семена.
- Мар'яну: шлях StockFlow.sln, тести net8.0 без БД, фабрика та конфігураційний ключ.
  Після merge каркаса додати єдиний .github/workflows/ci.yml із windows-latest,
  restore → build → test та job build-and-test. Не дублювати workflow.
- Тані: реєструвати справжні репозиторії/сервіси через AddStockFlow; використовувати
  фабрику і параметризовані SQL; інструкції з цього файла можна звести в її README.
  Продукти/категорії та їхні екрани цими гілками не реалізовані.

## CI і захист main: частково наявний, очікує CI

На час перевірки workflow відсутній у main і доступних гілках; GitHub Actions
повернув total_count=0. Фактичного job build-and-test ще немає, зеленого CI не заявлено.

У репозиторії вже активний ruleset **Main branch protection** (id 22749088),
націлений на default branch main. API підтвердив: PR обов'язковий, 2 approvals,
відхилення застарілих approvals, закриття review threads, заборона видалення та
force push; bypass_actors порожній. Required status checks поки немає.
Класичного branch protection rule немає; створювати його паралельно не потрібно.
Правил адміністратора не змінювали: користувач вимагає окремого підтвердження.

Після першого успішного запуску workflow Мар'яна і окремого підтвердження користувача:

1. В Actions відкрити фактичний run і звірити назву job та результат build-and-test.
2. Settings → Rules → Rulesets → Main branch protection.
3. Зберегти Enforcement status: Active і target default branch; не змінювати
   наявні PR, 2 approvals, review threads, Restrict deletions та Block force pushes.
4. Додати Require status checks to pass → Add checks; вибрати фактично
   зареєстровану перевірку build-and-test від GitHub Actions.
5. Bypass list залишити порожнім; Save changes.

Порядок редагування звірено з
[GitHub Docs](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/managing-rulesets-for-a-repository).
Наявні PR/approvals/заборони уже забезпечені правилами; додавання обов'язкового
CI лишається залежним невиконаним завданням.
