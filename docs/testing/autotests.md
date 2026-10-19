# Автоматизированные тесты (задание 2)

## 1. Состав

| Проект | Уровень | Метод | Тестов | Что проверяется |
|---|---|---|---:|---|
| `tests/SoundShare.Domain.UnitTests` | Модульный | Белый ящик | 72 | Сущности и правила: переходы статусов `QueueItem`, `QueueRules` (порог −5, большинство для пропуска, порядок очереди), `InviteCode`, `RoomMember` (бан, возврат), `RefreshToken`, `TitleParser` |
| `tests/SoundShare.Application.UnitTests` | Модульный, с моками | Белый ящик | 142 | Сервисы сценариев: `AuthService`, `UserAdminService`, `RoomService`, `QueueService`, `CatalogService`, валидаторы |
| `tests/SoundShare.IntegrationTests` → `Persistence` | Интеграционный (БД) | Серый ящик | 25 | Репозитории EF Core на настоящем Oracle, ограничения схемы, права `SOUNDSHARE_APP` |
| `tests/SoundShare.IntegrationTests` → `External` | Интеграционный (HTTP) | Серый ящик | 5 | Клиент YouTube Data API против заглушки WireMock: успех, пустой ответ, 403 quota, таймаут, нет ключа |
| `tests/SoundShare.IntegrationTests` → `Scenarios` | Сценарный | Чёрный ящик | 29 | Полные цепочки через HTTP API (`WebApplicationFactory`): переходы статусов, аутентификация, безопасность, аудит |
| **Итого** | | | **273** | |

## 2. Инструменты

- **xUnit** — тесты, `[Theory]` + `[InlineData]` для параметризации;
- **Shouldly** — читаемые проверки;
- **NSubstitute** — моки репозиториев, `ITokenService`, `IPasswordHasher`, `ITrackSource`, `IRoomNotifier`, `TimeProvider` (фиксированное время); `FakeUnitOfWork` — фейк транзакций;
- **Testcontainers** — на время прогона поднимается чистый контейнер `gvenzl/oracle-free:23-slim`, к нему применяются **те же SQL-миграции** `db/bootstrap` и `db/migrations`, что и в рабочей БД;
- **WireMock.Net** — HTTP-заглушка вместо YouTube Data API (`/youtube/v3/search`, `/youtube/v3/videos`);
- **Microsoft.AspNetCore.Mvc.Testing** — API поднимается в памяти (`WebApplicationFactory<Program>`) с настоящей БД из контейнера;
- **coverlet + ReportGenerator** — покрытие кода.

## 3. Соблюдение требований к тестам

| Требование | Как выполнено |
|---|---|
| Структура AAA | Каждый тест разделён на подготовку, действие и проверку пустыми строками |
| Понятные имена | `Метод_Условие_ОжидаемыйРезультат`: `Vote_ScoreReachesMinusFive_AutoRemovesItem`, `Refresh_RotatedTokenReused_RevokesWholeSession` |
| Параметризация | `[Theory]`: граничные значения имени пользователя (2/3/32/33 символа), пароля, голоса (−2…2), длительности бана (0, 1, 525 600, 525 601), ролей, некорректных инвайт-кодов, запрещённых SQL-операций |
| Негативные тесты | Неверный пароль, заблокированный аккаунт, повторное использование refresh-токена, 4-й трек (409), закрепление участником (403), чужая приватная комната (404), нарушение `CHECK`/`UNIQUE`/`FK`, 403 quota и таймаут YouTube |
| Независимость | Новый экземпляр класса и новые моки на каждый тест; в интеграционных тестах уникальные имена пользователей и комнат (`TestApi.UniqueName`), уникальные ID видео, отдельная заглушка WireMock на каждый поисковый запрос |
| Моки | NSubstitute в `Application.UnitTests` |
| БД | Testcontainers + реальный Oracle 23ai + миграции проекта |
| HTTP-заглушка | WireMock.Net вместо YouTube |
| Сценарии переходов состояний | `QueueLifecycleScenarioTests`, `AuthScenarioTests` |

## 4. Сценарные тесты переходов состояний

Диаграммы — [state-diagrams.md](../requirements/state-diagrams.md).

| Тест | Переходы |
|---|---|
| `QueuedTrack_DownvotedToMinusFive_BecomesAutoRemoved` | `QUEUED` → (6 голосов −1, рейтинг −5) → `AUTO_REMOVED`, трек исчезает из очереди |
| `QueuedTrack_DownvotedToMinusFour_StaysInQueue` | Граница: при −4 трек остаётся `QUEUED` |
| `PlayingTrack_SkipVotesReachMajority_SkippedAndNextStartsPlaying` | `PLAYING` → 1 голос из 3 (не хватает) → 2 голоса → `SKIPPED`; следующий `QUEUED` → `PLAYING` |
| `Moderator_PinsTrackAndSkips_PinnedTrackPlaysNext` | Участник не может закрепить (403); владелец назначает модератора; закреплённый трек становится первым и после пропуска — `PLAYING` |
| `Owner_LeavesRoom_OwnershipPassesToModerator` | Участник: `OWNER` уходит → модератор становится `OWNER` |
| `BannedMember_TriesToRejoin_GetsForbidden` | Участник: в комнате → забанен, повторный вход — 403 |
| `Refresh_RotatedTokenReused_RevokesWholeSession` | Токен: активен → заменён; повторное использование → отозваны все токены пользователя |

## 5. Покрытие

Покрытие считается по сборкам `Domain`, `Application`, `Infrastructure`, `WebApi`. Исключены `Program.cs` (конфигурация хоста), SQL-миграции (не C#), сгенерированный код и автосвойства.

| Сборка | Строки |
|---|---:|
| SoundShare.Domain | 94,6 % |
| SoundShare.Application | 86,4 % |
| SoundShare.Infrastructure | 86,7 % |
| SoundShare.WebApi | 80,9 % |
| **Итого** | **86,9 %** строк, **72,3 %** ветвей (порог — 70 %) |

Наименее покрыты `PlaylistService` и `RoomHub` (SignalR) — их сценарии относятся к клиенту и real-time и закрываются UI-тестами.

## 6. Запуск

```bash
dotnet test SoundShare.slnx
```

С отчётом покрытия (HTML-отчёт появится в `docs/testing/coverage/index.html`, в git не хранится):

```bash
dotnet test SoundShare.slnx --settings coverage.runsettings --results-directory TestResults
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:docs/testing/coverage -reporttypes:"Html;TextSummary"
```

Интеграционным тестам нужен запущенный Docker. Первый запуск скачивает образ Oracle; контейнер с БД поднимается один раз на весь прогон (`ICollectionFixture`) и удаляется после него.

## 7. Найденные проблемы

- Docker Desktop на машине разработчика не публикует случайный порт (`-p 0:1521` → контейнер завершается с кодом 255). Фикстура выбирает свободный порт сама (`TcpListener` на порт 0) и публикует его явно.
- Транзитивная зависимость `System.Security.Cryptography.Pkcs` 8.0.1 (через Oracle.EntityFrameworkCore и WireMock) имеет уязвимость GHSA-846f-f764-76mx; версия закреплена на 10.0.13 (`CentralPackageTransitivePinningEnabled`).
