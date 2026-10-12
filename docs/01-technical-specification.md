# SoundShare — Техническое задание (Этап 1)

> **Документ:** ТЗ на автоматизированную систему (структура адаптирована по ГОСТ 34.602-2020 + практики SRS / IEEE 29148)
> **Версия:** 1.0
> **Статус:** Утверждено (2026-10-03). Детализация модели данных — [02-data-model.md](02-data-model.md)
> **Шифр проекта:** `SSH-2026`

---

## 1. Общие сведения

| Параметр | Значение |
|---|---|
| Полное наименование | Сервис совместного прослушивания музыки и коллаборативного формирования плейлистов «SoundShare» |
| Краткое наименование | SoundShare, АС «SoundShare» |
| Заказчик | Кафедра (в рамках ЛР по МДиСУБД / проектированию ПО) |
| Исполнитель | Команда разработки SoundShare |
| Основание | Учебный план; цель команды — Production-Ready портфолио-проект |
| Плановые сроки | 8 итеративных этапов (ТЗ → ER/даталогия → DDL → ORM/Infrastructure → Application → API/Realtime → Тесты/оптимизация → Деплой) |

### 1.1 Глоссарий

| Термин | Определение |
|---|---|
| **Комната (Room)** | Виртуальное пространство, где участники слушают одну и ту же очередь треков синхронно. |
| **Хост / Владелец комнаты (Owner)** | Пользователь, создавший комнату. Роль *внутри комнаты*, не системная. |
| **Очередь (Queue)** | Плейлист, привязанный к комнате, порядок которого определяется голосованием. |
| **Позиция очереди (PlaylistTrack)** | Конкретное вхождение трека в плейлист (один и тот же трек может быть добавлен дважды — это две разные позиции). |
| **Голос (Vote)** | Оценка позиции очереди пользователем: `+1` / `-1`. |
| **Access Token** | Короткоживущий JWT для авторизации запросов. |
| **Refresh Token** | Долгоживущий непрозрачный (opaque) токен для выпуска новой пары токенов. |
| **Audit Trail** | Неизменяемый журнал значимых действий пользователей и системы. |
| **Hub** | Серверная точка SignalR, через которую идёт двунаправленный обмен сообщениями. |

---

## 2. Назначение и бизнес-цели системы

### 2.1 Проблема
Существующие стриминговые сервисы ориентированы на индивидуальное прослушивание. В групповых сценариях (вечеринка, офис, стрим, учебная аудитория) музыку контролирует один человек, остальные не влияют на выбор, а «общие плейлисты» не отражают текущих предпочтений группы.

### 2.2 Назначение
SoundShare предоставляет **комнаты синхронного прослушивания**, в которых очередь треков формируется **коллективно в реальном времени**: участники добавляют треки и голосуют, а система автоматически переупорядочивает очередь.

### 2.3 Бизнес-цели и измеримые показатели (KPI)

| ID | Цель | Метрика успеха (для демо/нагрузочного теста) |
|---|---|---|
| BG-1 | Вовлечение группы в выбор музыки | ≥ 60 % участников комнаты совершают хотя бы одно действие (add/vote) за сессию |
| BG-2 | Ощущение «живого» интерфейса | Изменение очереди доставляется всем клиентам комнаты за **p95 ≤ 300 мс** |
| BG-3 | Синхронность воспроизведения | Рассинхрон позиции трека между клиентами ≤ 1 с |
| BG-4 | Доверие и прозрачность | 100 % изменяющих операций фиксируются в Audit Trail |
| BG-5 | Портфолио-качество | Покрытие Domain+Application тестами ≥ 70 %, запуск всей системы одной командой `docker compose up` |

### 2.4 Границы системы (Scope)

**В рамках (In scope):** регистрация/аутентификация, управление комнатами, каталог треков и исполнителей (метаданные), очередь с голосованием, синхронизация состояния воспроизведения, журнал аудита, административная панель (API).

**Вне рамок (Out of scope, YAGNI):** собственный аудио-стриминг/хранилище медиафайлов с DRM, рекомендательная система на ML, мобильные нативные клиенты, платёжные функции, чат в комнате *(кандидат на v2)*.

---

## 3. Ролевая модель

Система использует **два уровня ролей**. Это ключевое архитектурное решение: смешивать их в одной таблице — частая ошибка.

### 3.1 Системные роли (глобальные, `ROLES` + `USERS.ROLE_ID`)

| Роль | Описание | Ключевые права |
|---|---|---|
| **Admin** | Администратор платформы | Управление каталогом (Tracks, Artists), блокировка пользователей, принудительное закрытие комнат, просмотр Audit Trail, просмотр статистики |
| **User** | Зарегистрированный пользователь | Создание комнат, вход в комнаты, добавление треков, голосование, управление своими плейлистами |

Реализация: claim `role` в Access Token → `[Authorize(Roles = "Admin")]` / политики (`AddAuthorization(o => o.AddPolicy(...))`).

### 3.2 Роли внутри комнаты (контекстные, `ROOM_MEMBERS.MEMBER_ROLE`)

| Роль | Права |
|---|---|
| **Owner** | Все права Moderator + удаление комнаты, назначение модераторов, передача владения |
| **Moderator** | Удаление чужих треков из очереди, принудительный skip текущего трека, исключение (kick) участника, **закрепление/открепление трека (pin)** |
| **Contributor** | Добавление треков (≤ 3 в очереди), голосование +1/−1, vote-to-skip, прослушивание |

Автоматическая передача владения — см. BR-ROOM-05 (раздел 12).

> **Почему так:** системная роль отвечает на вопрос «кто ты на платформе», контекстная — «кто ты в этой комнате». Admin в чужой комнате по умолчанию — Listener, но обладает «override»-правами через отдельную политику. Проверка контекстных прав выполняется в Application-слое (resource-based authorization: `IAuthorizationService.AuthorizeAsync(user, room, requirement)`), а не атрибутами.

### 3.3 Матрица доступа (RBAC)

| Операция | Guest | Contributor | Moderator | Owner | Admin |
|---|:-:|:-:|:-:|:-:|:-:|
| Регистрация / вход | ✅ | — | — | — | — |
| Просмотр публичных комнат | ❌ | ✅ | ✅ | ✅ | ✅ |
| Создать комнату | ❌ | ✅ | ✅ | ✅ | ✅ |
| Добавить трек в очередь | ❌ | ✅ | ✅ | ✅ | ✅ |
| Голосовать | ❌ | ✅ | ✅ | ✅ | ✅ |
| Удалить чужой трек / skip | ❌ | ❌ | ✅ | ✅ | ✅ |
| Kick участника | ❌ | ❌ | ✅ | ✅ | ✅ |
| Закрепить / открепить трек | ❌ | ❌ | ✅ | ✅ | ✅ |
| Vote-to-skip | ❌ | ✅ | ✅ | ✅ | ✅ |
| Удалить комнату | ❌ | ❌ | ❌ | ✅ | ✅ |
| CRUD каталога Tracks/Artists | ❌ | ❌ | ❌ | ❌ | ✅ |
| Чтение Audit Trail | ❌ | ❌ | ❌ | ❌ | ✅ |
| Блокировка пользователей | ❌ | ❌ | ❌ | ❌ | ✅ |

### 3.4 Сценарии использования (Use Cases)

| ID | Актор | Сценарий | Предусловие | Основной поток (кратко) | Постусловие |
|---|---|---|---|---|---|
| UC-01 | Guest | Регистрация | — | Ввод email/username/password → валидация (FluentValidation) → хэширование пароля → создание User с ролью `User` | Аккаунт создан, событие в аудите |
| UC-02 | Guest | Вход | Аккаунт активен | Проверка пароля → выдача Access + Refresh | Создана refresh-сессия |
| UC-03 | User | Обновление токенов | Валидный refresh | Ротация refresh, выдача новой пары | Старый refresh отозван |
| UC-04 | User | Выход | Авторизован | Отзыв текущего refresh | Сессия закрыта |
| UC-05 | User | Создание комнаты | Авторизован | Ввод названия/приватности → создаётся Room + Playlist-очередь + RoomMember(Owner) **в одной транзакции** | Комната активна |
| UC-06 | User | Вход в комнату | Комната активна, есть доступ | REST join → подключение к Hub → получение snapshot очереди | Пользователь — участник группы SignalR |
| UC-07 | Listener | Добавление трека | Участник комнаты | Поиск по каталогу → добавление → broadcast `QueueUpdated` | Новая позиция в очереди |
| UC-08 | Listener | Голосование | Участник, позиция не проиграна | Upsert голоса → пересчёт рейтинга → broadcast | Порядок очереди обновлён |
| UC-09 | Moderator | Skip / удаление позиции | Права Moderator+ | Команда → смена текущего трека → broadcast `PlaybackChanged` | — |
| UC-10 | Owner | Управление участниками | Owner | Назначить модератора / kick / передать владение | Изменена роль, аудит |
| UC-11 | Owner/Admin | Закрытие комнаты | — | Soft-close комнаты → всем клиентам `RoomClosed` → отключение из группы | Комната неактивна |
| UC-12 | User | Сохранение очереди как личного плейлиста | — | Копирование PlaylistTracks в новый Playlist | Личный плейлист |
| UC-13 | Admin | Управление каталогом | Admin | CRUD Artists/Tracks | Каталог изменён |
| UC-14 | Admin | Просмотр журнала аудита | Admin | Фильтры: пользователь, тип действия, период; пагинация | — |
| UC-15 | Admin | Блокировка пользователя | Admin | `IS_ACTIVE = 0` + отзыв всех refresh-токенов | Пользователь не может войти/обновиться |
| UC-16 | System | Автопереход к следующему треку | Трек закончился | Фоновая служба определяет следующую позицию по рейтингу | `PlaybackChanged` всем |

---

## 4. Информационная модель (перечень сущностей)

### 4.1 Сущности и обоснование

> Ниже — **концептуальный** уровень. Точные типы и DDL — Этапы 2–3. Имена таблиц указаны в целевом виде для Oracle (UPPER_SNAKE_CASE).

| # | Сущность → таблица | Назначение | Ключевые атрибуты (концептуально) | Обоснование / вклад в 3NF |
|---|---|---|---|---|
| 1 | **Roles** → `ROLES` | Справочник системных ролей | Id, Name (unique) | Вынос названия роли из `USERS` устраняет повтор строк «Admin»/«User» и аномалию обновления (переименование роли = 1 строка). Справочник — классический приём 2NF/3NF. |
| 2 | **Users** → `USERS` | Учётная запись | Id, Username (unique), Email (unique), PasswordHash, RoleId (FK), IsActive, CreatedAt | Все атрибуты зависят только от `Id`. Название роли не хранится (транзитивная зависимость `UserId → RoleId → RoleName` убрана в `ROLES`). |
| 3 | **Artists** → `ARTISTS` | Исполнитель | Id, Name, Country?, … | Без этой таблицы имя исполнителя дублировалось бы в каждом треке: `TrackId → ArtistId → ArtistName` — транзитивная зависимость, нарушение 3NF. |
| 4 | **Tracks** → `TRACKS` | Трек локального каталога (кэш метаданных внешнего источника) | Id, Title, DurationSec, ExternalSource ('YOUTUBE'), ExternalSourceId, ThumbnailUrl?, CreatedAt; UNIQUE(ExternalSource, ExternalSourceId) | Хранит только факты о треке, без исполнителей (они в `TRACK_ARTISTS`). Длительность нужна серверу для автоперехода (UC-16). Уникальность по внешнему ID исключает дубли при повторном импорте. |
| 4a | **TrackArtists** → `TRACK_ARTISTS` | Связь M:N Tracks↔Artists (feat.) | TrackId, ArtistId (составной PK), ArtistOrder, IsPrimary | Список исполнителей в одной колонке (`'A feat. B'`) нарушил бы 1NF. `ArtistOrder` зависит от пары целиком — 2NF соблюдена. |
| 5 | **Rooms** → `ROOMS` | Комната прослушивания | Id, Name, OwnerId (FK→USERS), IsPrivate, InviteCode? (UNIQUE), IsActive, CurrentPlaylistTrackId?, PlaybackStartedAt?, EmptySince?, MaxMembers (=100), CreatedAt | Хранит *состояние воспроизведения* (что играет и с какого момента UTC) — источник истины для «виртуального таймера». `EmptySince` — отметка для фоновой службы автозакрытия. |
| 6 | **RoomMembers** → `ROOM_MEMBERS` | Связь M:N Users↔Rooms + контекстная роль | RoomId, UserId (UNIQUE пары), MemberRole, JoinedAt, LeftAt? | Разрешает связь M:N. Атрибуты зависят от **пары** (Room, User) целиком — 2NF. `LeftAt IS NULL` = активное членство; правило «одна активная комната» — функциональным уникальным индексом (BR-ROOM-06). |
| 7 | **Playlists** → `PLAYLISTS` | Плейлист: очередь комнаты или личный | Id, Name, OwnerId (FK), RoomId? (FK, nullable), CreatedAt | Разделение Room и Playlist: комната — «где слушаем», плейлист — «что слушаем». Позволяет сохранять очередь как личный плейлист (UC-12) без дублирования структуры. |
| 8 | **PlaylistTracks** → `PLAYLIST_TRACKS` | Позиция трека в плейлисте | Id (суррогат), PlaylistId, TrackId, AddedById, AddedAt, Status (Queued/Playing/Played/Removed/AutoRemoved/Skipped), IsPinned NUMBER(1), PinnedAt? | Связь M:N Playlists↔Tracks. **Суррогатный ключ** нужен, т.к. один трек может быть в очереди дважды, и голоса привязываются к *конкретной позиции*. |
| 9 | **TrackVotes** → `TRACK_VOTES` | Голос пользователя за позицию | Id, PlaylistTrackId (FK), UserId (FK), Value (+1/−1), VotedAt; UNIQUE(PlaylistTrackId, UserId) | Голоса — отдельный факт, а **не** счётчик в `PLAYLIST_TRACKS`. Хранение `Score` в позиции было бы производным атрибутом (денормализацией) с риском рассинхронизации и lost update при конкурентных голосах. Рейтинг вычисляется `SUM(VALUE)` + `RANK() OVER (...)`. |
| 10 | **AuditLogs** → `AUDIT_LOGS` | Неизменяемый журнал действий | Id, UserId? (nullable — анонимные действия), Action, EntityType?, EntityId?, HttpMethod, Path, StatusCode, IpAddress, CorrelationId, DurationMs, Details (JSON), CreatedAt | Append-only таблица. `UserId` хранится без каскадного удаления (журнал должен пережить удаление пользователя → `ON DELETE SET NULL`). Username **не** дублируется (3NF) — получается JOIN-ом. |

### 4.2 Дополнительные сущности (по итогам бизнес-правил)

| # | Сущность | Зачем |
|---|---|---|
| 11 | **RefreshTokens** → `REFRESH_TOKENS` | **Обязательна** для механизма из раздела 5.1: Id, UserId, TokenHash, ExpiresAt, CreatedAt, RevokedAt?, ReplacedByTokenId?, CreatedByIp. Без неё невозможны ротация, отзыв и logout со всех устройств. |
| 12 | ⚑ **SkipVotes** → `SKIP_VOTES` | Голоса vote-to-skip за **текущий** трек: Id, PlaylistTrackId, UserId, CreatedAt; UNIQUE(PlaylistTrackId, UserId). Семантика отличается от like/dislike (лайк ≠ «пропустить»), поэтому отдельная таблица, а не тип в `TRACK_VOTES`. |

Итого **13 таблиц**: ROLES, USERS, REFRESH_TOKENS, ARTISTS, TRACKS, TRACK_ARTISTS, ROOMS, ROOM_MEMBERS, PLAYLISTS, PLAYLIST_TRACKS, TRACK_VOTES, SKIP_VOTES, AUDIT_LOGS.

### 4.3 Связи (кратко)

```
ROLES 1───N USERS 1───N ROOMS (owner)
USERS N───M ROOMS        via ROOM_MEMBERS (≤ 1 активной на пользователя)
TRACKS N───M ARTISTS     via TRACK_ARTISTS
PLAYLISTS N───M TRACKS   via PLAYLIST_TRACKS
ROOMS 1───0..1 PLAYLISTS (active queue)
PLAYLIST_TRACKS 1───N TRACK_VOTES N───1 USERS
PLAYLIST_TRACKS 1───N SKIP_VOTES  N───1 USERS
USERS 1───N REFRESH_TOKENS
USERS 0..1───N AUDIT_LOGS
```

### 4.4 Проверка нормальных форм (сводка)

- **1NF:** все атрибуты атомарны; нет списков (исполнители, голоса, участники вынесены в отдельные таблицы).
- **2NF:** во всех таблицах со составными естественными ключами (`ROOM_MEMBERS`, `TRACK_VOTES`) неключевые атрибуты зависят от ключа целиком.
- **3NF:** транзитивные зависимости вынесены (`ROLES`, `ARTISTS`); производные значения (`Score`, `MembersCount`) не хранятся, а вычисляются.
- **Осознанные исключения:** `ROOMS.CURRENT_PLAYLIST_TRACK_ID` формально дублирует `PLAYLIST_TRACKS.STATUS = 'Playing'`. Решение: оставить только одно из двух (обсудим на Этапе 2) — это хороший пример для защиты ЛР.

---

## 5. Функциональные требования

Нотация: **FR-<модуль>-<№>**, приоритет по MoSCoW (M — Must, S — Should, C — Could).

### 5.1 Модуль «Аутентификация и авторизация» (FR-AUTH)

#### 5.1.1 Механизм Access/Refresh

```
Client                          API                              Oracle
  │ POST /api/auth/login          │                                 │
  │──────────────────────────────►│ verify PasswordHash             │
  │                               │ create REFRESH_TOKENS(hash) ───►│
  │◄── 200 {accessToken}  ────────│                                 │
  │    Set-Cookie: rt=<opaque>; HttpOnly; Secure; SameSite=Strict; Path=/api/auth
  │                               │                                 │
  │ GET /api/rooms  (Bearer AT)   │ JwtBearer validates signature,  │
  │──────────────────────────────►│ iss, aud, exp — БЕЗ похода в БД │
  │                               │                                 │
  │ (AT expired → 401)            │                                 │
  │ POST /api/auth/refresh (cookie)                                 │
  │──────────────────────────────►│ find by SHA256(rt), check       │
  │                               │ not revoked/expired ───────────►│
  │                               │ revoke old, insert new (1 tx) ─►│
  │◄── 200 {newAT} + new cookie ──│                                 │
```

| ID | Требование | Приоритет |
|---|---|:-:|
| FR-AUTH-01 | Регистрация с валидацией: email (RFC-формат, уникален), username (3–32, `[a-zA-Z0-9_]`, уникален), пароль (≥ 8 символов, буквы+цифры). | M |
| FR-AUTH-02 | Пароли хранятся только в виде хэша с солью (PBKDF2 через `PasswordHasher<T>` из ASP.NET Core Identity, ≥ 100 000 итераций, либо Argon2id). | M |
| FR-AUTH-03 | **Access Token** — JWT (HS256 для MVP / RS256 для prod), TTL **15 мин**. Claims: `sub` (UserId), `unique_name`, `role`, `jti`, `iat`, `exp`. Не содержит персональных данных сверх необходимого. | M |
| FR-AUTH-04 | **Refresh Token** — криптографически случайные 64 байта (`RandomNumberGenerator`), TTL **7 дней**. В БД хранится **только SHA-256 хэш**. Передаётся в `HttpOnly; Secure; SameSite=Strict` cookie. | M |
| FR-AUTH-05 | **Ротация:** каждый refresh выдаёт новый RT и отзывает старый (`REVOKED_AT`, `REPLACED_BY_TOKEN_ID`) в одной транзакции. | M |
| FR-AUTH-06 | **Reuse detection:** предъявление уже отозванного RT ⇒ отзыв всей цепочки токенов пользователя (признак кражи) + запись в аудит с уровнем `Security`. | S |
| FR-AUTH-07 | Logout отзывает текущий RT; «выйти со всех устройств» — все RT пользователя. | M |
| FR-AUTH-08 | Блокировка пользователя (Admin) отзывает все RT; уже выданный AT доживает ≤ 15 мин (осознанный компромисс stateless JWT, фиксируем в ТЗ). | M |
| FR-AUTH-09 | Rate limiting на `/api/auth/*`: ≤ 5 попыток входа / мин / IP (встроенный `Microsoft.AspNetCore.RateLimiting`). | S |
| FR-AUTH-10 | Авторизация SignalR: AT передаётся в query `access_token` (браузерный WebSocket API не поддерживает заголовки), извлекается в `JwtBearerEvents.OnMessageReceived` **только для пути `/hubs/*`**. | M |
| FR-AUTH-11 | Долгоживущее WebSocket-соединение закрывается по истечении AT (`CloseOnAuthenticationExpiration = true`), клиент переподключается с новым токеном. | S |

> **Internals .NET:** `JwtBearerHandler` валидирует токен локально (подпись + `TokenValidationParameters`), поэтому AT не требует обращения к БД — это и есть масштабируемость stateless-подхода. Цена — невозможность мгновенного отзыва AT, отсюда короткий TTL. Не забудьте `ClockSkew = TimeSpan.FromSeconds(30)` — по умолчанию он **5 минут**, что фактически удлиняет жизнь токена.

### 5.2 Модуль «Real-time синхронизация очереди» (FR-RT)

#### 5.2.1 Архитектурные принципы
1. **Server-authoritative:** клиент никогда не изменяет локальное состояние «оптимистично без подтверждения». Все изменения — команда → сервер → БД → broadcast.
2. **Команды по REST или Hub, события — только через Hub.** Рекомендация: мутации идут через REST (единая валидация, аудит через Middleware, идемпотентность), Hub используется для push-событий и лёгких команд (vote).
3. **Группы SignalR:** одна группа на комнату — `room:{roomId}`. Членство в группе **не хранится** SignalR между переподключениями — клиент обязан вызвать `JoinRoom` после reconnect.
4. **Версионирование состояния:** у очереди есть монотонный `QueueVersion`. Каждое событие несёт версию; клиент, обнаруживший разрыв (`received != local + 1`), запрашивает полный snapshot.

#### 5.2.2 Контракт Hub (`/hubs/room`)

| Направление | Метод/событие | Payload | Описание |
|---|---|---|---|
| C → S | `JoinRoom(roomId)` | — | Проверка членства в БД, `Groups.AddToGroupAsync`, ответ — `RoomSnapshot` |
| C → S | `LeaveRoom(roomId)` | — | Удаление из группы |
| C → S | `Vote(playlistTrackId, value)` | `+1` / `-1` / `0` (снять) — **целевое** состояние, не toggle | Upsert голоса (`MERGE`), идемпотентно |
| C → S | `VoteSkip()` | — | Голос за пропуск текущего трека (BR-VOTE-07) |
| S → C | `SkipProgress` | `{votes, required}` | Прогресс vote-to-skip |
| S → C | `QueueUpdated` | `{version, items[{id, track, score, rank, addedBy}]}` | Полный отсортированный список (очередь небольшая — проще и надёжнее диффов, KISS) |
| S → C | `PlaybackChanged` | `{playlistTrackId, startedAtUtc, serverTimeUtc}` | Смена трека |
| S → C | `MemberJoined` / `MemberLeft` | `{userId, username}` | Presence |
| S → C | `RoomClosed` | `{reason}` | Комната закрыта |
| S → C | `Error` | `{code, message}` | Ошибки бизнес-валидации |

| ID | Требование | Приоритет |
|---|---|:-:|
| FR-RT-01 | Изменение очереди (add / remove / vote / skip) доставляется всем участникам комнаты, p95 ≤ 300 мс (локальная сеть/Docker). | M |
| FR-RT-02 | Broadcast выполняется **только после успешного COMMIT** транзакции. Никогда не рассылать незафиксированное состояние. | M |
| FR-RT-03 | Сортировка очереди: `is_pinned DESC, pinned_at ASC, score DESC, added_at ASC`. Ранг вычисляется в Oracle через `ROW_NUMBER() OVER (PARTITION BY playlist_id ORDER BY ...)` (порядок воспроизведения должен быть строгим, без «ничьих»); `RANK()` — для рейтингов статистики. | M |
| FR-RT-04 | Синхронизация воспроизведения: сервер хранит `PLAYBACK_STARTED_AT` (UTC). Клиент вычисляет позицию `now_server − startedAt`, корректируя часы по `serverTimeUtc` в событии. | M |
| FR-RT-05 | Автопереход к следующему треку выполняет `BackgroundService` (UC-16) с защитой от двойного срабатывания (условный UPDATE: `WHERE CURRENT_PLAYLIST_TRACK_ID = :expected`). | M |
| FR-RT-06 | После reconnect клиент получает актуальный snapshot (`JoinRoom` идемпотентен). | M |
| FR-RT-07 | Один голос на пользователя на позицию — обеспечивается **UNIQUE-ограничением в БД**, а не только проверкой в коде (защита от race condition двух параллельных запросов). | M |
| FR-RT-08 | Ограничения против спама: ≤ N треков от одного пользователя в очереди одновременно; rate limit на Hub-методы (через `IHubFilter`). | S |
| FR-RT-09 | Горизонтальное масштабирование: архитектура допускает подключение Redis backplane (`AddStackExchangeRedis`) без изменения кода Hub. | C |

> **Internals SignalR:** `IHubContext<RoomHub>` позволяет отправлять события из Application/Infrastructure-сервисов вне Hub. Чтобы Application-слой не зависел от SignalR (Clean Architecture!), вводим абстракцию `IRoomNotifier` в Application и реализацию в WebAPI/Infrastructure. Экземпляр Hub **transient** — создаётся на каждый вызов метода, поэтому хранить состояние в полях Hub нельзя.

### 5.3 Модуль «Сквозное журналирование (Audit Trail)» (FR-AUD)

#### 5.3.1 Двухуровневая модель аудита

| Уровень | Механизм | Что фиксирует |
|---|---|---|
| **HTTP-уровень** | Custom `AuditMiddleware` | Кто, что, когда, откуда, результат, длительность |
| **Hub-уровень** | `IHubFilter` | Вызовы методов SignalR (Middleware **не видит** отдельные сообщения внутри WebSocket — только сам upgrade-запрос!) |
| **Доменный уровень** *(S)* | EF Core `SaveChangesInterceptor` | Какие сущности изменены, old/new значения |

#### 5.3.2 Конвейер Middleware

```
Request → ExceptionHandler → CorrelationId → Authentication → Authorization
        → AuditMiddleware → RateLimiter → Endpoint
```
`AuditMiddleware` стоит **после** `UseAuthentication/UseAuthorization`, иначе `HttpContext.User` ещё пуст. Для фиксации исходов исключений он оборачивает `await _next(context)` в `try/finally`.

#### 5.3.3 Асинхронная запись

```
AuditMiddleware ──TryWrite──► Channel<AuditEntry> (bounded, 10 000)
                                     │
                     AuditWriterService : BackgroundService
                     читает пачками (≤ 100 / 2 с) → свой DbContext scope
                     → batch INSERT в AUDIT_LOGS
```

| ID | Требование | Приоритет |
|---|---|:-:|
| FR-AUD-01 | Логируются все **изменяющие** запросы (POST/PUT/PATCH/DELETE), все попытки аутентификации (успешные и неуспешные), все ответы 401/403. GET — не логируются (шум), кроме админских эндпоинтов. | M |
| FR-AUD-02 | Поля записи: UserId, Action, EntityType, EntityId, HttpMethod, Path (без query string), StatusCode, IpAddress, UserAgent, CorrelationId, DurationMs, CreatedAt (UTC), Details (JSON). | M |
| FR-AUD-03 | **Запрещено** логировать: пароли, токены, заголовок `Authorization`, cookie. Тела запросов — только по whitelist эндпоинтов с маскированием. | M |
| FR-AUD-04 | Запись аудита не блокирует ответ клиенту (Channel + BackgroundService). При переполнении канала — запись в fallback-лог (Serilog) и метрика `audit_dropped_total`. | M |
| FR-AUD-05 | `AUDIT_LOGS` — append-only: у прикладного пользователя БД нет прав UPDATE/DELETE на таблицу (GRANT только INSERT, SELECT). | S |
| FR-AUD-06 | `CorrelationId` (заголовок `X-Correlation-Id` или генерируемый) прокидывается в Serilog `LogContext` и в ответ — сквозная трассировка запроса. | M |
| FR-AUD-07 | Admin API: фильтрация по пользователю/действию/периоду, keyset-пагинация (`WHERE ID < :lastId FETCH FIRST n ROWS ONLY`), т.к. OFFSET на больших таблицах деградирует. | M |
| FR-AUD-08 | Индексы: `(CREATED_AT)`, `(USER_ID, CREATED_AT)`; эффективность подтверждается `EXPLAIN PLAN` (Этап 7). | S |
| FR-AUD-09 | Политика хранения: 90 дней «горячих» данных; при наличии Partitioning — `INTERVAL`-партиционирование по месяцам и удаление старых партиций `DROP PARTITION` вместо `DELETE`. | C |

> **Почему не писать аудит в той же транзакции, что и бизнес-операция?** Middleware не знает о транзакции Unit of Work, а запись прямо в request pipeline добавляет латентность к каждому запросу. Компромисс: HTTP-аудит — асинхронный (допускается потеря при аварийном падении процесса), доменный аудит через `SaveChangesInterceptor` — **в той же транзакции** (гарантия ACID для критичных изменений).

### 5.4 Прочие модули (кратко)

| ID | Требование | Приоритет |
|---|---|:-:|
| FR-ROOM-01 | Создание комнаты атомарно создаёт Room + Playlist + RoomMember(Owner). | M |
| FR-ROOM-02 | Приватная комната доступна по инвайт-коду (BR-ROOM-02). | M |
| FR-INT-01 | Интеграция с YouTube Data API v3: поиск (`search.list`) и метаданные/длительность (`videos.list`). Абстракция `ITrackSourceClient` в Application, реализация — typed `HttpClient` в Infrastructure с resilience-пайплайном (retry, timeout, circuit breaker — `Microsoft.Extensions.Http.Resilience`). | M |
| FR-INT-02 | Сначала поиск по локальному `TRACKS`, внешний API — только при недостатке результатов; найденные треки сохраняются в каталог (кэш). Причина — квота YouTube API (см. BR-TRK-03). | M |
| FR-ROOM-03 | Список публичных активных комнат с числом участников (агрегат, не хранимое поле). | M |
| FR-CAT-01 | Поиск треков по названию/исполнителю (регистронезависимый: `UPPER(TITLE) LIKE UPPER(:q) \|\| '%'` + функциональный индекс). | M |
| FR-STAT-01 | Топ-N треков по суммарным голосам за период с использованием `RANK()` / `DENSE_RANK()`. | S |
| FR-STAT-02 | Топ «диджеев» (пользователи, чьи треки набрали больше всего голосов) — `ROW_NUMBER() OVER (PARTITION BY room ...)`. | C |

---

## 6. Нефункциональные требования

### 6.1 Безопасность (NFR-SEC)

| ID | Требование |
|---|---|
| NFR-SEC-01 | Весь трафик — HTTPS (в Docker — через reverse proxy или Kestrel с сертификатом dev); HSTS в prod. |
| NFR-SEC-02 | Секреты (JWT signing key, строка подключения) — только из переменных окружения / user-secrets / `.env` (не коммитится). В репозитории — `.env.example`. |
| NFR-SEC-03 | SQL-инъекции исключены: только параметризованные запросы (EF Core LINQ, `FromSqlInterpolated`; **запрещён** `FromSqlRaw` с конкатенацией). |
| NFR-SEC-04 | Валидация всех входных DTO (FluentValidation) до попадания в Application-логику. |
| NFR-SEC-05 | Ошибки — в формате `ProblemDetails` (RFC 9457) без stack trace в prod. |
| NFR-SEC-06 | CORS — явный whitelist origin-ов фронтенда; `AllowCredentials` только для них (нужно для cookie RT и SignalR). |
| NFR-SEC-07 | Принцип наименьших привилегий в Oracle: приложение подключается под пользователем `SOUNDSHARE_APP`, а не под владельцем схемы; DDL выполняется отдельным пользователем миграций. |
| NFR-SEC-08 | Защита от IDOR: любой доступ к ресурсу комнаты проверяет членство пользователя, а не только факт аутентификации. |

### 6.2 Целостность данных и транзакции (NFR-TX, ACID)

| Свойство | Как обеспечивается в SoundShare + Oracle |
|---|---|
| **Atomicity** | Unit of Work = один `DbContext.SaveChangesAsync()` = одна транзакция. Многошаговые сценарии (UC-05, ротация RT) — явная `BeginTransactionAsync` + `ExecutionStrategy`. Oracle откатывает транзакцию по undo-сегментам. |
| **Consistency** | Ограничения на уровне БД, а не только кода: PK, FK, UNIQUE (`(PLAYLIST_TRACK_ID, USER_ID)`), `CHECK (VALUE IN (-1, 1))`, `CHECK (IS_ACTIVE IN (0,1))`. |
| **Isolation** | Oracle по умолчанию — `READ COMMITTED` с **multiversion read consistency**: читатели не блокируют писателей и наоборот, грязное чтение невозможно в принципе. Для конкурентных обновлений (смена текущего трека) — оптимистическая блокировка: колонка `VERSION NUMBER` как concurrency token (в Oracle нет `rowversion`, как в SQL Server). |
| **Durability** | COMMIT возвращается после записи redo в online redo log (LGWR). Docker volume для `/opt/oracle/oradata` обязателен. |

| ID | Требование |
|---|---|
| NFR-TX-01 | Транзакции короткие: никаких внешних HTTP-вызовов и SignalR-рассылок внутри открытой транзакции. |
| NFR-TX-02 | При `DbUpdateConcurrencyException` — до 3 повторов с перечитыванием состояния, затем `409 Conflict`. |
| NFR-TX-03 | Транзиентные ошибки соединения обрабатываются `ExecutionStrategy` с retry. |
| NFR-TX-04 | Удаление — мягкое (`IS_ACTIVE`/`STATUS`) для Users, Rooms; физическое — только для голосов. |

### 6.3 Производительность и масштабируемость (NFR-PERF / NFR-SCALE)

| ID | Требование |
|---|---|
| NFR-PERF-01 | REST p95 ≤ 200 мс при 100 RPS на эталонном стенде (Docker, 4 vCPU). |
| NFR-PERF-02 | Поддержка ≥ 500 одновременных WebSocket-соединений на один инстанс API. |
| NFR-PERF-03 | Отсутствие N+1: явные `Include`/проекции `Select` в DTO; read-запросы — `AsNoTracking()`. |
| NFR-PERF-04 | Все FK-колонки проиндексированы (Oracle **не** создаёт индексы на FK автоматически — без них DELETE/UPDATE ключа в родительской таблице вызывает блокировку всей дочерней таблицы). |
| NFR-SCALE-01 | API stateless (кроме SignalR-соединений) → горизонтальное масштабирование за балансировщиком со sticky sessions или Redis backplane. |
| NFR-SCALE-02 | Пул соединений ODP.NET настраивается явно (`Min Pool Size`, `Max Pool Size`), DbContext — scoped. |

### 6.4 Надёжность и наблюдаемость (NFR-REL / NFR-OBS)

| ID | Требование |
|---|---|
| NFR-REL-01 | Health checks: `/health/live` (процесс жив), `/health/ready` (Oracle доступен). Используются в `docker compose` (`depends_on: condition: service_healthy`). |
| NFR-REL-02 | Graceful shutdown: `BackgroundService` дописывает буфер аудита при `StopAsync`. |
| NFR-REL-03 | SignalR-клиент использует `withAutomaticReconnect()` и восстанавливает состояние через snapshot. |
| NFR-OBS-01 | Структурное логирование Serilog (JSON) с CorrelationId, UserId. |
| NFR-OBS-02 | *(C)* OpenTelemetry трейсинг (ASP.NET Core + EF Core). |

### 6.5 Сопровождаемость и качество кода (NFR-MAINT)

| ID | Требование |
|---|---|
| NFR-MAINT-01 | Clean Architecture: зависимости направлены внутрь (`WebAPI → Infrastructure → Application → Domain`; Domain не ссылается ни на что). Проверяется архитектурными тестами (NetArchTest / ArchUnitNET). |
| NFR-MAINT-02 | `Nullable` enable, `TreatWarningsAsErrors` для Domain/Application, `.editorconfig` + анализаторы. |
| NFR-MAINT-03 | Тесты: unit (xUnit + FluentAssertions/Shouldly + NSubstitute), интеграционные — Testcontainers с образом `gvenzl/oracle-free`. |
| NFR-MAINT-04 | Git: GitHub Flow, ветки `feature/*`, `docs/*`, `fix/*`, PR с минимум 1 ревью, Conventional Commits. CI (GitHub Actions): build + test. |
| NFR-MAINT-05 | API документируется OpenAPI (встроенный `Microsoft.AspNetCore.OpenApi` + Scalar UI). |

---

## 7. Ограничения и соглашения по платформе

### 7.1 Технологический стек

| Компонент | Выбор | Примечание архитектора |
|---|---|---|
| Runtime | .NET 11 | ⚠️ .NET 11 — STS-релиз. **Риск:** `Oracle.EntityFrameworkCore` исторически выходит с задержкой относительно новых EF Core. Действие на Этапе 4: проверить наличие версии провайдера под EF Core 11; fallback — `net10.0` (LTS). |
| СУБД | Oracle Database 23ai Free (Docker `gvenzl/oracle-free`) | Бесплатна, легко поднимается в compose. |
| ORM | EF Core + Oracle.EntityFrameworkCore | Code-First миграции + ручной SQL для аналитики. |
| Real-time | ASP.NET Core SignalR | Транспорт: WebSockets → SSE → Long Polling (автоматический fallback). |

### 7.2 Соглашения Oracle (обязательны для всей команды)

| # | Соглашение | Обоснование |
|---|---|---|
| ORA-1 | Все идентификаторы — `UPPER_SNAKE_CASE` (`PLAYLIST_TRACKS.ADDED_AT`). В EF — глобальная конвенция именования (напр., `EFCore.NamingConventions` → `UseUpperSnakeCaseNamingConvention()`), а не ручные `ToTable` в каждой конфигурации. | Провайдер EF **заключает имена в кавычки**: без конвенции получится `"PlaylistTracks"` — регистрозависимое имя, к которому в SQL придётся обращаться только в кавычках. Это главная ловушка Oracle + EF. |
| ORA-2 | `bool` ↔ `NUMBER(1)` + `CHECK (COL IN (0,1))`. | Классические версии Oracle не имеют SQL-типа BOOLEAN. В 23ai он появился, но мы фиксируем `NUMBER(1)` для переносимости и предсказуемого маппинга провайдера. |
| ORA-3 | Суррогатные PK: `NUMBER(19) GENERATED ALWAYS AS IDENTITY` ↔ `long` в C#. | `ALWAYS` запрещает ручную вставку ID (защита от рассинхронизации). Под капотом Oracle создаёт системную последовательность `ISEQ$$_…`. Минус — seed-данные нельзя вставить с заданным ID: учтём на Этапе 3. |
| ORA-4 | Время — `TIMESTAMP` в UTC (или `TIMESTAMP WITH TIME ZONE`), в C# — `DateTimeOffset`/`DateTime` UTC. Тип `DATE` не используется для событий. | `DATE` в Oracle хранит время до секунд — недостаточно для упорядочивания голосов. |
| ORA-5 | Строки — `NVARCHAR2`/`VARCHAR2(n CHAR)` (семантика символов, не байт). JSON — тип `JSON` (21c+) или `CLOB` с `CHECK (... IS JSON)`. | Названия треков содержат Unicode (кириллица, иероглифы, emoji). |
| ORA-6 | Длина идентификаторов ≤ 30 символов для совместимости (лимит 128 действует с 12.2). Имена ограничений: `PK_<TABLE>`, `FK_<CHILD>_<PARENT>`, `UQ_<TABLE>_<COLS>`, `CK_<TABLE>_<COL>`, `IX_<TABLE>_<COLS>`. | Читаемость ошибок `ORA-00001: unique constraint (SOUNDSHARE.UQ_TRACK_VOTES_PT_USER) violated` — сразу понятно, что случилось. |
| ORA-7 | Рейтинги — аналитические функции (`RANK`, `DENSE_RANK`, `ROW_NUMBER`), агрегация на стороне БД. | Не тянуть все голоса в память приложения. |
| ORA-8 | Каждый новый индекс обосновывается `EXPLAIN PLAN FOR ...` + `DBMS_XPLAN.DISPLAY` (до/после). | Требование ЛР и хорошая инженерная практика. |
| ORA-9 | Пагинация — `OFFSET … FETCH NEXT … ROWS ONLY` (12c+) или keyset. | EF Oracle-провайдер генерирует именно этот синтаксис. |

---

## 8. Требования к составу и содержанию работ (этапы)

| Этап | Результат |
|---|---|
| 1 | Утверждённое ТЗ (этот документ v1.0) |
| 2 | Инфологическая (ER) и даталогическая модели, нормализация |
| 3 | DDL-скрипты Oracle, ограничения, индексы, seed-данные |
| 4 | DML-запросы с описанием; Domain + Infrastructure (EF Core mapping, Repository/UoW) |
| 5 | SELECT-запросы с описанием; Application-слой (CQRS-запросы, DTO, маппинг) |
| 6 | Запросы журналирования, макеты интерфейсов; WebAPI, JWT, SignalR Hub, Audit Middleware, клиент |
| 7 | Приложение, тестовые данные, отчёт об индексах (EXPLAIN PLAN), тесты и покрытие |
| 8 | README, руководство пользователя, Docker Compose, CI, защита |

Полная матрица соответствия критериям курсов и темам собеседования — [00-roadmap.md](00-roadmap.md).

## 9. Порядок контроля и приёмки

- Каждое требование `Must` покрывается минимум одним автотестом или демонстрационным сценарием (матрица трассируемости FR → тест — к Этапу 7).
- Приёмка этапа: PR в `main` с артефактами этапа + ревью тимлида.
- Финальная приёмка: `docker compose up` на чистой машине → прохождение сценариев UC-01…UC-16 по чек-листу.

---

## 10. Распределение задач Этапа 1 (Git)

> Предполагается команда из 3–4 человек (A, B, C, D). Если вас меньше — объедините роли B+D.

| Участник | Зона ответственности | Ветка | Задачи |
|---|---|---|---|
| **A — Tech Lead** | Репозиторий, процессы | `chore/repo-bootstrap` | Создать репо, `README.md`, `.gitignore` (VisualStudio), `.editorconfig`, шаблон PR, branch protection на `main`, папка `docs/`. Финальная сборка ТЗ v1.0. |
| **B — Domain/DB** | Разделы 4, 7.2 | `docs/tz-information-model` | Финализировать перечень сущностей и атрибутов по ответам на вопросы; подготовить черновик ER для Этапа 2. |
| **C — Security/Backend** | Разделы 3, 5.1, 6.1 | `docs/tz-auth-roles` | Финализировать ролевую модель и матрицу доступа, сценарий reuse detection. |
| **D — Realtime/Infra** | Разделы 5.2, 5.3, 6.3–6.4 | `docs/tz-realtime-audit` | Финализировать контракт Hub, список аудируемых действий, оценить .NET 11 vs провайдер Oracle. |

**Definition of Done Этапа 1:** все разделы без пометок `TBD`, ответы на 3 вопроса раздела 12 внесены, версия документа — 1.0, PR смержен.

---

## 11. История изменений

| Версия | Дата | Автор | Изменения |
|---|---|---|---|
| 0.9 | 2026-10-03 | Architect | Первоначальный черновик |
| 1.0-rc | 2026-10-03 | Architect + Team | Зафиксированы бизнес-правила (раздел 12), добавлены `TRACK_ARTISTS`, `SKIP_VOTES`, роль Contributor, интеграция YouTube |
| 1.0 | 2026-10-03 | Team | Приняты все поправки ⚑; пауза исключена из v1; pin доступен Moderator |

---

## 12. Бизнес-правила (утверждены командой)

> ⚑ — поправка архитектора к ответу команды, **принята** командой 2026-10-03.

### 12.1 Голосование и очередь (BR-VOTE)

| ID | Правило |
|---|---|
| BR-VOTE-01 | Шкала `+1` / `−1`. Рейтинг позиции = `SUM(VALUE)`, может быть отрицательным. Хранимого счётчика нет. |
| BR-VOTE-02 | Голос можно сменить (`UPDATE`) или снять (`DELETE`). ⚑ Клиент передаёт **целевое значение** (`+1/−1/0`), а не «toggle»: логику «повторный лайк = снять» считает UI. Причина — идемпотентность: при сетевом повторе запроса toggle перевернёт голос дважды. |
| BR-VOTE-03 | Реализация — одним оператором Oracle `MERGE ... WHEN MATCHED THEN UPDATE ... DELETE WHERE (:value = 0) WHEN NOT MATCHED THEN INSERT ... WHERE (:value <> 0)`. Конкурентный первый голос одного пользователя (две вкладки) даёт `ORA-00001` → повтор операции. |
| BR-VOTE-04 | Добавивший трек автоматически ставит ему `+1`; вставка `PLAYLIST_TRACKS` + `TRACK_VOTES` — **одна транзакция**. |
| BR-VOTE-05 | Автоудаление: при рейтинге `≤ −5` позиция получает статус `AutoRemoved` и пропадает из очереди. Закреплённые позиции и текущий играющий трек автоудалению не подлежат. Порог — настройка (`QueueOptions.AutoRemoveThreshold`). |
| BR-VOTE-06 | ⚑ Проверка порога выполняется в Domain/Application-логике, **не триггером БД** (обоснование — 12.4). |
| BR-VOTE-07 | Vote-to-skip: трек переключается, когда число skip-голосов **строго больше 50 % участников онлайн** (уникальных пользователей с активным SignalR-соединением в комнате). ⚑ Именно онлайн, а не всех членов: иначе ушедшие без «выхода» участники сделают skip недостижимым. |
| BR-VOTE-08 | Закрепление (pin) — Owner и Moderator (Owner задаёт правила комнаты, Moderator управляет текущей очередью). Закреплённые идут первыми в порядке `PINNED_AT ASC`, далее — по рейтингу, затем по `ADDED_AT`. |
| BR-VOTE-09 | ⚑ Конкурентность: голосование выполняется в `READ COMMITTED` с блокировкой строки позиции `SELECT ... FROM PLAYLIST_TRACKS WHERE ID = :id FOR UPDATE` перед `MERGE` (обоснование — 12.4). |

### 12.2 Треки и воспроизведение (BR-TRK)

| ID | Правило |
|---|---|
| BR-TRK-01 | Источник — YouTube (Data API v3 для метаданных, IFrame Player API на клиенте для звука). В БД — `EXTERNAL_SOURCE`, `EXTERNAL_SOURCE_ID`, метаданные. Аудио не скачивается и не проксируется (условия использования YouTube). |
| BR-TRK-02 | Трек может иметь несколько исполнителей → `TRACK_ARTISTS` (M:N). ⚑ YouTube не отдаёт структурированных исполнителей: они извлекаются эвристикой из названия (`Artist1 feat. Artist2 - Title`) / имени канала, Admin может исправить. |
| BR-TRK-03 | ⚑ Квота YouTube Data API — 10 000 единиц/сутки, `search.list` стоит 100 единиц ⇒ **~100 поисков в сутки на ключ**. Поэтому: локальный каталог первым (FR-INT-02), debounce поиска на клиенте, rate limit на эндпоинт поиска. |
| BR-TRK-04 | ⚑ Жанр в v1 не хранится: YouTube не предоставляет жанр видео. Если понадобится — отдельный справочник `GENRES` (3NF), не строковая колонка. |
| BR-TRK-05 | «Виртуальный таймер»: сервер хранит `PLAYBACK_STARTED_AT`; позиция = `serverNow − startedAt`. В событии передаётся `serverTimeUtc` для компенсации расхождения часов клиента. Пауза в v1 не поддерживается: непрерывное вещание (модель «радио»), все участники слышат один и тот же момент трека. |
| BR-TRK-06 | Автопереход выполняет `BackgroundService` по `PLAYBACK_STARTED_AT + DURATION_SEC`. |

### 12.3 Комнаты (BR-ROOM)

| ID | Правило |
|---|---|
| BR-ROOM-01 | Публичные комнаты видны в поиске; приватные — только по инвайт-коду. |
| BR-ROOM-02 | ⚑ Инвайт-код — 6 символов **алфавитно-цифровых** (алфавит без `0/O/1/I`, 32 символа ⇒ ~10⁹ комбинаций), а не 6 цифр (10⁶ — перебирается за минуты). Плюс rate limit на вход по коду. Уникальность — `UNIQUE` на `INVITE_CODE` (у публичных `NULL`; Oracle не индексирует полностью NULL-ключи, поэтому множество `NULL` не конфликтует). |
| BR-ROOM-03 | Максимум 100 активных участников в комнате. |
| BR-ROOM-04 | Не более 3 позиций со статусом `Queued`/`Playing` от одного пользователя в очереди комнаты. |
| BR-ROOM-05 | Явный выход Owner (или переход Owner в другую комнату, см. BR-ROOM-06) ⇒ владение переходит ⚑ сначала самому раннему по `JOINED_AT` **Moderator**, при их отсутствии — самому раннему **Contributor**. Нет кандидатов — комната пустеет (BR-ROOM-07). |
| BR-ROOM-06 | Пользователь — активный участник не более одной комнаты. ⚑ Не через `USERS.CURRENT_ROOM_ID` (дублирует членство — нарушение 3NF и два источника истины), а через `ROOM_MEMBERS.LEFT_AT` + функциональный уникальный индекс: `CREATE UNIQUE INDEX UX_RM_ACTIVE_USER ON ROOM_MEMBERS (CASE WHEN LEFT_AT IS NULL THEN USER_ID END);` Вход в новую комнату в той же транзакции закрывает предыдущее членство. |
| BR-ROOM-07 | ⚑ «Пустая комната» = 0 онлайн-соединений (обрыв связи ≠ выход). При уходе последнего соединения ставится `ROOMS.EMPTY_SINCE`; при входе — сбрасывается. `BackgroundService` с `PeriodicTimer` раз в минуту выполняет `UPDATE ROOMS SET IS_ACTIVE = 0 WHERE IS_ACTIVE = 1 AND EMPTY_SINCE < SYSTIMESTAMP - INTERVAL '10' MINUTE`. Без `Task.Delay(10 мин)` на каждую комнату: такие таймеры теряются при рестарте процесса. |

### 12.4 Обоснование решений по конкурентности (для защиты)

1. **Почему 20 одновременных лайков — не проблема.** Каждый голос — **отдельная строка** `TRACK_VOTES`, писатели не конкурируют за одну строку. Будь рейтинг счётчиком `SCORE = SCORE + 1`, все 20 транзакций выстроились бы в очередь на блокировку одной строки (а при чтении-изменении-записи в приложении — lost update). Нормализация = масштабируемость.
2. **Где гонка реально есть — проверка порогов.** Рейтинг −3, двое одновременно ставят −1. В `READ COMMITTED` каждая транзакция видит только свой незакоммиченный голос ⇒ обе насчитают −4, порог −5 не сработает ни у одной, итог −5 без удаления. Аналогично для vote-to-skip и лимитов «3 трека» и «100 участников» (`COUNT` → `INSERT`: классическая аномалия *write skew*).
3. **Решение — точечная пессимистическая блокировка** родительской строки (`SELECT ... FOR UPDATE` на `PLAYLIST_TRACKS` / `ROOMS` / `ROOM_MEMBERS`): операции над одной позицией/комнатой выполняются последовательно, над разными — параллельно. Блокировка держится миллисекунды.
4. **Почему не `SERIALIZABLE`.** В Oracle он реализован через снимки (snapshot isolation) и **не** предотвращает write skew, зато при конфликте записи бросает `ORA-08177: can't serialize access`, требуя повторов. Это отличный пункт для сравнения на защите: показать, что `SERIALIZABLE` в Oracle ≠ сериализуемость из теории.
5. **Почему не триггер для автоудаления.** (а) Row-level триггер на `TRACK_VOTES`, считающий `SUM` по той же таблице, падает с `ORA-04091: table is mutating`; (б) приложение не узнает об удалении и не отправит SignalR-событие; (в) бизнес-правило прячется от Domain-слоя и юнит-тестов.

### 12.5 Архитектурное замечание

⚑ Остаёмся **модульным монолитом** в Clean Architecture. Микросервисы для команды из 3–4 человек и одной БД — это распределённые транзакции и сетевые сбои без выигрыша. Фоновые службы (`BackgroundService`) живут в том же процессе WebAPI. При необходимости их можно вынести в отдельный Worker-процесс без изменения Application-слоя.
