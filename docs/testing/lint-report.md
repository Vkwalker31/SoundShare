# Статический анализ кода (задание 5)

## 1. Инструменты и настройка

| Инструмент | Где настроен | Что проверяет |
|---|---|---|
| .NET-анализаторы (Roslyn, правила CA) | `Directory.Build.props`: `AnalysisLevel=latest-recommended`, `TreatWarningsAsErrors=true` | Надёжность, безопасность, производительность, проектирование |
| Правила стиля кода (IDE) | `.editorconfig` + `EnforceCodeStyleInBuild=true` | Пространства имён, фигурные скобки, `var`, именование, порядок `using` |
| `dotnet format --verify-no-changes` | Командная строка / CI | Форматирование и стиль по `.editorconfig`, включая правила именования, которые сборка не проверяет |
| Строгий профиль `AnalysisLevel=latest-all` | Разовый запуск для отчёта | Все правила CA, включая отключённые по умолчанию |

Рабочий профиль — `latest-recommended` с превращением предупреждений в ошибки: сборка с нарушением не проходит, поэтому в репозитории нарушений рабочего профиля нет. Ослабления правил ограничены по месту: `tests/.editorconfig` (имена тестов с подчёркиванием, `ConfigureAwait`, строковые URL в тестах) и `src/SoundShare.Infrastructure/Persistence` (`ToUpper()` внутри LINQ-запросов, которые переводятся в SQL `UPPER`).

## 2. Найденные нарушения и исправления

### 2.1 `dotnet format`

| Правило | Найдено | Причина | Исправление |
|---|---:|---|---|
| IDE1006 (именование) | 18 | Правило «приватные поля с `_`» распространялось на `const` и `static readonly` (например, `Now`, `SessionFlag`, `UnavailableCodes`), хотя по соглашениям .NET они пишутся в PascalCase | В `.editorconfig` добавлены правила `private_constants_pascal` и `private_static_readonly_pascal`; повторный запуск — 0 нарушений |

### 2.2 Строгий профиль `latest-all` (уникальные предупреждения)

| Правило | До | После | Решение |
|---|---:|---:|---|
| CA1062 Проверять аргументы публичных методов | 25 | 14 | В сервисах Application, `AuditBuffer`, `TokenService`, `AuthSession` добавлены `ArgumentNullException.ThrowIfNull`; оставшиеся — в тестовых проектах |
| CA1032 Стандартные конструкторы исключения | 3 | 0 | `ApiException` клиента получила стандартные конструкторы |
| CA1003 Событие с `EventHandler` | 1 | 0 | `AuthSession.Changed` переведено на `EventHandler` |
| CA1307 Указывать `StringComparison` | 4 | 1 | `PathString.StartsWithSegments(..., OrdinalIgnoreCase)`; оставшийся вызов — внутри запроса EF Core, перегрузка со `StringComparison` не транслируется в SQL |
| CA2007 `ConfigureAwait` | 269 | 269 | Не применяется: в ASP.NET Core нет `SynchronizationContext`, а Blazor WebAssembly должен вернуться в контекст UI |
| CA1812 Класс не создаётся | 41 | 41 | Ложные срабатывания: классы создаются контейнером DI и MVC (сервисы, контроллеры, хабы, тестовые классы) |
| CA1515 Сделать тип `internal` | 27 | 27 | Типы публичны намеренно: контракты, сервисы и тесты используются из других сборок |
| CA2000 Освобождать объекты | 6 | 6 | `HttpClientHandler`/`SocketsHttpHandler` передаются во владение `HttpClient`, который их освобождает |
| CA1054 / CA2234 Строковые URL | 10 | 10 | Относительные пути API в клиенте и тестах; `Uri` здесь не добавляет проверки |
| CA1308 `ToUpperInvariant` вместо `ToLowerInvariant` | 4 | 4 | Нормализация не используется для сравнения — это тестовые данные (имена и email в нижнем регистре) |
| CA5399 Проверка отзыва сертификатов | 3 | 3 | Тестовые клиенты к `http://localhost` и WireMock |
| CA1819 Свойство-массив | 2 | 2 | `byte[] TokenHash` — тип колонки `RAW(32)` для EF Core |
| CA1724 Имя типа совпадает с пространством имён | 2 | 2 | `DependencyInjection` — принятое в .NET имя класса регистрации сервисов |
| CA1008 Нулевое значение перечисления | 2 | 2 | `SystemRole`, `RoomRole` начинаются с 1 — это коды из справочника `ROLES` |

## 3. Итог

- Рабочий профиль: **0 нарушений**, сборка и `dotnet format --verify-no-changes` проходят.
- Строгий профиль: исправлены все предметные нарушения в коде приложения (CA1062, CA1032, CA1003, CA1307); остальные правила задокументированы как неприменимые к архитектуре проекта.

Повторить проверку:

```bash
dotnet build SoundShare.slnx
dotnet format SoundShare.slnx --verify-no-changes
dotnet build SoundShare.slnx --no-incremental -p:AnalysisLevel=latest-all -p:TreatWarningsAsErrors=false
```
