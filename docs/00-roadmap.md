# SoundShare — Дорожная карта и матрица соответствия критериям

Проект закрывает три набора требований:

- **МДиСУБД** — совместные лабораторные работы (ЛР7), 8 этапов;
- **ВПО** — 5 заданий по требованиям и тестированию ПО;
- **Modsen** — минимальные требования к стажёру C#/.NET.

Каждый критерий привязан к артефакту в репозитории. Пункт закрыт, когда артефакт есть и проверен.

Статусы: ✅ готово · 🟡 в работе · ⬜ не начато

Решения: клиент — **Blazor WebAssembly** (весь стек на C#). Задания ВПО идут раньше этапа 7 МДиСУБД, поэтому работа идёт **вертикальными срезами** (раздел 1).

---

## 1. Порядок работ (вехи)

Задания ВПО требуют работающего приложения, поэтому после схемы БД сразу собирается минимальный, но законченный срез «от БД до UI». Этапы МДиСУБД 4–7 закрываются по ходу срезов.

| Веха | Содержание | Закрывает |
|---|---|---|
| **M0. Схема БД** | V001–V005, seed, тесты ограничений, первый `EXPLAIN PLAN` | МДиСУБД 3 |
| **M1. Документы требований и тестирования** (не требуют приложения) | `README.md` (каркас), use case диаграммы, диаграммы состояний, матрица верификации, шаблон `ISSUES.md`; план тестирования, чек-лист, ≥ 25 тест-кейсов | ВПО 1 (кроме сверки), ВПО 3.1–3.3 |
| **M2. Backend-срез** | Solution Clean Architecture; модули: аутентификация (JWT), управление пользователями (Admin), журнал аудита, комнаты, очередь, голоса; линтер в сборке; юнит + интеграционные тесты, покрытие ≥ 70 %; Postman-коллекция; базовые проверки безопасности | МДиСУБД 4–5, ВПО 2, ВПО 4.2, ВПО 4.3, ВПО 5.4 |
| **M3. Клиент и real-time** | Blazor WASM, SignalR, YouTube API; макеты интерфейсов; выполнение тест-кейсов со скриншотами, дефекты; вёрстка и кроссбраузерность; Playwright | МДиСУБД 6, ВПО 1 (сверка), ВПО 3.4–3.5, ВПО 4.1, ВПО 4.4 |
| **M4. Производительность** | Объёмные данные, нагрузочные тесты, отчёт об индексах, матрица трассируемости | МДиСУБД 7, ВПО 5.1–5.3, ВПО 5.5 |
| **M5. Сдача** | Руководство пользователя, финальный README, Docker Compose всей системы, CI, защита | МДиСУБД 8 |

Минимальный срез M2 содержит только то, что нужно для проверок: регистрация/вход/refresh, CRUD пользователей для Admin, журнал, создание/вход в комнату, добавление/удаление трека, голосование, закрепление. Real-time, YouTube и фоновые службы — в M3.

---

## 2. МДиСУБД — этапы лабораторных работ

| Этап | Требование курса | Артефакты SoundShare | Веха | Статус |
|---|---|---|:-:|:-:|
| 1 | ТЗ: сущности, функции (авторизация, управление пользователями с группами Admin/User, журналирование) | `docs/01-technical-specification.md` | — | ✅ |
| 2 | ER-схема, описание сущностей и связей | `docs/02-data-model.md` (+ Crow's Foot в отчёт) | — | ✅ |
| 3 | Полный набор SQL по созданию схемы | `db/bootstrap/`, `db/migrations/V001…V005`, `db/seed/`, `db/tests/` | M0 | 🟡 |
| 4 | DML-запросы с описанием | `db/queries/04-dml.sql`, `docs/04-dml.md` | M2 | ⬜ |
| 5 | SELECT-запросы с описанием | `db/queries/05-select.sql`, `docs/05-select.md` (очередь с `ROW_NUMBER`, рейтинги `RANK`/`DENSE_RANK`) | M2 | ⬜ |
| 6 | Запросы журналирования, интерфейсы, проволочные макеты | `db/queries/06-audit.sql`, `docs/06-audit-and-ui.md`, `docs/wireframes/` | M3 | ⬜ |
| 7 | Приложение, тестовые данные, обоснование индексов | `db/seed/volume/`, `db/explain/`, `docs/07-index-report.md` | M4 | ⬜ |
| 8 | Защита, руководство пользователя | `README.md`, `docs/08-user-manual.md` | M5 | ⬜ |

Правило: SQL-артефакт этапа пишется первым, затем тот же сценарий реализуется в коде (например, `MERGE` голоса на SQL → репозиторий).

---

## 3. ВПО — требования и тестирование

### 3.1 Задание 1 — требования (M1)

| Критерий | Артефакт | Статус |
|---|---|:-:|
| README: название, назначение, пользователи, стек | `README.md` | ✅ |
| README: схема компонентов, модули, точки интеграции, сборка/запуск | `README.md` (Mermaid `flowchart`) — дополняется в M2/M3 | 🟡 |
| Бизнес-требования | ТЗ §2 (BG-1…BG-5) | ✅ |
| ≥ 5 пользовательских требований + use case диаграммы | ТЗ §3.4 (UC-01…UC-16) + `docs/requirements/use-cases.md` с диаграммами (PlantUML → PNG) | 🟡 |
| ≥ 15 функциональных, ≥ 5 нефункциональных | ТЗ §5, §6 | ✅ |
| Способ проверки каждого требования; непроверяемые — переписать | `docs/requirements/verification-matrix.md` | ⬜ |
| Сверка с приложением, дефекты | `ISSUES.md` (M3) | ⬜ |
| Диаграмма состояний | `docs/requirements/state-diagrams.md`: позиция очереди (`QUEUED → PLAYING → PLAYED/SKIPPED`, `QUEUED → REMOVED/AUTO_REMOVED`), комната (Active → Empty → Closed) | ⬜ |

### 3.2 Задание 2 — автотесты (M2)

| Критерий | Реализация |
|---|---|
| Юнит-тесты + моки | `tests/SoundShare.Domain.UnitTests`, `tests/SoundShare.Application.UnitTests`; NSubstitute для репозиториев, `IRoomNotifier`, `ITrackSourceClient`, `TimeProvider` |
| Интеграционные с БД | `tests/SoundShare.Infrastructure.IntegrationTests` — Testcontainers (`gvenzl/oracle-free`) + миграции |
| Заглушки HTTP | WireMock.Net вместо YouTube Data API (успех, 403 quota, таймаут) |
| Сценарии переходов состояний | `tests/SoundShare.WebApi.ScenarioTests` — `WebApplicationFactory`: добавить трек → голоса → `AUTO_REMOVED`; `PLAYING` → vote-to-skip → `SKIPPED` → следующий `PLAYING` |
| AAA, имена, параметризация, негативные, независимость | `Method_State_ExpectedResult`, xUnit `[Theory]`, граничные значения (−5, ровно 50 %, 3-й/4-й трек), свежие данные на тест |
| Покрытие ≥ 70 % | coverlet + ReportGenerator, порог в CI; исключены миграции, `Program.cs`, конфигурация, сгенерированный код |

### 3.3 Задание 3 — ручное тестирование

| Критерий | Артефакт | Веха |
|---|---|:-:|
| План тестирования: цель; области тестирования и исключения со ссылками на FR/NFR и обоснованием; стратегия (уровни, виды, инструменты); критерии приёмки/начала/приостановки/возобновления/завершения через метрики; метрики (успешность, выполнение, покрытие требований, устранение дефектов по важности — формулы из пособия §6.2) | `docs/testing/test-plan.md` | M1 |
| Чек-лист по всем FR, сгруппирован по модулям, позитивные и негативные проверки | `docs/testing/checklist.md` | M1 |
| ≥ 25 тест-кейсов, покрыты все FR с приоритетом Must; атрибуты: id, приоритет, требование, модуль/подмодуль, заглавие, исходные данные, шаги, ожидаемый результат | `docs/testing/test-cases.md` | M1 |
| Выполнение: результат, статус, скриншоты/видео | `docs/testing/test-run-<дата>.md`, `docs/testing/evidence/` | M3 |
| Дефекты: краткое и подробное описание, ожидаемый/фактический результат, требование, шаги, воспроизводимость, важность, срочность, симптом, обходной путь | `ISSUES.md` | M3 |

Модули для группировки (совпадают с модулями приложения): Auth, Users (Admin), Audit, Rooms, Queue, Votes, Playback/Real-time, Catalog/YouTube.

### 3.4 Задание 4 — UI, API, безопасность, автоматизация UI

| Критерий | Реализация | Веха |
|---|---|:-:|
| Вёрстка: целостность блоков, сетка, текст вокруг изображений (обложки треков) | Ручная проверка по чек-листу, `docs/testing/ui-compat.md` | M3 |
| Адаптивность: ширины экрана, масштаб | DevTools device toolbar: 360, 768, 1280, 1920 px; масштаб 50–200 % | M3 |
| Интерактивные элементы: ссылки/кнопки, фокус, hover, логотип → главная, favicon | Чек-лист в `ui-compat.md` | M3 |
| Реальные данные: длинные/короткие строки, пустые списки, много элементов | Seed с крайними значениями (название трека 300 символов, пустая очередь, 100 участников) | M3 |
| Нет 404 и ошибок в консоли | DevTools Network/Console, скриншоты | M3 |
| Кроссбраузерность ≥ 3 браузеров + мобильная эмуляция; таблица «проверка × браузер» | Chrome, Firefox, Edge (+ WebKit через Playwright) | M3 |
| Postman: все эндпоинты use case; позитивные (код, структура, значения), негативные (без токена, некорректные данные, несуществующие ручки); связывание (токен логина → дальнейшие запросы); запуск с отчётом | `tests/postman/SoundShare.postman_collection.json` + environment; Newman с отчётом `htmlextra` | M2 |
| Безопасность: доступ без авторизации по прямому URL; IDOR (подмена id); ошибки без стека, путей и SQL; логирование операций и ошибок | Postman-папка `Security` + `docs/testing/security-checks.md`; опирается на NFR-SEC-05/08, FR-AUD | M2 |
| UI-автотесты: Page Object, свои данные на тест, явные ожидания, скриншот при падении | `tests/SoundShare.UiTests` — Playwright for .NET + xUnit; данные через API; `Expect(...)` вместо пауз; скриншот и trace в `TestResults/` при падении | M3 |

### 3.5 Задание 5 — нагрузка, линтер, трассируемость

| Критерий | Реализация | Веха |
|---|---|:-:|
| План нагрузочного тестирования: цели из NFR (NFR-PERF-01/02, FR-RT-01), профиль нагрузки (действия, число пользователей, паузы), виды (нагрузочный, стрессовый с восстановлением), критерии успеха (доля ошибок, время ответа) | `docs/testing/load-test-plan.md` | M4 |
| Прогоны: базовый (1 пользователь), нагрузочный (целевое число), стрессовый (до деградации) | `tests/SoundShare.LoadTests` — **NBomber** (C#), HTML-отчёты в `docs/testing/load/` | M4 |
| Результаты: пользователи, RPS, доля ошибок; график «время отклика × пользователи», точка деградации; гипотеза об узком месте (запросы к БД, индексы, пул соединений) | `docs/testing/load-test-report.md`; связь с отчётом об индексах МДиСУБД 7 (`EXPLAIN PLAN` до/после) | M4 |
| Статическая проверка кода линтером, исправление нарушений | .NET analyzers (`AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`), `SonarAnalyzer.CSharp`, `dotnet format --verify-no-changes` в CI; отчёт `docs/testing/lint-report.md` | M2 (настройка) / M4 (отчёт) |
| Матрица трассируемости: требования ↔ тест-кейсы ↔ автотесты ↔ дефекты; покрытие требований; требования без тестов; тесты без требований; зоны риска по числу дефектов | `docs/testing/traceability-matrix.md` | M4 |

---

## 4. Modsen — темы собеседования и их отражение в проекте

Конспект ответов — `docs/interview-notes.md`, пополняется на каждой вехе.

### 4.1 Базовые принципы
| Тема | Где в SoundShare |
|---|---|
| Git | Conventional Commits, `.gitattributes`, ветки + PR |
| ООП | Инкапсуляция агрегатов (`Room`, `QueueItem`: приватные сеттеры, методы `Pin()`, `Skip()`), `Entity`/`AggregateRoot`, полиморфизм |
| DRY, KISS, YAGNI | DM-09, «полный список очереди вместо диффов», модульный монолит |
| SOLID | S — обработчик на команду; O — pipeline behaviors; L — репозитории; I — раздельные read/write интерфейсы; D — Application зависит от абстракций |

### 4.2 C#
| Тема | Где в SoundShare |
|---|---|
| Синтаксис, типы данных | `record`/`record struct` для value objects (`InviteCode`, `VoteValue`), `enum` статусов |
| ref / out / in | `InviteCode.TryParse(string, out InviteCode)`; `in` для readonly struct |
| Nullable | `<Nullable>enable</Nullable>`, `DateTime? PinnedAt` ↔ nullable-колонки |
| Ресурсы | `OracleConnection`, `IAsyncDisposable`, `await using`; `IHttpClientFactory` |
| GC | Ограниченный `Channel<AuditEntry>`, поколения, LOH, Server GC |
| Исключения | Доменные исключения → `IExceptionHandler` → `ProblemDetails`; `ORA-00001` → 409 |
| Generics | `Result<T>`, `IRepository<TEntity, TKey>`, `IPipelineBehavior<TRequest, TResponse>` |
| Коллекции | `IReadOnlyCollection<T>`, `HashSet`, `ConcurrentDictionary` для присутствия |
| Асинхронность | `async`/`await`, `CancellationToken`, `BackgroundService`, `PeriodicTimer` |
| Синхронизация потоков | `ConcurrentDictionary` + `Interlocked`, `SemaphoreSlim`; в БД — `SELECT … FOR UPDATE` |
| LINQ | Проекции в DTO, `IQueryable` против `IEnumerable` |
| EF Core | Mapping-only, `AsNoTracking`, concurrency token `QUEUE_VERSION`, `SaveChangesInterceptor`, `FromSql` |

### 4.3 .NET, Web API, Blazor
| Тема | Где в SoundShare |
|---|---|
| Основы .NET | Generic Host, конфигурация, Options pattern |
| DI | Scoped/Singleton/Transient; `IServiceScopeFactory` в `BackgroundService`; DI в Blazor WASM |
| Web API | Контроллеры, привязка моделей, OpenAPI + Scalar, `ProblemDetails` |
| Конвейер запроса | ExceptionHandler → CorrelationId → Authentication → Authorization → Audit → RateLimiter → Endpoint |
| Blazor WebAssembly | Компоненты, `AuthenticationStateProvider` на JWT, `HubConnection` SignalR, JS interop для YouTube IFrame Player |

### 4.4 Реляционные БД
| Тема | Где в SoundShare |
|---|---|
| Нормализация / денормализация | BCNF, таблица отвергнутых денормализаций (`02-data-model.md` §5) |
| SQL | DML, SELECT, `MERGE`, аналитические функции (МДиСУБД 4–6) |
| Индексы, структуры данных | B-tree, функциональные «частичные», IOT, индексы на FK; `EXPLAIN PLAN` (МДиСУБД 7) |
| CAP | Один Oracle — согласованность; SignalR + Redis backplane при масштабировании |
| Транзакции, ACID, изоляция | Unit of Work, `READ COMMITTED` + `FOR UPDATE`, `ORA-08177` в `SERIALIZABLE` |
| Нагрузка и узкие места | Связь нагрузочного теста (ВПО 5) с планами запросов и пулом соединений |

### 4.5 HTTP
| Тема | Где в SoundShare |
|---|---|
| REST, URI | `/api/rooms/{roomId}/queue/items/{itemId}/vote` |
| Методы | `PUT …/vote` (идемпотентно), `PATCH …/pin`, `DELETE …/items/{id}`, `POST /api/rooms` |
| Коды | 200/201 (+`Location`)/204, 400/401/403/404, 409, 422, 429 |
| Заголовки | `Authorization`, `Set-Cookie` HttpOnly, `ETag`/`If-Match`, `Retry-After`, `X-Correlation-Id` |

### 4.6 Good to know
| Тема | Где в SoundShare |
|---|---|
| AutoMapper, FluentValidation, JwtBearer | Application-слой, pipeline behavior, Access/Refresh |
| N-Tier против Clean Architecture | Сравнение в `docs/interview-notes.md` |
| DDD | Агрегаты `Room`, `Queue`, value objects, доменные события |
| Docker | Compose, healthcheck, профили, multi-stage Dockerfile |

---

## 5. Соглашения проекта

- Комментарии в коде не пишутся, кроме короткой шапки файла. Пояснения — в `docs/`.
- Conventional Commits, один логический шаг = один коммит.
- Скрипты БД совместимы с Oracle 19c/21c.
