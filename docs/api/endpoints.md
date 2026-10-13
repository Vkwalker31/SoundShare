# Контракт REST API

Базовый путь — `/api`. Формат — JSON. Ошибки — `ProblemDetails` (RFC 9457) с полями `type`, `title`, `status`, `detail`, `traceId`; ошибки валидации дополнительно содержат `errors` по полям. Реализация в WebAPI обязана соответствовать этому документу; расхождения регистрируются в [ISSUES.md](../../ISSUES.md).

## 1. Общие правила

| Ситуация | Код |
|---|---|
| Нет токена, токен недействителен или истёк | `401 Unauthorized` |
| Нет прав на операцию (роль) | `403 Forbidden` |
| Ресурс не существует **или** пользователь не участник приватной комнаты | `404 Not Found` — существование приватной комнаты не раскрывается |
| Ошибка валидации входных данных | `400 Bad Request` |
| Нарушение бизнес-правила (занятое имя, лимит, последний администратор, комната заполнена) | `409 Conflict` |
| `If-Match` не совпадает с текущей версией очереди | `412 Precondition Failed` |
| Превышен лимит частоты запросов | `429 Too Many Requests` + `Retry-After` |
| БД недоступна | `503 Service Unavailable` |
| Прочие непредвиденные ошибки | `500 Internal Server Error` без деталей реализации |

Все изменяющие запросы принимают и возвращают `X-Correlation-Id`. Ответ `201 Created` содержит заголовок `Location`.

## 2. Аутентификация

| Метод и путь | Тело запроса | Успех | Ошибки |
|---|---|---|---|
| `POST /api/auth/register` | `username`, `email`, `password` | `201` `{ id, username }` | `400`; `409` имя или email заняты |
| `POST /api/auth/login` | `username`, `password` | `200` `{ accessToken, expiresIn }` + cookie `rt` | `401` неверные данные; `403` аккаунт заблокирован (только при верном пароле); `429` |
| `POST /api/auth/refresh` | — (cookie `rt`) | `200` `{ accessToken, expiresIn }` + новая cookie | `401` |
| `POST /api/auth/logout` | — | `204`, cookie удалена | — |
| `POST /api/auth/logout-all` | — | `204` | `401` |

## 3. Пользователи

| Метод и путь | Тело запроса | Успех | Ошибки |
|---|---|---|---|
| `GET /api/users/me` | — | `200` `{ id, username, email, role }` | `401` |
| `DELETE /api/users/me` | `password` | `204` | `400`; `403` неверный пароль |
| `GET /api/admin/users?role=&isActive=&page=&pageSize=` | — | `200` `{ items, total, page, pageSize }` | `401`; `403` |
| `POST /api/admin/users` | `username`, `email`, `password`, `role` | `201` | `400`; `403`; `409` |
| `PATCH /api/admin/users/{userId}/role` | `role` | `204` | `403`; `404`; `409` последний администратор |
| `PUT /api/admin/users/{userId}/block` | — | `204` (блокировка) | `403`; `404` |
| `DELETE /api/admin/users/{userId}/block` | — | `204` (разблокировка) | `403`; `404` |

## 4. Комнаты и участники

| Метод и путь | Тело запроса | Успех | Ошибки |
|---|---|---|---|
| `GET /api/rooms` | — | `200` публичные активные комнаты `[{ id, name, membersCount }]` | `401` |
| `POST /api/rooms` | `name`, `isPrivate` | `201` `{ id, name, inviteCode }` | `400` |
| `GET /api/rooms/{roomId}` | — | `200` состояние комнаты | `404` |
| `POST /api/rooms/{roomId}/members` | — | `200` состояние комнаты (вход в публичную) | `403` бан (`detail` содержит время окончания); `404`; `409` комната заполнена |
| `POST /api/rooms/join` | `inviteCode` (регистр не важен, приводится к верхнему) | `200` состояние комнаты | `403` бан; `404` неверный код; `409` |
| `DELETE /api/rooms/{roomId}/members/me` | — | `204` | `404` |
| `PATCH /api/rooms/{roomId}/members/{userId}` | `role` | `204` | `403`; `404` |
| `POST /api/rooms/{roomId}/bans` | `userId`, `durationMinutes` (пусто — бессрочно) | `204` | `403` роль инициатора не выше роли участника; `404` |
| `DELETE /api/rooms/{roomId}` | — | `204` | `403`; `404` |

Повторный вход действующего участника не создаёт второе членство и возвращает `200`.

## 5. Очередь и воспроизведение

| Метод и путь | Тело запроса | Успех | Ошибки |
|---|---|---|---|
| `GET /api/rooms/{roomId}/queue` | — | `200` `{ version, playing, items[{ id, track, score, position, addedBy, pinned }] }` + `ETag` | `404` |
| `POST /api/rooms/{roomId}/queue/items` | `trackId` | `201` | `403`; `404`; `409` лимит 3 трека |
| `POST /api/rooms/{roomId}/queue/items/from-playlist` | `playlistId` | `200` `{ added }` | `404` |
| `DELETE /api/rooms/{roomId}/queue/items/{itemId}` | — | `204` | `403`; `404` |
| `PUT /api/rooms/{roomId}/queue/items/{itemId}/vote` | `value`: `1`, `-1`, `0` | `200` `{ score }` | `400`; `404` |
| `PUT /api/rooms/{roomId}/queue/items/{itemId}/pin` | — | `204` | `403`; `404` |
| `DELETE /api/rooms/{roomId}/queue/items/{itemId}/pin` | — | `204` | `403`; `404` |
| `POST /api/rooms/{roomId}/playback/skip-votes` | — | `200` `{ votes, required }` | `404`; `409` ничего не играет |
| `POST /api/rooms/{roomId}/playback/skip` | — | `204` (пропуск модератором) | `403`; `404` |

## 6. Каталог и плейлисты

| Метод и путь | Тело запроса | Успех | Ошибки |
|---|---|---|---|
| `GET /api/tracks?query=` | — | `200` `{ items, externalSearchAvailable }` | `400` пустой запрос |
| `POST /api/playlists` | `name`, `fromRoomId` (необязательно) | `201` | `400`; `404` |
| `GET /api/playlists/{playlistId}` | — | `200` | `404` |

При недоступности YouTube API поиск возвращает `200` с локальными результатами и `externalSearchAvailable = false`.

## 7. Журнал аудита

| Метод и путь | Успех | Ошибки |
|---|---|---|
| `GET /api/admin/audit-logs?userId=&action=&from=&to=&pageSize=&cursor=` | `200` `{ items, nextCursor }`, записи по убыванию ID | `401`; `403` |

## 8. Real-time и служебные адреса

| Адрес | Назначение |
|---|---|
| `/hubs/room?access_token=` | SignalR Hub; контракт сообщений — ТЗ §5.3 |
| `/health/live`, `/health/ready` | Проверки состояния (`ready` → `503` при недоступной БД) |
| `/openapi/v1.json`, `/scalar` | Описание API |
