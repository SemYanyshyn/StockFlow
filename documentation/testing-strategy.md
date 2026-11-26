# Стратегія тестування StockFlow

Документ описує, як і що ми тестуємо, як запускати перевірки та де їхні межі. Числа взято з прогону на чистому клоні на коміті `3288910` (20.11.2026), див. [«Результати прогону»](#результати-прогону).

## 1. Мета й обсяг

Тести гарантують бізнес-правила (валідація, права ролей, заборона видаляти пов'язані записи, правила накладних), цілісність даних у PostgreSQL (обмеження схеми, транзакції, конкурентні зміни) і поведінку частини WPF-екранів (команди ViewModel, видимість дій за роллю, обробка помилок без падіння).

Тести **не** гарантують візуальний вигляд екранів (кольори, розташування — це ручна перевірка зі скріншотами в PR) і продуктивність на великих обсягах даних: її ми не вимірювали.

## 2. Рівні тестування

| Рівень | Проєкт | Що перевіряє | Інструмент | Кількість | Де запускається |
|---|---|---|---|---|---|
| Модульні | `tests/StockFlow.Tests` | домен, сервіси, реєстрація DI, конфігурація; без бази | xUnit, ручні фейки | 367 тестів | job `build-and-test`; локально `dotnet test` |
| Перевірки БД | `tests/StockFlow.DatabaseChecks` | репозиторії на справжній PostgreSQL, обмеження схеми, ICU-унікальність, конкурентні зміни | консольна програма, Npgsql | 169 перевірок | jobs `database-checks` і `windows-checks`; локально `scripts/Verify-Isolated.ps1` |
| Перевірки WPF | `tests/StockFlow.WindowsChecks` | ViewModel і XAML реальних екранів: вхід, постачальники, користувачі, права ролей, помилки підключення | консольна програма, WPF Dispatcher | 27 перевірок | job `windows-checks`; локально `scripts/Verify-Isolated.ps1` |
| Перевірки накладних | `tests/StockFlow.MovementChecks` | запити списку накладних на справжній PostgreSQL | консольна програма, Npgsql | 15 перевірок | вручну, `scripts/Verify-Movements-Isolated.sh` (macOS, Linux) |

Перевірки БД і WPF запускаються окремо від `StockFlow.sln` з PostgreSQL із підтримкою ICU. Для WPF потрібна Windows. Обидві програми приймають лише рядок підключення до БД `stockflow_isolated_*` на loopback-адресі.

### Результати прогону

Чистий клон `main`, без `appsettings.json` і локальних налаштувань, .NET SDK 8.0.427, PostgreSQL 17 на Windows 10.

| Набір | Кількість | Результат | Дата | Коміт |
|---|---|---|---|---|
| Модульні (`dotnet test -c Release`) | 367 | усі пройшли | 20.11.2026 | `3288910` |
| `DatabaseChecks` (`Verify-Isolated.ps1`) | 169 | усі пройшли | 20.11.2026 | `3288910` |
| `WindowsChecks` (`Verify-Isolated.ps1`) | 27 | усі пройшли | 20.11.2026 | `3288910` |
| Міграції (`Verify-Isolated.ps1`) | — | пройшли | 20.11.2026 | `3288910` |
| `MovementChecks` | 15 | усі пройшли | 20.11.2026 | `3288910` |
| Покриття рядків: Domain / BusinessLogic | 96,7 % / 99,1 % | гілки BusinessLogic 93,2 % | 20.11.2026 | `3288910` |

Збірка з `-warnaserror` дала 0 попереджень, `dotnet format --verify-no-changes` змін не знайшов. Той самий коміт у CI: усі три jobs зелені. [Прогін на `main`](https://github.com/SemYanyshyn/StockFlow/actions/runs/44693221947)

Покриття рахує лише модульні тести, тому DataAccess має 12,8 %: репозиторії перевіряють `DatabaseChecks` на справжньому PostgreSQL. `Verify-Movements-Isolated.sh` розрахований на macOS і Linux, тож на Windows ті самі кроки (тимчасовий кластер, схема, seed, `dotnet run`) виконано вручну.

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

Для ручного прогону на Linux потрібні .NET SDK 8, Python 3 і бінарні файли PostgreSQL 16 з ICU. Запустіть з кореня репозиторію.

```bash
(
  set -e
  pg_bin=$(pg_config --bindir)
  cluster=$(mktemp -d)
  port=$(python3 -c 'import socket; s = socket.socket(); s.bind(("127.0.0.1", 0)); print(s.getsockname()[1])')
  "$pg_bin/initdb" -D "$cluster/data" --encoding=UTF8 --locale=C --auth=trust -U stockflow_check
  "$pg_bin/pg_ctl" -D "$cluster/data" -l "$cluster/server.log" -o "-h 127.0.0.1 -p $port -k $cluster" -w start
  trap '"$pg_bin/pg_ctl" -D "$cluster/data" -m fast -w stop' EXIT
  export PGHOST=127.0.0.1 PGPORT="$port" PGUSER=stockflow_check PGDATABASE=stockflow_isolated_ci
  "$pg_bin/createdb" "$PGDATABASE"
  "$pg_bin/psql" -X -v ON_ERROR_STOP=1 -f database/01_schema.sql -f database/02_seed.sql
  export STOCKFLOW_TEST_CONNECTION="Host=$PGHOST;Port=$PGPORT;Database=$PGDATABASE;Username=$PGUSER;Timeout=3;Command Timeout=15"
  dotnet run --project tests/StockFlow.DatabaseChecks/StockFlow.DatabaseChecks.csproj -c Release
)
```

Після завершення PostgreSQL зупиниться. Шлях до тимчасового каталогу покаже `initdb`.

## 8. CI

Workflow `CI` запускається для PR у `main` та push у `main` і має три jobs. Обов’язковий для злиття у `main` лише `build-and-test`. [Конфігурація workflow](../.github/workflows/ci.yml) · [Прогін на `main`](https://github.com/SemYanyshyn/StockFlow/actions/runs/44693221947)

`build-and-test` на Windows виконує restore, перевірку форматування (`dotnet format --verify-no-changes`), Release-збірку з `-warnaserror` і xUnit зі збором покриття. Резюме покриття видно на сторінці запуску (**Summary**), HTML-звіт — в артефакті `coverage-html`, який зберігається 14 днів. Порога покриття немає. [Локальний запуск і звіт](../README.md#ci)

Для MA7-5 обрано варіант A. `DatabaseChecks` та її залежності мають цільову платформу `net8.0`. В окремому job `database-checks` на Ubuntu PostgreSQL 16 з ICU працює як сервіс. `psql` застосовує схему та seed, після чого `dotnet run` використовує `STOCKFLOW_TEST_CONNECTION` з loopback-адресою й іменем `stockflow_isolated_ci`.

У `windows-checks` скрипт `Verify-Isolated.ps1` створює тимчасовий кластер і запускає перевірки БД, WPF та міграцій. `WindowsChecks` має цільову платформу `net8.0-windows` і програмно рендерить WPF через STA Dispatcher.

`database-checks` і `windows-checks` стануть обов’язковими після п’яти успішних запусків поспіль на GitHub Actions.

## 9. Баги й регресії

Помилка оформлюється issue з лейблом `bug`: кроки, очікувано, фактично, скріншот. Спершу пишемо тест, який її відтворює й падає, потім виправлення, після якого тест зелений. Назва тесту додається в issue.

## 10. Рефакторинг

Правило: тести зелені до змін і після, нових функцій у PR рефакторингу немає. Приклади — у розділі [«Приклади рефакторингу»](#приклади-рефакторингу) нижче.

## 11. Обмеження й прогалини

- Екрани «Товари» й «Категорії» не мають WPF-перевірок: їх перевіряли вручну в PR (скріншоти) і UI Automation-скриптами, які в репозиторій не входять.
- `database-checks` і `windows-checks` поки не обов’язкові для злиття (розділ 8). `MovementChecks` у CI не запускаються.
- Для залишків є доменні тести (`StockTests`), а `StockRepository` і звіти ще в роботі (задачі SA7-1 – SA7-4).
- Покриття коду показуємо як інформацію, а не як вимогу. Воно не враховує перевірки БД і WPF.

---

## Крайові ситуації

Рядки всіх модулів, що є в `main` на коміті `3288910`. Рядки для залишків і звітів (SA7-1 – SA7-4) додамо після мерджу цих задач.

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
| 17 | Постачальники | Email рівно 100 символів | Приймається | `SupplierTests.EmailAtMaxLengthIsAccepted` | модульний |
| 18 | Постачальники | Email `bad-email`, `name@`, `a@b@c.com`, `hello @example.com` | «Некоректна електронна пошта постачальника.», запис не зберігається | `SupplierServiceTests.InvalidEmailIsRejectedWithoutSaving` | модульний |
| 19 | Постачальники | Телефон і email порожні або з самих пробілів | Зберігаються як `null` | `SupplierServiceTests.BlankOptionalFieldsBecomeNull` | модульний |
| 20 | Постачальники | Видалення постачальника, для якого є рухи | Відмова, запис лишається; постачальника без рухів видаляє | `SupplierServiceTests.DeleteProtectsHistoryAndRemovesUnusedSupplier` | модульний |
| 21 | Склади | Назва `null`, порожня або з самих пробілів | «Назва складу обов'язкова.» | `WarehouseServiceTests.Add_WhenNameIsEmptyOrWhitespace_ThrowsValidationException` | модульний |
| 22 | Користувачі | Логін «NewUser» уже є, створюємо «NewUser» і «newuser» | Точний дубль: «Користувач із таким логіном уже існує.»; інший регістр дозволено | `UserServiceTests.CreateTrimsIdentityHashesPasswordAndPreservesCaseSensitiveUsernameRule` | модульний |
| 23 | Користувачі | Зміна ролі останнього активного Адміністратора на Працівника | «Не можна позбавити систему останнього активного Адміністратора.»; змінити ім'я можна | `UserServiceTests.LastActiveAdminCannotBeDemotedButCanChangeName` | модульний |
| 24 | Користувачі | Адміністратор блокує або видаляє власний обліковий запис | «Не можна заблокувати власний обліковий запис.» / «Не можна видалити власний обліковий запис.» | `UserServiceTests.CannotBlockOrDeleteSelfAndCanBlockUnblockOtherUsers` | модульний |
| 25 | Користувачі | Адміністратора заблокували або понизили в БД, поки його сесія відкрита | «Виконувати цю дію може лише Адміністратор.», жодних змін | `UserServiceTests.StaleAdminSessionCannotReadOrWriteWhenBlockedOrDemotedInDatabase` | модульний |
| 26 | Користувачі | Видалення користувача, який має рухи товарів | «Користувач має зареєстровані рухи товарів. Замість видалення заблокуйте обліковий запис.» | `UserServiceTests.DeleteChecksMovementsAndTranslatesForeignKeyRaceWithoutCommit` | модульний |
| 27 | Вхід | Невідомий логін, неправильний пароль, пошкоджений хеш | Однакове «Невірний логін або пароль»; заблокований із правильним паролем — «Обліковий запис заблоковано. Зверніться до Адміністратора.» | `AuthServiceTests.UnknownWrongAndCorruptCredentialsHaveSameMessageAndBlockedStatusNeedsCorrectPassword` | модульний |
| 28 | Вхід | Пароль « secret » із пробілами | Пароль не обрізається: «secret» не підходить | `AuthServiceTests.HashUsesRandomSaltAndVerifiesExactPassword` | модульний |
| 29 | Накладні | Списання 11 при залишку 10; списання 1 при залишку 0 | «Недостатньо товару на складі», залишок не змінюється | `StockMovementTests.RejectedMovementProvidesOneErrorAndCannotChangeStock` | модульний |
| 30 | Накладні | Надходження без постачальника; списання з постачальником | «Для надходження потрібно обрати постачальника» / «Для списання постачальник не зазначається» | `StockMovementTests.RejectedMovementProvidesOneErrorAndCannotChangeStock` | модульний |
| 31 | Накладні | Надходження, після якого залишок перевищив би `int.MaxValue` | «Кількість перевищує допустиму», залишок не змінюється | `StockMovementTests.RejectedMovementProvidesOneErrorAndCannotChangeStock` | модульний |
| 32 | Накладні | Списання всього залишку: 10 із 10 | Приймається, залишок 0 | `StockMovementTests.WriteOffSupportsPartialAndWholeStock` | модульний |
| 33 | Накладні | Номер рівно 50 символів; 49 символів із пробілами по краях | 50 приймається; з пробілами — ні, бо пробіли теж зберігаються | `StockMovementTests.NumberWithExactlyTheSchemaLimitIsAccepted`, `StockMovementTests.WhitespaceStillCountsTowardTheStoredNumberLength` | модульний |
| 34 | Накладні | Фільтр: дата «з» пізніша за дату «по» | Відмова до запиту в БД | `StockMovementListTests.InvalidRangeRejectedBeforeRepositoryAndSameDayTimesNormalize` | модульний |
| 35 | Накладні | Порожня база, немає збігів за фільтром, помилка БД | «Накладних ще немає.» / «За обраними фільтрами накладних не знайдено.» / «Не вдалося завантажити…» без тексту винятку; повторне завантаження працює | `StockMovementListTests.EmptyDatabaseNoMatchAndFailureAreDifferentStatesAndCanRetry` | модульний |
| 36 | Залишки | Кількість 4, 10, 15 при порозі 10; 0 при порозі 0 | Низький залишок лише 4 із 10; рівно на порозі — не низький | `StockTests.IsBelowThreshold_EvaluatesCorrectly` | модульний |

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
