# Стратегія тестування StockFlow

Документ описує, як і що ми тестуємо, як запускати перевірки та де їхні межі. Числа зафіксовано на коміті `11d2aa8` (17.11.2026) і будуть оновлені після прогону на чистому клоні (TA7-5).

## 1. Мета й обсяг

Тести гарантують бізнес-правила (валідація, права ролей, заборона видаляти пов'язані записи, правила накладних), цілісність даних у PostgreSQL (обмеження схеми, транзакції, конкурентні зміни) і поведінку частини WPF-екранів (команди ViewModel, видимість дій за роллю, обробка помилок без падіння).

Тести **не** гарантують візуальний вигляд екранів (кольори, розташування — це ручна перевірка зі скріншотами в PR) і продуктивність на великих обсягах даних: її ми не вимірювали.

## 2. Рівні тестування

| Рівень | Проєкт | Що перевіряє | Інструмент | Кількість | Де запускається |
|---|---|---|---|---|---|
| Модульні | `tests/StockFlow.Tests` | домен, сервіси, реєстрація DI, конфігурація; без бази | xUnit, ручні фейки | 360 тестів | CI на кожен PR і push у `main`; локально `dotnet test` |
| Перевірки БД | `tests/StockFlow.DatabaseChecks` | репозиторії на справжній PostgreSQL, обмеження схеми, ICU-унікальність, конкурентні зміни, міграції | консольна програма, Npgsql | 32 перевірки | вручну, `scripts/Verify-Isolated.ps1` |
| Перевірки WPF | `tests/StockFlow.WindowsChecks` | ViewModel і XAML реальних екранів: вхід, постачальники, користувачі, права ролей, помилки підключення | консольна програма, WPF Dispatcher | 27 перевірок | вручну, той самий скрипт (Windows) |
| Перевірки накладних | `tests/StockFlow.MovementChecks` | запити списку накладних на справжній PostgreSQL | консольна програма, Npgsql | 15 перевірок | вручну, `scripts/Verify-Movements-Isolated.sh` |

Перевірки БД і WPF навмисно не входять у `StockFlow.sln` і CI: їм потрібні PostgreSQL з ICU і Windows. Кожна працює лише з тимчасовою базою `stockflow_isolated_*` на loopback-адресі й відмовляється запускатися з іншим рядком підключення.

## 3. Що де тестуємо

| Правило | Рівень |
|---|---|
| Валідація полів, межі довжини, обчислення (`Validate`, `Apply`, `MatchesSearch`, `Filter`) | модульні |
| Права ролей у сервісах (Адмін / Працівник) | модульні |
| Унікальність, `ON DELETE RESTRICT`, `CHECK`, транзакції, блокування рядка | перевірки БД |
| Видимість кнопок за роллю, фільтрація й повідомлення на екрані | перевірки WPF |
| Транзакційна логіка сервісу | модульні з фейками плюс перевірки БД |

Якщо правило дублюється в коді й у базі (як унікальність назви категорії), тестуємо обидва місця: сервіс — модульним тестом, базу — перевіркою БД.

## 4. Дані й фейки

- Репозиторії підмінюються ручними фейками всередині тестових класів (наприклад, `FakeCategoryRepository` у `CategoryServiceTests`). Бібліотеку імітацій не використовуємо: фейк рахує виклики (`AddCalls`, `DeleteCalls`) і вміє кидати потрібний виняток (`ThrowDuplicateOnAdd`).
- Перевірки БД і WPF ніколи не працюють із робочою базою. Скрипт створює окремий кластер PostgreSQL на вільному порту, бази `stockflow_isolated_*` з `database/01_schema.sql` і `database/02_seed.sql`, а наприкінці зупиняє кластер.
- Тестові дані — це `02_seed.sql` плюс записи, які перевірка створює сама з випадковим суфіксом.

## 5. Угоди

- **Назви нових тестів**: `Метод_Сценарій_Результат`, наприклад `Validate_WriteOffAboveStock_ReturnsError`. Наявні тести не перейменовуємо, щоб не створювати шум у diff.
- **Структура**: Arrange-Act-Assert; один тест перевіряє одну поведінку.
- **Тест відмови в кожному PR**: нова логіка не мерджиться без тесту, і хоча б один тест перевіряє відмову.
- **Відмова перевіряється повністю**: текст повідомлення дослівно (через константу сервісу, наприклад `CategoryService.DuplicateNameMessage`) і те, що репозиторій не викликався (`Assert.Equal(0, repository.AddCalls)`).

## 6. Крайові ситуації

Каталог із реальними назвами тестів — у розділі [«Крайові ситуації»](#крайові-ситуації) нижче.

## 7. Запуск

```powershell
dotnet restore
dotnet build StockFlow.sln -c Release
dotnet test StockFlow.sln -c Release

# список усіх тестів (перевірити, що тест із каталогу існує)
dotnet test StockFlow.sln --list-tests

# один клас або тест за частиною назви
dotnet test StockFlow.sln --filter "FullyQualifiedName~CategoryServiceTests"

# перевірки БД і WPF у тимчасовому кластері (шлях до bin вашої версії PostgreSQL)
.\scripts\Verify-Isolated.ps1 -PostgreSqlBin 'C:\Program Files\PostgreSQL\17\bin'
```

`-SkipWindowsChecks` пропускає WPF-перевірки. Журнали й рендери скрипт кладе в `TestResults/`, Git їх ігнорує.

## 8. CI

Workflow [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) на кожен PR і push у `main` виконує restore, Release-збірку й модульні тести. Перевірка `build-and-test` обов'язкова для `main`: без неї PR не змерджити.

Перевірки БД і WPF у CI не запускаються (див. розділ 2). Чи можна їх туди перенести, з'ясовує задача MA7-5; додаткові перевірки якості (попередження як помилки, форматування, покриття) додає MA7-2. Розділ оновимо за їхніми результатами.

## 9. Баги й регресії

Помилка оформлюється issue з лейблом `bug`: кроки, очікувано, фактично, скріншот. Спершу пишемо тест, який її відтворює й падає, потім виправлення, після якого тест зелений. Назва тесту додається в issue.

## 10. Рефакторинг

Правило: тести зелені до змін і після, нових функцій у PR рефакторингу немає. Приклади — у розділі [«Приклади рефакторингу»](#приклади-рефакторингу) нижче.

## 11. Обмеження й прогалини

- Екрани «Товари» й «Категорії» не мають WPF-перевірок: їх перевіряли вручну в PR (скріншоти) і UI Automation-скриптами, які в репозиторій не входять.
- Перевірки БД і WPF не запускаються автоматично (розділ 8).
- Модулі залишків і звітів на момент написання ще без бізнес-логіки, тож і без тестів (задачі SA7-1 – SA7-4).
- Покриття коду показуємо як інформацію, а не як вимогу (після MA7-2).

---

## Крайові ситуації

Рядки модулів «Товари» й «Категорії». Рядки інших модулів додадуть автори (SE7-7, SA7-7, MA7-8).

| № | Модуль | Ситуація | Очікувана поведінка | Тест | Рівень |
|---|---|---|---|---|---|
| 1 | Категорії | Назва рівно 100 символів | Приймається | `CategoryTests.NameAtMaxLengthIsAccepted` | модульний |
| 2 | Категорії | Назва 101 символ | «Назва категорії не може бути довшою за 100 символів.», запис не зберігається | `CategoryServiceTests.TooLongNameIsRejectedWithoutSaving` | модульний |
| 3 | Категорії | Назва `null`, порожня або з самих пробілів | «Назва категорії обов'язкова.», запис не зберігається | `CategoryServiceTests.MissingNameIsRejectedWithoutSaving` | модульний |
| 4 | Категорії | Дубль іншим регістром: «Напої» / «НАПОЇ» / «  напої », «Ґудзики» / «ґудзики» | «Категорія з такою назвою вже існує.», запис не зберігається | `CategoryServiceTests.DuplicateNameIsRejectedWithoutSaving` | модульний |
| 5 | Категорії | Той самий дубль в обхід сервісу, база з локаллю C | Унікальний ICU-індекс відхиляє, репозиторій кидає `DuplicateNameException` | `DatabaseChecks`: «ICU unique index rejects Напої/напої in locale C» | БД |
| 6 | Категорії | «ї» і «і» в назві | Різні назви, не дублікат | `DatabaseChecks`: «ICU keeps Ukrainian accents distinct» | БД |
| 7 | Категорії | Перейменування «напої» → «Напої» (лише регістр власної назви) | Дозволено | `CategoryServiceTests.CategoryCanChangeCaseOfItsOwnName` | модульний |
| 8 | Категорії | Видалення категорії, у якій 1, 3, 5, 11, 12, 21, 22 товари | Відмова з правильним відмінком: «Категорію не можна видалити: у ній 21 товар.», «… 22 товари.», «… 11 товарів.» | `CategoryServiceTests.CategoryWithProductsIsNotDeletedAndCountIsShown` | модульний |
| 9 | Категорії | Пошук із `null`, порожнім рядком або пробілами | Повертає всі категорії | `CategoryTests.EmptyQueryMatchesEveryCategory` | модульний |
| 10 | Товари | Пошук «МОЛОКО», «мол», «  мол  » | Знаходить «Молоко» | `ProductTests.MatchingQueryIsFound` | модульний |
| 11 | Товари | Пошук «ґудзики ЇЖАЧОК» | Знаходить «Ґудзики їжачок» | `ProductTests.SearchIgnoresCaseOfUkrainianLetters` | модульний |
| 12 | Товари | Фільтр: категорія + пошук без жодного збігу | Порожній список, без помилки | `ProductServiceTests.NoMatchesReturnsEmptyList` | модульний |
| 13 | Товари | Невідома одиниця виміру `(UnitOfMeasure)99` | «Оберіть одиницю виміру.», запис не зберігається | `ProductServiceTests.MissingOrUnknownUnitIsRejectedWithoutSaving` | модульний |
| 14 | Товари | Зміна одиниці виміру товару, для якого є залишки або рухи | «Одиницю виміру не можна змінити: для товару вже є залишки або рухи.», оновлення не викликається | `ProductServiceTests.UnitCannotChangeWhenProductHasStockOrMovements` | модульний |
| 15 | Товари | Видалення товару із залишками або рухами | «Товар не можна видалити: для нього є залишки або рухи товару.», запис лишається | `ProductServiceTests.ProductWithStockOrMovementsIsNotDeleted` | модульний |
| 16 | Товари | Два товари з однаковою назвою | Дозволено (на відміну від категорій) | `ProductServiceTests.DuplicateProductNamesAreAllowed` | модульний |

---

## Приклади рефакторингу

### 1. Один `ValidationException` і одна реєстрація DI (#177)

**Проблема.** Гілка складів принесла власні копії вже наявних класів: `StockFlow.BusinessLogic.ValidationException` поруч зі спільним `StockFlow.BusinessLogic.Products.ValidationException`, другий метод `DependencyInjection.AddStockFlow` поруч із `ServiceCollectionExtensions.AddStockFlow` і інтерфейс `IDbConnectionFactory`, який ніхто не реалізовував. Два однойменні винятки — це два різні типи: `catch (ValidationException)` ловить лише той, чий простір імен підключено. У #177 через це падали 12 тестів постачальників, а склади не реєструвалися в DI.

**До** (видалений файл `src/StockFlow.BusinessLogic/ValidationException.cs`):

```csharp
namespace StockFlow.BusinessLogic;

public class ValidationException : Exception
{
    public ValidationException() { }
    public ValidationException(string message) : base(message) { }
    public ValidationException(string message, Exception innerException)
        : base(message, innerException) { }
}
```

**Після** (`WarehouseService` використовує спільний виняток, склади реєструються там само, де й інші модулі):

```csharp
using StockFlow.BusinessLogic.Products;
...
services.AddSingleton<IWarehouseRepository, WarehouseRepository>();
services.AddSingleton<WarehouseService>();
```

**Чому краще.** Один тип помилки валідації для всіх сервісів: ViewModel ловить його однаково на кожному екрані. Одна точка реєстрації DI замість двох методів з однаковою назвою. Мертвий інтерфейс прибрано.

**Як збережено поведінку.** Коміт `84d5395`. Тести постачальників і складів знову зелені; додано `WarehousesRegistrationTests`, який перевіряє, що склади отримуються з DI. [CI #177 — success](https://github.com/SemYanyshyn/StockFlow/actions/runs/41026339236/job/123965231991).

### 2. Спільна валідація Add і Update у товарах (#174)

**Проблема.** У #150 вся валідація товару жила всередині `ProductService.Add`. Для редагування в #174 її довелося б скопіювати в `Update`, і тоді будь-яка зміна правил вимагала б правок у двох місцях.

**До** (#150, `ProductService.Add`):

```csharp
var trimmedName = name?.Trim() ?? string.Empty;
if (trimmedName.Length == 0)
    throw new ValidationException(NameRequiredMessage);
if (trimmedName.Length > Product.MaxNameLength)
    throw new ValidationException(NameTooLongMessage);
if (categoryId is not > 0)
    throw new ValidationException(CategoryRequiredMessage);
if (unit is null || !Enum.IsDefined(unit.Value))
    throw new ValidationException(UnitRequiredMessage);
var product = new Product { Name = trimmedName, CategoryId = categoryId.Value, Unit = unit.Value };
...
product.Id = _repository.Add(product);
```

**Після** (#174): перевірки винесено в `CreateValidProduct`, і обидва методи його викликають:

```csharp
public Product Add(string? name, int? categoryId, UnitOfMeasure? unit)
{
    ...
    var product = CreateValidProduct(0, name, categoryId, unit);
    product.Id = _repository.Add(product);
    return product;
}

public Product Update(int id, string? name, int? categoryId, UnitOfMeasure? unit)
{
    ...
    var product = CreateValidProduct(id, name, categoryId, unit);
    ...
}
```

**Чому краще.** Правила валідації товару описані в одному місці, тож Add і Update не можуть розійтися. Той самий підхід застосовано до категорій (`CreateValidCategory`, #173).

**Як збережено поведінку.** Тести додавання з #150 (`MissingNameIsRejectedWithoutSaving`, `TooLongNameIsRejectedWithoutSaving`, `MissingCategoryIsRejectedWithoutSaving`, `MissingOrUnknownUnitIsRejectedWithoutSaving`) не змінювалися й лишилися зеленими; новий `EditUsesTheSameValidationAsAdd` перевіряє, що редагування дає ті самі повідомлення. [CI #174 — success](https://github.com/SemYanyshyn/StockFlow/actions/runs/41023112395/job/123954832617).

### 3. Дублікати категорій без `LOWER()` (#120, #192)

**Проблема.** План етапу 5 пропонував шукати дублікат назви в SQL через `LOWER(name) = LOWER(@name)`. Перевірка показала, що для кирилиці це не працює: у локалі C `LOWER()` змінює лише латиницю. До коду цей варіант не потрапив, тож це радше виправлене проєктне рішення, ніж переписаний код.

Перевірка на PostgreSQL 17:

```sql
SELECT lower('НАПОЇ' COLLATE "C") = 'напої';           -- false, повертає «НАПОЇ»
SELECT lower('НАПОЇ' COLLATE "uk-UA-x-icu") = 'напої'; -- true
```

**До** (запропоновано, у коді не було): порівняння через `LOWER()` у запиті до бази.

**Після.** Сервіс порівнює назви в C# без урахування регістру (#120):

```csharp
if (_repository.GetAll().Any(existing => existing.Id != id && IsSameName(existing.Name, category.Name)))
    throw new ValidationException(DuplicateNameMessage);

private static bool IsSameName(string existing, string candidate) =>
    string.Equals(existing.Trim(), candidate, StringComparison.OrdinalIgnoreCase);
```

Згодом (#192) база отримала другу лінію захисту — унікальний індекс з ICU-правилом порівняння, незалежний від локалі бази (`database/01_schema.sql`, міграція `04_categories_case_insensitive.sql`):

```sql
CREATE UNIQUE INDEX categories_name_case_insensitive_key
    ON categories (name COLLATE stockflow_case_insensitive);
```

Порушення індексу (`23505`) репозиторій перетворює на `DuplicateNameException`, а сервіс — на те саме повідомлення «Категорія з такою назвою вже існує.».

**Чому краще.** Результат не залежить від локалі бази. Сервіс дає зрозуміле повідомлення ще до запиту на запис, а індекс захищає від двох одночасних додавань.

**Як збережено поведінку.** `CategoryServiceTests.DuplicateNameIsRejectedWithoutSaving` (зокрема «Ґудзики» / «ґудзики», «Їжа для тварин» / «їЖА ДЛЯ ТВАРИН»), `DuplicateDetectedByDatabaseIsReportedAsValidationError` і перевірки БД з рядків 5–6 каталогу. [CI #192 — success](https://github.com/SemYanyshyn/StockFlow/actions/runs/43314390640/job/131518466492).

Інші приклади (SE7-6, SA7-6, MA7-8) додадуть автори.
