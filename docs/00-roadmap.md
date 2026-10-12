# SoundShare — Дорожная карта и матрица соответствия критериям

> Проект одновременно закрывает три набора требований:
> **(A)** лабораторные работы по МДиСУБД (8 этапов), **(B)** задания по требованиям и тестированию ПО,
> **(C)** темы собеседования на стажировку C#/.NET (Modsen).
> Каждый критерий здесь привязан к артефакту в репозитории. Пункт закрыт, когда артефакт есть и проверен.

Статусы: ✅ готово · 🟡 в работе · ⬜ не начато

---

## 1. Этапы проекта = этапы курса МДиСУБД (A)

| Этап | Требование курса | Артефакты SoundShare | Разработка в этом этапе | Статус |
|---|---|---|---|:-:|
| 1 | ТЗ: предметная область, сущности, функции (авторизация, управление пользователями с группами Admin/User, журналирование) | `docs/01-technical-specification.md` | — | ✅ |
| 2 | ER-схема, описание сущностей и связей | `docs/02-data-model.md` (+ диаграмма Crow's Foot в отчёт) | — | ✅ |
| 3 | Полный набор SQL по созданию схемы | `db/bootstrap/`, `db/migrations/V001…V005`, `db/seed/`, `db/tests/constraints_test.sql` | Docker-окружение, DbUp-раннер | 🟡 |
| 4 | DML-запросы (INSERT/UPDATE/DELETE) с описанием | `db/queries/04-dml.sql`, `docs/04-dml.md` | Domain-слой, Infrastructure (EF Core mapping, репозитории, Unit of Work) | ⬜ |
| 5 | SELECT-запросы с описанием | `db/queries/05-select.sql`, `docs/05-select.md` (очередь с `ROW_NUMBER`, рейтинги `RANK/DENSE_RANK`, статистика) | Application-слой: CQRS-запросы, DTO, AutoMapper | ⬜ |
| 6 | Запросы журналирования, интерфейсы приложения + проволочные макеты | `db/queries/06-audit.sql`, `docs/06-audit-and-ui.md`, `docs/wireframes/*.png` | WebAPI, JWT, Audit Middleware, `IHubFilter`, SignalR Hub, клиент | ⬜ |
| 7 | Приложение, тестовые данные, обоснование индексов | `db/seed/volume/`, `db/explain/`, `docs/07-index-report.md` | Генератор данных, `EXPLAIN PLAN` до/после, тесты и покрытие (B2) | ⬜ |
| 8 | Защита, руководство пользователя | `README.md`, `docs/08-user-manual.md`, демо-сценарий | Docker Compose всей системы, CI | ⬜ |

Связь с разработкой: SQL-артефакты этапа (A) пишутся **первыми**, затем тот же сценарий реализуется в коде. Например, на этапе 4 сначала `MERGE` голоса на чистом SQL, потом репозиторий, который его выполняет.

---

## 2. Требования и тестирование ПО (B)

### 2.1 Задание 1 — требования

| Критерий | Артефакт | Что сделать | Этап | Статус |
|---|---|---|:-:|:-:|
| README: название, назначение, пользователи | `README.md` | Каркас сейчас, дополнять по ходу | 3→8 | ⬜ |
| README: стек технологий | `README.md` | — | 3 | ⬜ |
| README: схема компонентов и связей | `README.md` (Mermaid `flowchart`) | Клиент ↔ WebAPI ↔ SignalR ↔ Oracle ↔ YouTube API | 4 | ⬜ |
| README: перечень модулей и их ответственность | `README.md` | Domain / Application / Infrastructure / WebAPI / tests | 4 | ⬜ |
| README: точки интеграции | `README.md` | Oracle, YouTube Data API, SignalR, файловая система (логи) | 6 | ⬜ |
| README: сборка и запуск | `README.md` | `docker compose up`, миграции, seed | 3→8 | ⬜ |
| Бизнес-требования | `docs/01-technical-specification.md` §2 | Уже есть: BG-1…BG-5 | 1 | ✅ |
| ≥ 5 пользовательских требований + use case диаграммы | `docs/requirements/use-cases.md` + `docs/requirements/diagrams/*.png` | UC-01…UC-16 уже описаны таблицей; нужны **диаграммы** (PlantUML / draw.io) | 4 | 🟡 |
| ≥ 15 функциональных требований | ТЗ §5 (FR-AUTH, FR-RT, FR-AUD, FR-ROOM, FR-CAT, FR-INT, FR-STAT) | Уже > 40 | 1 | ✅ |
| ≥ 5 нефункциональных требований | ТЗ §6 (NFR-SEC, NFR-TX, NFR-PERF, NFR-REL, NFR-MAINT) | Уже > 25 | 1 | ✅ |
| Верификация: способ проверки каждого требования; непроверяемые — переписать | `docs/requirements/verification-matrix.md` | Таблица «ID → способ проверки (тест / инспекция / демонстрация / анализ) → ссылка на тест» | 7 | ⬜ |
| Сверка с работающим приложением, дефекты в `ISSUES.md` | `ISSUES.md` | Формат: ID, требование, шаги, ожидание, факт, серьёзность, статус | 7 | ⬜ |
| Диаграмма состояний сущности с жизненным циклом | `docs/requirements/state-diagrams.md` | **Позиция очереди** `PLAYLIST_TRACKS.STATUS` (QUEUED→PLAYING→PLAYED/SKIPPED, QUEUED→REMOVED/AUTO_REMOVED) с событиями и условиями; дополнительно — **комната** (Active → Empty → Closed) | 4 | ⬜ |

### 2.2 Задание 2 — тесты

| Критерий | Реализация в SoundShare | Этап |
|---|---|:-:|
| Юнит-тесты | `tests/SoundShare.Domain.UnitTests` (агрегаты, переходы статусов, value objects), `tests/SoundShare.Application.UnitTests` (обработчики команд, валидаторы) | 4–6 |
| Моки зависимостей | NSubstitute для репозиториев, `IRoomNotifier`, `ITrackSourceClient`, `TimeProvider` | 5 |
| Интеграционные тесты с БД | `tests/SoundShare.Infrastructure.IntegrationTests` — Testcontainers (`gvenzl/oracle-free`), миграции DbUp, реальные ограничения и `MERGE` | 4 |
| Заглушки HTTP для внешних сервисов | WireMock.Net вместо YouTube Data API: успешный ответ, 403 quota exceeded, таймаут | 6 |
| Сценарные тесты переходов состояний | `tests/SoundShare.WebApi.ScenarioTests` — `WebApplicationFactory` + цепочка API-вызовов: добавить трек → голоса → автоудаление; играет → vote-to-skip → следующий трек | 6–7 |
| Arrange–Act–Assert, понятные имена | Соглашение `Method_State_ExpectedResult`, секции AAA | все |
| Параметризованные тесты | xUnit `[Theory]` + `[InlineData]` / `[MemberData]` (валидация username, пороги голосов, коды приглашений) | все |
| Негативные тесты | Исключения домена, некорректный ввод (400), граничные случаи (ровно 50 % skip, рейтинг −5, 3-й/4-й трек) | все |
| Независимость тестов | Свежие данные на тест, транзакция с откатом / пересоздание схемы, без общего состояния | все |
| Покрытие ≥ 70 % | coverlet + ReportGenerator, порог в CI; исключены миграции, `Program.cs`, конфигурация, сгенерированный код (`[ExcludeFromCodeCoverage]`) | 7 |

---

## 3. Темы собеседования C#/.NET (C) — где это видно в проекте

Цель: по каждой теме иметь **живой пример в коде** и уметь его объяснить. Конспект ответов — `docs/interview-notes.md`, пополняется на каждом этапе.

### 3.1 Базовые принципы
| Тема | Где в SoundShare |
|---|---|
| Git | Conventional Commits, `.gitattributes`, ветки + PR, история по этапам |
| ООП | Инкапсуляция агрегатов (`Room`, `QueueItem`: приватные сеттеры, методы поведения `Pin()`, `Skip()`), наследование `Entity`/`AggregateRoot`, полиморфизм стратегий |
| DRY, KISS, YAGNI | Решения DM-09 (лимиты — конфигурация), «полный список очереди вместо диффов», отказ от микросервисов |
| SOLID | S — один обработчик на команду; O — pipeline behaviors; L — реализации репозиториев; I — раздельные `I…ReadRepository`/`I…WriteRepository`; D — Application зависит от абстракций (`IRoomNotifier`, `ITrackSourceClient`) |

### 3.2 C#
| Тема | Где в SoundShare |
|---|---|
| Синтаксис, типы данных | `record`/`record struct` для value objects (`InviteCode`, `VoteValue`), `enum` статусов, `DateTime` UTC |
| ref / out / in | `InviteCode.TryParse(string, out InviteCode)`; `in` для readonly struct в горячем пути подсчёта голосов |
| Nullable | `<Nullable>enable</Nullable>`; `DateTime? PinnedAt`, `RevokedAt` ↔ nullable-колонки |
| Управляемые / неуправляемые ресурсы | `OracleConnection` и `IDisposable`/`IAsyncDisposable`, `await using`; `IHttpClientFactory` (исчерпание сокетов при `new HttpClient()`) |
| Сборщик мусора | Ограниченный `Channel<AuditEntry>` (нет бесконечного роста памяти), поколения, LOH, Server GC в контейнере |
| Исключения | Доменные исключения → `IExceptionHandler` → `ProblemDetails`; маппинг `ORA-00001` → 409, `ORA-02290` → 400 |
| Generics | `Result<T>`, `IRepository<TEntity, TKey>`, `IPipelineBehavior<TRequest, TResponse>` |
| Коллекции | `IReadOnlyCollection<T>` из агрегатов, `HashSet` для проверки голосов, `ConcurrentDictionary` для онлайн-присутствия |
| Асинхронность | `async`/`await` насквозь, `CancellationToken`, `BackgroundService`, `PeriodicTimer` (BR-ROOM-07) |
| Синхронизация потоков | `ConcurrentDictionary` + `Interlocked` в трекере присутствия, `SemaphoreSlim` для сериализации команд одной комнаты; на уровне БД — `SELECT … FOR UPDATE` |
| LINQ | Проекции в DTO, `IQueryable` против `IEnumerable` (где выполняется фильтр) |
| Entity Framework Core | Mapping-only поверх SQL-миграций, `AsNoTracking`, concurrency token `QUEUE_VERSION`, `SaveChangesInterceptor` (доменный аудит), `FromSql` для аналитики, транзакции |

### 3.3 .NET и Web API
| Тема | Где в SoundShare |
|---|---|
| Основы .NET Core | Generic Host, конфигурация, Options pattern (`RoomOptions`, `JwtOptions`) |
| Dependency Injection | Время жизни Scoped/Singleton/Transient; captive dependency — `BackgroundService` берёт `IServiceScopeFactory` |
| Web API | Контроллеры, привязка моделей, OpenAPI + Scalar, `ProblemDetails` |
| Конвейер обработки запроса | Порядок middleware: ExceptionHandler → CorrelationId → Authentication → Authorization → Audit → RateLimiter → Endpoint (ТЗ §5.3.2) |

### 3.4 Реляционные БД
| Тема | Где в SoundShare |
|---|---|
| Реляционная СУБД | Oracle 19c/21c/23ai: схемы, PDB, привилегии |
| Нормализация / денормализация | BCNF всех таблиц, таблица отвергнутых денормализаций и сценарий с материализованным представлением (`docs/02-data-model.md` §5) |
| Основы SQL | Этапы 4–6: DML, SELECT, `MERGE`, аналитические функции |
| Индексы | B-tree, уникальные, функциональные «частичные», IOT, индексы на FK; отчёт `EXPLAIN PLAN` (этап 7) |
| Структуры данных | B-дерево индекса, хеш-соединение, стек/очередь в `Channel<T>` |
| CAP-теорема | Один Oracle — согласованность важнее доступности; SignalR + Redis backplane — пример компромисса при масштабировании |
| Транзакции, ACID | Unit of Work, атомарное создание комнаты (UC-05), ротация refresh-токенов |
| Уровни изоляции | `READ COMMITTED` + `FOR UPDATE` против write skew; демонстрация `ORA-08177` в `SERIALIZABLE` (ТЗ §12.4) |

### 3.5 HTTP
| Тема | Где в SoundShare |
|---|---|
| Основы HTTP, REST | Ресурсно-ориентированный API, stateless, JWT |
| Именование URI | `/api/rooms/{roomId}/queue/items/{itemId}/vote`, существительные во множественном числе, без глаголов |
| Методы | `PUT …/vote` — идемпотентная установка голоса, `PATCH …/pin`, `DELETE …/items/{id}`, `POST /api/rooms` |
| Коды ответов | 200/201 (+`Location`)/204, 400/401/403/404, 409 (конфликт версии), 422, 429 (лимит) |
| Заголовки | `Authorization: Bearer`, `Set-Cookie` HttpOnly (refresh), `ETag`/`If-Match` по `QUEUE_VERSION`, `Retry-After`, `X-Correlation-Id` |

### 3.6 Good to know
| Тема | Где в SoundShare |
|---|---|
| AutoMapper | Сущность → DTO в Application-слое |
| FluentValidation | Валидаторы команд + pipeline behavior |
| JwtBearer | Access/Refresh (ТЗ §5.1), токен в SignalR через query string |
| N-Tier против Clean Architecture | Сравнение в `docs/interview-notes.md`; в проекте — Clean Architecture |
| Domain Driven Design | Агрегаты `Room` и `Queue`, value objects, доменные события `QueueChanged` |
| Docker | Compose, healthcheck, профили, multi-stage Dockerfile для API (этап 8) |

---

## 4. Соглашения, действующие во всём проекте

- **Комментарии в коде не пишутся**, кроме короткой шапки файла (назначение, кто запускает, примечания). Пояснения — в `docs/`.
- Коммиты — Conventional Commits, один логический шаг = один коммит.
- Скрипты БД совместимы с Oracle 19c/21c.

## 5. Открытые вопросы

1. Клиентская часть (нужна для этапа 6 и сценариев демонстрации): Blazor WebAssembly (весь стек на C#) или React + TypeScript?
2. Сроки сдачи заданий по тестированию ПО относительно этапов МДиСУБД.
