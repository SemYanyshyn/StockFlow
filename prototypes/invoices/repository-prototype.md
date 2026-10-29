# SE-3: тестовий прототип репозиторію

Кандидат компілюється тільки в окремому prototype test project; production DataAccess і Solution не змінені. Залежить від доменного прототипу PR #139 та майбутнього спільного Stock.

Схему звірено з PR #123: stock_movements_number_key, TIMESTAMP без timezone, nullable supplier_id/note. Add використовує тільки передану транзакцію/підключення; створює і звільняє власну команду. Receipt/write_off зіставлені явно; DateTime2 відповідає TIMESTAMP, значення збережене як wall-clock з Kind.Unspecified.

23 unit-тести з recording-фейками перевіряють параметри, типи/null, RETURNING id, життєвий цикл ресурсів і класифікацію SQLSTATE за точним constraint. Вони не є доказом виконання SQL або атомарності PostgreSQL. Реальну SQL-перевірку root виконує окремо в scratch-кластері; результат буде у documentation/stage6-semen.md.

```sh
export PATH="/private/tmp/stockflow-dotnet8:$PATH"
dotnet test prototypes/invoices/Tests/StockFlow.Invoices.Prototype.Tests.csproj -c Release --filter FullyQualifiedName~StockMovementRepositoryTests
```

Після появи реального Stock і домену: перенести три кандидатні DataAccess-файли до src/StockFlow.DataAccess, перенести тести до основного test project, прибрати test-only Stock та звірити фактичний constraint зі злитою схемою. Не переносити fixture до production.
