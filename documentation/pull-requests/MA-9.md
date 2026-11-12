# MA-9: справжній вхід

Base: feature/suppliers-edit-delete (після її merge — main). Branch: feature/auth-login.
Related issue: #23 (US-10).

Додано UserRepository.GetByUsername, PBKDF2-SHA256 v1 зі випадковою сіллю та FixedTimeEquals, AuthService й авторизований контекст без хешу пароля. Неправильний пароль та невідомий логін мають однакову помилку; блокування перевіряється після пароля. LoginView обробляє помилки БД, закриття завершує програму. Головне вікно й ViewModels із правами створюються після входу. TemporaryUserContext залишено тільки для сумісності тестів, у DI його немає.

Seed містить справжні демонстраційні хеші. 03_update_demo_passwords.sql оновлює лише початкові placeholder-хеші, без видалення даних. README містить пароль і порядок оновлення.

Перевірено: Release build, 305 unit-тестів без PostgreSQL. Реальні UI/DB перевірки — у спільному звіті MA-8–MA-10.

```powershell
dotnet restore StockFlow.sln
dotnet build StockFlow.sln -c Release --no-restore
dotnet test tests/StockFlow.Tests/StockFlow.Tests.csproj -c Release --no-build
```
