# Автоматизированные UI-тесты (задание 4)

Проект: `tests/SoundShare.UiTests` — **Playwright for .NET** + xUnit.

## 1. Требования к тестам и как они выполнены

| Требование | Реализация |
|---|---|
| Page Object | `Pages/LoginPage`, `RoomsPage`, `RoomPage`, `AdminUsersPage` — тесты не обращаются к селекторам напрямую. Селекторы — атрибуты `data-testid`, не зависящие от вёрстки и текста |
| Свои данные на тест | Каждый тест регистрирует новых пользователей через API (`UiFixture.CreateAccountAsync`) и создаёт свои комнаты; демо-данные используются только для чтения |
| Явные ожидания | Только `Expect(locator).ToBeVisibleAsync / ToHaveTextAsync / ToHaveCountAsync / ToHaveURLAsync` с автоожиданием; `Thread.Sleep` и `Task.Delay` не используются. Таймаут ожиданий 15 с (первая загрузка WebAssembly в Firefox) |
| Скриншот при падении | `UiTestBase.RunAsync` при исключении сохраняет полный скриншот страницы и trace Playwright (`TestResults/ui/<браузер>-<тест>.png` и `.zip`) |
| Изоляция | Отдельный `BrowserContext` (свои cookie и localStorage) на тест; второй пользователь в тесте реального времени — отдельный контекст |

## 2. Сценарии

| Тест | Что проверяет |
|---|---|
| `Anonymous_OpensRooms_RedirectedToLogin` | Защищённая страница без входа → `/login` |
| `Login_WrongPassword_ShowsErrorAndStaysOnLogin` | Сообщение об ошибке входа |
| `Register_NewAccount_LandsOnRoomsAsThatUser` | Регистрация и автоматический вход |
| `CreateRoom_AddTrack_TrackAppearsAndPlays` | Создание комнаты, роль владельца, первый трек начинает играть |
| `Queue_SecondTrack_ShowsAuthorUpvoteAndDownvoteChangesScore` | Голос автора +1, голос против меняет рейтинг |
| `FourthTrack_ShowsLimitError` | Лимит 3 трека, сообщение об ошибке, очередь не изменилась |
| `RealTime_TrackAddedByOwner_AppearsForGuestWithoutReload` | Два пользователя: вход в приватную комнату по коду, трек владельца появляется у гостя без перезагрузки за ≤ 3 с |
| `PrivateRoom_WrongInviteCode_ShowsNotFound` | Неверный код приглашения |
| `RegularUser_OpensAdminPage_SeesForbiddenMessage` | Раздел администрирования недоступен обычному пользователю |
| `Logout_ReturnsToLoginAndProtectsRooms` | Выход и повторная защита страниц |
| `MobileViewport_RoomPage_HasNoHorizontalScroll` | 375 × 812, длинное название комнаты, нет горизонтальной прокрутки |
| `CompatibilityTests` | 7 страниц × 6 ширин, см. [ui-compat.md](ui-compat.md) |

## 3. Результаты (20.10.2026)

| Браузер | Пройдено |
|---|---:|
| Google Chrome | 12/12 |
| Microsoft Edge | 12/12 |
| Firefox 153 | 12/12 |

При первом запуске в Chrome упал тест реального времени из-за ошибки в самом тесте: после нового поиска он нажимал «Добавить» у ещё не обновлённого результата предыдущего поиска. Page Object теперь ждёт результат с искомым названием; после исправления все тесты проходят во всех браузерах.

## 4. Запуск

Нужен запущенный API (`http://127.0.0.1:5290`, переменная `SOUNDSHARE_UI_URL`) на демо-данных. Браузер выбирается переменной `UI_BROWSER`: `chrome`, `msedge` (установленные браузеры) или `firefox` (один раз: `pwsh tests/SoundShare.UiTests/bin/Debug/net10.0/playwright.ps1 install firefox`).

```bash
dotnet test tests/SoundShare.UiTests
```
