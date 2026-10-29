# Етап 6 — частина Семена

Факти перевірено 2026-10-30. Робочий репозиторій: SemYanyshyn/StockFlow.
Актуальна база виконаних перевірок: main `723e6563a7265db1e709a450b45b62854bd96d31`.
Під час роботи команда злила #124 (Stock/оболонка), #123 (БД/контекст), #120
(товари/категорії) та #141 (CI). Початковий прототип перенесено на справжні типи;
другого production Stock або тестового дубліката User/контексту немає.

## Статус SE-1–SE-9

| Задача / issue | Фактичний результат | Що лишається |
|---|---|---|
| SE-1 [#126](https://github.com/SemYanyshyn/StockFlow/issues/126) | [Draft PR #125](https://github.com/SemYanyshyn/StockFlow/pull/125): GetAll(int?) до реального IStockRepository, таблиця контракту, сценарій першого надходження, питання Саші; власний CI success | Підтвердити контракт і Serializable зі Сашею. Пропозиція не названа погодженим рішенням |
| SE-2 [#127](https://github.com/SemYanyshyn/StockFlow/issues/127) | [PR #139](https://github.com/SemYanyshyn/StockFlow/pull/139) готовий до review: production StockMovement + 42 тести на справжньому Stock; Windows CI success | Командне review і два approvals; PR не злитий |
| SE-3 [#128](https://github.com/SemYanyshyn/StockFlow/issues/128) | [Draft PR #140](https://github.com/SemYanyshyn/StockFlow/pull/140): production ADO.NET Add/RETURNING, 23 unit-тести; реальний SQL-runner перевірив схему/nullable/timestamp/дублікат | Злиття домену; CI для власного head після перенесення base у main |
| SE-4 [#129](https://github.com/SemYanyshyn/StockFlow/issues/129) | [Draft PR #146](https://github.com/SemYanyshyn/StockFlow/pull/146): production сервіс/транзакційна фабрика, 21 тест сервісу + 2 DI; реальні commit/rollback перевірки з тестовим SQL adapter | Реалізація StockRepository Саші, погодження стратегії, активація модуля та Windows запуск |
| SE-5 [#130](https://github.com/SemYanyshyn/StockFlow/issues/130) | **Виконано й перевірено** після окремого підтвердження користувача: до ruleset22749088 додано фактичний build-and-test від GitHub Actions; усі попередні правила збережені | Передано лише підготовлений текст повідомлення команді, без самовільного надсилання |
| SE-6 [#131](https://github.com/SemYanyshyn/StockFlow/issues/131) | [COMMENT review PR #124](https://github.com/SemYanyshyn/StockFlow/pull/124#pullrequestreview-5705948049): 2 inline-коментарі App.xaml.cs:18 / IStockRepository.cs:9, автор перевірений як loktiska | Переглянути SQL/UPSERT, коли з'явиться справжній StockRepository. Approve не ставився |
| SE-7 [#132](https://github.com/SemYanyshyn/StockFlow/issues/132) | [PR #135](https://github.com/SemYanyshyn/StockFlow/pull/135) готовий до review: 3 SVG + 3 PNG у чинних шляхах, експорти візуально перевірені, CI success | Два approvals; PR не злитий. Це макети, а не скриншоти WPF |
| SE-8 [#133](https://github.com/SemYanyshyn/StockFlow/issues/133) | Не оголошено реалізованим: критерії/розбіжність #30 і повідомлення Тані підготовлені | StockRepository, активація сервісу, узгоджена sequence-міграція номера, форма/список/навігація та справжні Windows-скриншоти |
| SE-9 [#134](https://github.com/SemYanyshyn/StockFlow/issues/134) | 2 реальні двосеансові SQL-проби Serializable пройшли з доведеним перекриттям | Повторити через production StockRepository/сервіс та Windows UI після інтеграції. SQL-проба не є завершенням SE-9 |

## Реалізація і життєвий цикл

- `src/StockFlow.Domain/StockMovement.cs`: усі UML/ER поля, bool Validate(Stock),
  void Apply(Stock), єдине джерело GetValidationError й українських повідомлень.
  Перевірки номера, типу, кількості, дати, FK, постачальника, пари товар/склад,
  достатності й переповнення; Apply спочатку обчислює результат, тоді присвоює.
- `src/StockFlow.DataAccess/{IStockMovementRepository,StockMovementRepository,DuplicateNumberException}.cs`:
  параметризований INSERT RETURNING id; receipt/write_off явно; DBNull типізований;
  TIMESTAMP WITHOUT TIME ZONE через DateTime2. Лише 23505 + stock_movements_number_key
  стає DuplicateNumberException; інші constraint/state не видаються за дублікат номера.
- `src/StockFlow.BusinessLogic/StockMovementService.cs`: лише Employee, UserId із
  ICurrentUserContext, один trusted snapshot користувача й одна транзакція.
  Apply → Add → Save → Commit; rollback/dispose на помилках. Повідомлення не
  містять provider/SQL/connection string. Оброблено 40001/40P01 та connection states.
- `src/StockFlow.DataAccess/{IStockTransactionFactory,NpgsqlStockTransactionFactory}.cs`:
  новий connection на операцію, Serializable, власник session виконує commit/rollback
  та Dispose. Репозиторій не відкриває другий connection і не закриває переданий.
- `AddStockFlow` зберігає реєстрації товарів/категорій/користувача команди та додає
  movement repository/transaction factory. `AddStockMovementServices` активує сервіс
  **після** реєстрації реального IStockRepository. Заглушки не реєструються.
  Наявні інфраструктурні тести без БД збережено. Тест DI явно доводить помилку
  відсутньої командної залежності замість приховування її фіктивною реалізацією.
- `src/StockFlow.UI/App.xaml.cs`: відновлено Load/AddStockFlow, ValidateOnBuild і
  ValidateScopes та Dispose, збережено всі реєстрації оболонки/навігації Саші.
  Це виправлення інтеграції, підняте у review124; запуск WPF ще не перевірено.
- `tests/StockFlow.Tests/{StockMovementTests.cs,Repository/,Service/}`: звичайні unit-тести.
  Фейки існують лише в tests. Справжні Stock/User/context беруться з Domain/BusinessLogic.

Перед активацією форми Саші потрібно додати реальний IStockRepository, потім викликати
AddStockMovementServices. Контракт GetAll(int?) пропонується у PR125; GetForUpdate і
Save вже існують у main. Save має UPSERT лише quantity, зберігаючи min_threshold;
новий рядок має threshold0. Serializable — перевірена пропозиція, ще не командне
погодження. SELECT FOR UPDATE не блокує відсутній рядок; один UPSERT не усуває lost update.

## Команди й фактичні результати

На цьому Mac використано SDK8.0.427 у /private/tmp/stockflow-dotnet8; системний
SDK10 не замінено. Для постійної роботи потрібне постійне встановлення SDK8.

```sh
export PATH="/private/tmp/stockflow-dotnet8:$PATH"
dotnet sln StockFlow.sln list
dotnet restore StockFlow.sln
dotnet build StockFlow.sln -c Release --no-restore
dotnet test tests/StockFlow.Tests/StockFlow.Tests.csproj -c Release --no-build --no-restore
```

Solution зберігає п'ять проєктів. Restore/Release build успішні: 0 warnings/errors.
**234 passed, 0 failed, 0 skipped** = 146 командних +42 доменних +23 репозиторію
+21 сервісу +2 DI. Тести не потребують БД, локального JSON, WPF або мережі після restore.
EnableWindowsTargeting увімкнено тільки для UI; інші чотири проєкти лишилися net8.0.

Компіляційна проба в окремій копії: UI→DbConnectionFactory відхилено CS0234;
та сама проба без DisableTransitiveProjectReferences компілюється, після видалення
перевірочного файла нормальний build знову проходить. У production проби немає.
Окрема чиста копія Git snapshot без appsettings.json також пройшла restore,
Release build і всі234тести; умовне копіювання синтетичного JSON перевірено лише
в тимчасовій копії. Обидва створені нами PostgreSQL сервери після перевірок зупинені.
Перевірено всі прямі ProjectReference, .gitignore й git diff --check; UI не має
прямого доступу до DataAccess. Особистий JSON/секрети/bin/obj не додаються в Git.

## Реальний PostgreSQL і конкурентність

Ручний runner **поза Solution/звичайним CI**:

```sh
python3 tests/StockFlow.IntegrationChecks/run-scratch-checks.py \
  --pg-bin /Applications/Postgres.app/Contents/Versions/18/bin \
  --dotnet /private/tmp/stockflow-dotnet8/dotnet
```

Команда реально виконана на PostgreSQL **18.4**, не 15. Вона створює новий кластер
у короткому /private/tmp, дві одноразові test-БД, застосовує схему/seed зі злитого
commit5e1a910, виконує перевірки й зупиняє тільки власний сервер у finally.
Робочі бази/конфігурація користувача не використовуються. Дані й журнали збережено
у /private/tmp/stockflow-se6-verify-g5gkk3ua. Перше тестування runner виявило довгий
macOS TMPDIR/socket; це виправлено, повторний повний запуск успішний.

**7 груп SQL-перевірок пройшли:** INSERT/RETURNING + усі параметри/null/TIMESTAMP;
справжній duplicate constraint; commit руху+кількості; реальний rollback після
інжектованої SQL CHECK-помилки Save; відмова списання понад залишок; безпечний
дублікат; відсутність залишених operation connections. Тут production movement
класи, але **Stock SQL adapter лише тестовий**, бо production StockRepository відсутній.
Це реальна перевірка PostgreSQL-транзакції, а не лише виклику Rollback на фейку.

Остання відтворювана raw SQL-проба з двома сесіями:

| Сценарій | Початок / перекриття | Фактичний кінець |
|---|---|---|
| WriteOff8+8 | stock10/threshold10; A тримає row lock, у B старий snapshot і підтверджене очікування Lock | A commit, B40001; quantity2, threshold10, 1 рух на8 |
| Перші Receipt5+7 | рядка немає; обидва ABSENCE_READ-бар'єри пройдені до INSERT | B commit, A40001; quantity7, threshold0, 1 рух на7 |

Переможець Receipt може змінитися при повторі; runner допускає рівно одного
успішного учасника, відповідну йому кількість і 40001 іншого. Автоматичного retry немає.
Продуктовий сервіс перетворює конфлікт на «Спробуйте ще раз» (перевірено unit-тестами).
Ця SQL-проба не доводить поведінку ще неіснуючого production StockRepository/Windows UI.
Машиночитні факти, SQL і журнали: [evidence/stage6](evidence/stage6).

## CI та захист

- [main CI](https://github.com/SemYanyshyn/StockFlow/actions/runs/40968466801) — success.
- [домен139 Windows CI](https://github.com/SemYanyshyn/StockFlow/actions/runs/40968882593) — success,188 tests.
- [контракт125 CI](https://github.com/SemYanyshyn/StockFlow/actions/runs/40969925494) — success,146 tests.
- [макети135 CI](https://github.com/SemYanyshyn/StockFlow/actions/runs/40969916067) — success,146 tests.
- Repo140/service146 та поточний перевірочний PR мають feature-base: чинний
  workflow запускається лише для PR→main і push/main. Їхній власний Windows CI
  **не запускався**; успіх main не видається за перевірку цих head.

Після явного підтвердження користувача змінено рівно одне правило:
required build-and-test, GitHub Actions integration15368. Перед save звірено
свіжий ruleset із підготовленим before; після save перевірено ruleset та effective
rules main. PR,2approvals,stale approvals,review threads,block deletion/force push,
default-branch target і bypass[] збережені; strict up-to-date requirement не додавали.
[Чинний ruleset](https://github.com/SemYanyshyn/StockFlow/rules/22749088).

## Гілки, review і наступні дії

| Гілка | PR / base | Перевірений head |
|---|---|---|
| docs/stock-contract | #125 → main, Draft | 2ba704e |
| feature/invoices-domain-rules | #139 → main, ready for review | 753cb8c |
| feature/invoices-repository | #140 → domain branch, Draft | 0fbf180 |
| feature/invoices-service | #146 → repository branch, Draft | 3049bc7 |
| docs/wireframes-movements | #135 → main, ready for review | 81e7143 |
| test/invoices-verification | поточний окремий PR → service branch, Draft | див. історію гілки |

Залежні PR не відкривали незалежно в main із повторними файлами. Main оновлювався
звичайними merge лише **в feature-гілки**; PR не зливалися, force push не було.
Після злиття батька звірити diff і змінити base наступного PR на main. Якщо батька
squash/rebase-merged, створити нову гілку від main і cherry-pick лише власні коміти,
замість force push або повторного включення всієї історії каркаса.

За ротацією код Семена переглядає Мар'ян, але чинні правила все одно вимагають
двох approvals. GitHub Projects не змінено: gh повернув відсутній scope read:project.
Вручну додати/перевести готові картки #127 і #132 у Ready for Review на командній дошці.
Залежні пункти не позначати Done. Issues #30/#32 лишаються відкритими: форми ще немає.

## Хвилина демо

1. 0–15с: `dotnet test tests/StockFlow.Tests/StockFlow.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~StockMovementServiceTests`.
2. 15–30с: показати InsufficientStockDoesNotAddOrSaveAndReleasesTransaction та
   SaveFailureRollsBackStagedFakeMovementAndStock: це **фейки**, які перевіряють оркестрацію.
3. 30–50с: відкрити integration-output.txt: production сервіс із тестовим SQL adapter
   реально відкотив INSERT після SQL CHECK-помилки Save. Не видавати adapter за StockRepository Саші.
4. 50–60с: показати concurrency-results.json:1 успіх/40001 і фінальні2 та7; назвати
   незавершеними live StockRepository, номерогенерацію, форму/список та Windows-прогін.

Windows після інтеграції модуля і двох approvals (з кореня, SDK8):

```powershell
dotnet restore StockFlow.sln
dotnet build StockFlow.sln -c Release --no-restore
dotnet test tests/StockFlow.Tests/StockFlow.Tests.csproj -c Release --no-build --no-restore
dotnet run --project src/StockFlow.UI/StockFlow.UI.csproj -c Release --no-build
```

WPF запуск і справжні скриншоти ще **не перевірено**. Cross-build не заміняє Windows.
Контракти/підготовлені повідомлення та цільове виправлення #30:
[stage6-team-messages.md](stage6-team-messages.md). Повідомлення не надсилалися.
