# Матрица трассируемости

Связь требований [ТЗ](../01-technical-specification.md) с тест-кейсами ([test-cases.md](test-cases.md), результаты — [test-run-2026-10-20.md](test-run-2026-10-20.md)), автотестами ([autotests.md](autotests.md)) и дефектами ([ISSUES.md](../../ISSUES.md)). Таблица собрана из матрицы верификации, сводной таблицы тест-кейсов и сводной таблицы дефектов.

Статус требования: **Проверено** — есть пройденный тест-кейс или автотест; **Есть открытый дефект** — связанный тест-кейс провален; **Не проверено** — тест-кейсы заблокированы и автотестов нет; **Не покрыто** — тестов нет.

## 1. Сводка

| Показатель | Значение |
|---|---:|
| Требований всего (FR, NFR, BR) | 94 |
| Покрыто тест-кейсами | 50 (53 %) |
| Покрыто автотестами | 90 (95 %) |
| Требований приоритета M с тест-кейсами | 32 / 32 |
| Проверено | 86 |
| С открытым дефектом | 4 |
| Не проверено | 0 |
| Не покрыто тестами | 4 |

## 2. Требования без тестов

- FR-STAT-01
- FR-STAT-02
- NFR-SCALE-01
- NFR-SCALE-02

Причины: требования приоритета C (вне области текущего релиза) и требования, проверяемые инспекцией или анализом (раздел 1 матрицы верификации), а не тестом.

## 3. Тесты без требований

| Тест | Почему нет требования |
|---|---|
| Postman `08 Security / Повышение прав через тело запроса игнорируется` | Исследовательская проверка mass assignment, отдельного требования нет — кандидат в NFR-SEC |
| Postman `08 Security / Несуществующий эндпоинт API` | Общее поведение API (404), требования нет |
| `SecurityScenarioTests.UnknownRoute_Returns404` | То же |
| `YouTubeTrackSourceTests.Search_SlowResponse_TimesOut` | Таймаут внешнего API (5 с) задан в коде, но не в требованиях — кандидат в NFR-REL |
| `UiScenarioTests.MobileViewport_RoomPage_HasNoHorizontalScroll` | Адаптивность проверяется заданием 4, требования к ширине экрана нет — кандидат в NFR-UX |

## 4. Зоны риска по числу дефектов

| Группа требований | Дефектов | Риск |
|---|---:|---|
| Документация | 5 | Высокий |
| FR-AUD | 5 | Высокий |
| BR-ROOM | 4 | Высокий |
| FR-USR | 4 | Высокий |
| BR-VOTE | 4 | Высокий |
| NFR-SEC | 3 | Средний |
| NFR-REL | 2 | Средний |
| FR-ROOM | 2 | Средний |
| FR-AUTH | 2 | Средний |
| FR-RT | 2 | Средний |
| NFR-PERF | 1 | Низкий |
| NFR-SCALE | 1 | Низкий |
| FR-INT | 1 | Низкий |
| NFR-OBS | 1 | Низкий |
| BR-ACC | 1 | Низкий |

Больше всего дефектов — в журнале аудита (FR-AUD) и очереди/голосовании (FR-RT, BR-VOTE): это сложные сквозные функции (middleware, триггеры, real-time). На них в первую очередь направлены регрессионные автотесты.

## 5. Матрица

| Требование | Пр. | Тест-кейсы | Автотесты | Дефекты | Статус |
|---|:-:|---|---|---|---|
| FR-AUTH-01 | M | TC-01, TC-02, TC-03 | AuthServiceTests, ValidatorTests (Register/Login), AuthScenarioTests | — | Проверено |
| FR-AUTH-02 | M | TC-04, TC-06 | AuthServiceTests, ValidatorTests (Register/Login), AuthScenarioTests | — | Проверено |
| FR-AUTH-03 | M | TC-05 | AuthServiceTests, ValidatorTests (Register/Login), AuthScenarioTests | — | Проверено |
| FR-AUTH-04 | M | TC-05 | AuthServiceTests, ValidatorTests (Register/Login), AuthScenarioTests | — | Проверено |
| FR-AUTH-05 | M | TC-07 | AuthServiceTests, ValidatorTests (Register/Login), AuthScenarioTests | — | Проверено |
| FR-AUTH-06 | S | TC-07 | AuthServiceTests, ValidatorTests (Register/Login), AuthScenarioTests | — | Проверено |
| FR-AUTH-07 | M | TC-08 | AuthServiceTests, ValidatorTests (Register/Login), AuthScenarioTests | — | Проверено |
| FR-AUTH-08 | M | TC-09 | AuthServiceTests, ValidatorTests (Register/Login), AuthScenarioTests | DEF-008 | Проверено |
| FR-AUTH-09 | S | — | AuthServiceTests, ValidatorTests (Register/Login), AuthScenarioTests | — | Проверено |
| FR-AUTH-10 | M | TC-10 | AuthServiceTests, ValidatorTests (Register/Login), AuthScenarioTests | DEF-009 | Проверено |
| FR-AUTH-11 | S | — | AuthServiceTests, ValidatorTests (Register/Login), AuthScenarioTests | — | Проверено |
| FR-USR-01 | M | TC-11, TC-14 | UserAdminServiceTests, AuthScenarioTests (блокировка, создание администратора, удаление аккаунта) | DEF-014 | Проверено |
| FR-USR-02 | M | TC-12 | UserAdminServiceTests, AuthScenarioTests (блокировка, создание администратора, удаление аккаунта) | DEF-003 | Проверено |
| FR-USR-03 | M | TC-13 | UserAdminServiceTests, AuthScenarioTests (блокировка, создание администратора, удаление аккаунта) | DEF-003 | Проверено |
| FR-USR-04 | M | TC-09 | UserAdminServiceTests, AuthScenarioTests (блокировка, создание администратора, удаление аккаунта) | DEF-008 | Проверено |
| FR-USR-05 | M | TC-15 | UserAdminServiceTests, AuthScenarioTests (блокировка, создание администратора, удаление аккаунта) | DEF-003, DEF-016 | Есть открытый дефект |
| FR-RT-01 | M | TC-21 | QueueServiceTests, QueueRulesTests, QueueItemTests, QueueLifecycleScenarioTests, UiScenarioTests.RealTime_* | — | Проверено |
| FR-RT-02 | M | TC-22 | QueueServiceTests, QueueRulesTests, QueueItemTests, QueueLifecycleScenarioTests, UiScenarioTests.RealTime_* | DEF-008 | Проверено |
| FR-RT-03 | M | TC-21, TC-25 | QueueServiceTests, QueueRulesTests, QueueItemTests, QueueLifecycleScenarioTests, UiScenarioTests.RealTime_* | — | Проверено |
| FR-RT-04 | M | TC-27, TC-29 | QueueServiceTests, QueueRulesTests, QueueItemTests, QueueLifecycleScenarioTests, UiScenarioTests.RealTime_* | — | Проверено |
| FR-RT-05 | M | TC-27 | QueueServiceTests, QueueRulesTests, QueueItemTests, QueueLifecycleScenarioTests, UiScenarioTests.RealTime_* | — | Проверено |
| FR-RT-06 | M | TC-28 | QueueServiceTests, QueueRulesTests, QueueItemTests, QueueLifecycleScenarioTests, UiScenarioTests.RealTime_* | DEF-018 | Проверено |
| FR-RT-07 | M | TC-23 | QueueServiceTests, QueueRulesTests, QueueItemTests, QueueLifecycleScenarioTests, UiScenarioTests.RealTime_* | — | Проверено |
| FR-RT-08 | S | — | QueueServiceTests, QueueRulesTests, QueueItemTests, QueueLifecycleScenarioTests, UiScenarioTests.RealTime_* | — | Проверено |
| FR-RT-09 | C | — | QueueServiceTests, QueueRulesTests, QueueItemTests, QueueLifecycleScenarioTests, UiScenarioTests.RealTime_* | — | Проверено |
| FR-AUD-01 | M | TC-06, TC-30 | SecurityScenarioTests.FailedRequest_IsAuditedWithRealStatusCode, SecurityScenarioTests.AdminEndpoint_RegularUser_DenialIsAudited, RepositoryTests.Audit_*, DatabaseConstraintTests.AuditLogs_* | DEF-010, DEF-011, DEF-014 | Проверено |
| FR-AUD-02 | M | TC-30 | SecurityScenarioTests.FailedRequest_IsAuditedWithRealStatusCode, SecurityScenarioTests.AdminEndpoint_RegularUser_DenialIsAudited, RepositoryTests.Audit_*, DatabaseConstraintTests.AuditLogs_* | DEF-010 | Проверено |
| FR-AUD-03 | M | TC-31 | SecurityScenarioTests.FailedRequest_IsAuditedWithRealStatusCode, SecurityScenarioTests.AdminEndpoint_RegularUser_DenialIsAudited, RepositoryTests.Audit_*, DatabaseConstraintTests.AuditLogs_* | DEF-016 | Проверено |
| FR-AUD-04 | M | TC-33 | SecurityScenarioTests.FailedRequest_IsAuditedWithRealStatusCode, SecurityScenarioTests.AdminEndpoint_RegularUser_DenialIsAudited, RepositoryTests.Audit_*, DatabaseConstraintTests.AuditLogs_* | DEF-004 | Проверено |
| FR-AUD-05 | S | — | SecurityScenarioTests.FailedRequest_IsAuditedWithRealStatusCode, SecurityScenarioTests.AdminEndpoint_RegularUser_DenialIsAudited, RepositoryTests.Audit_*, DatabaseConstraintTests.AuditLogs_* | DEF-011 | Проверено |
| FR-AUD-06 | M | TC-30 | SecurityScenarioTests.FailedRequest_IsAuditedWithRealStatusCode, SecurityScenarioTests.AdminEndpoint_RegularUser_DenialIsAudited, RepositoryTests.Audit_*, DatabaseConstraintTests.AuditLogs_* | — | Проверено |
| FR-AUD-07 | M | TC-32 | SecurityScenarioTests.FailedRequest_IsAuditedWithRealStatusCode, SecurityScenarioTests.AdminEndpoint_RegularUser_DenialIsAudited, RepositoryTests.Audit_*, DatabaseConstraintTests.AuditLogs_* | — | Проверено |
| FR-AUD-08 | S | — | SecurityScenarioTests.FailedRequest_IsAuditedWithRealStatusCode, SecurityScenarioTests.AdminEndpoint_RegularUser_DenialIsAudited, RepositoryTests.Audit_*, DatabaseConstraintTests.AuditLogs_* | — | Проверено |
| FR-AUD-09 | C | — | SecurityScenarioTests.FailedRequest_IsAuditedWithRealStatusCode, SecurityScenarioTests.AdminEndpoint_RegularUser_DenialIsAudited, RepositoryTests.Audit_*, DatabaseConstraintTests.AuditLogs_* | — | Проверено |
| FR-ROOM-01 | M | TC-16 | RoomServiceTests, RepositoryTests.Rooms_*, QueueLifecycleScenarioTests (приватная комната, бан) | DEF-009 | Проверено |
| FR-ROOM-02 | M | TC-17 | RoomServiceTests, RepositoryTests.Rooms_*, QueueLifecycleScenarioTests (приватная комната, бан) | DEF-008 | Проверено |
| FR-ROOM-03 | M | TC-18 | RoomServiceTests, RepositoryTests.Rooms_*, QueueLifecycleScenarioTests (приватная комната, бан) | — | Проверено |
| FR-CAT-01 | M | TC-34 | CatalogServiceTests, RepositoryTests.Catalog_*, TitleParserTests | — | Проверено |
| FR-INT-01 | M | TC-35, TC-36 | YouTubeTrackSourceTests (WireMock), CatalogServiceTests, QueueLifecycleScenarioTests.Catalog_YouTubeUnavailable_* | DEF-008 | Проверено |
| FR-INT-02 | M | TC-35 | YouTubeTrackSourceTests (WireMock), CatalogServiceTests, QueueLifecycleScenarioTests.Catalog_YouTubeUnavailable_* | — | Проверено |
| FR-STAT-01 | S | — | — | — | Не покрыто |
| FR-STAT-02 | C | — | — | — | Не покрыто |
| NFR-SEC-01 | — | — | SecurityScenarioTests, DatabaseConstraintTests.AppUser_ForbiddenStatement_IsDenied, AuthScenarioTests.Login_ValidCredentials_SetsHttpOnlyRefreshCookie | — | Проверено |
| NFR-SEC-02 | — | — | SecurityScenarioTests, DatabaseConstraintTests.AppUser_ForbiddenStatement_IsDenied, AuthScenarioTests.Login_ValidCredentials_SetsHttpOnlyRefreshCookie | — | Проверено |
| NFR-SEC-03 | — | — | SecurityScenarioTests, DatabaseConstraintTests.AppUser_ForbiddenStatement_IsDenied, AuthScenarioTests.Login_ValidCredentials_SetsHttpOnlyRefreshCookie | — | Проверено |
| NFR-SEC-04 | — | TC-03 | SecurityScenarioTests, DatabaseConstraintTests.AppUser_ForbiddenStatement_IsDenied, AuthScenarioTests.Login_ValidCredentials_SetsHttpOnlyRefreshCookie | — | Проверено |
| NFR-SEC-05 | — | TC-38 | SecurityScenarioTests, DatabaseConstraintTests.AppUser_ForbiddenStatement_IsDenied, AuthScenarioTests.Login_ValidCredentials_SetsHttpOnlyRefreshCookie | DEF-008, DEF-022 | Проверено |
| NFR-SEC-06 | — | — | SecurityScenarioTests, DatabaseConstraintTests.AppUser_ForbiddenStatement_IsDenied, AuthScenarioTests.Login_ValidCredentials_SetsHttpOnlyRefreshCookie | — | Проверено |
| NFR-SEC-07 | — | — | SecurityScenarioTests, DatabaseConstraintTests.AppUser_ForbiddenStatement_IsDenied, AuthScenarioTests.Login_ValidCredentials_SetsHttpOnlyRefreshCookie | — | Проверено |
| NFR-SEC-08 | — | TC-14, TC-37 | SecurityScenarioTests, DatabaseConstraintTests.AppUser_ForbiddenStatement_IsDenied, AuthScenarioTests.Login_ValidCredentials_SetsHttpOnlyRefreshCookie | DEF-008, DEF-014 | Проверено |
| NFR-TX-01 | — | — | RepositoryTests.Rooms_LockInsideTransaction_Succeeds, RepositoryTests.Queue_VotesFromSeveralUsers_ScoreIsSumOfVotes | — | Проверено |
| NFR-TX-02 | — | — | RepositoryTests.Rooms_LockInsideTransaction_Succeeds, RepositoryTests.Queue_VotesFromSeveralUsers_ScoreIsSumOfVotes | — | Проверено |
| NFR-TX-03 | — | — | RepositoryTests.Rooms_LockInsideTransaction_Succeeds, RepositoryTests.Queue_VotesFromSeveralUsers_ScoreIsSumOfVotes | — | Проверено |
| NFR-TX-04 | — | — | RepositoryTests.Rooms_LockInsideTransaction_Succeeds, RepositoryTests.Queue_VotesFromSeveralUsers_ScoreIsSumOfVotes | — | Проверено |
| NFR-PERF-01 | — | — | Нагрузочный тест NBomber (tests/SoundShare.LoadTests) | — | Проверено |
| NFR-PERF-02 | — | — | Нагрузочный тест NBomber (tests/SoundShare.LoadTests) | — | Проверено |
| NFR-PERF-03 | — | — | Нагрузочный тест NBomber (tests/SoundShare.LoadTests) | DEF-004 | Проверено |
| NFR-PERF-04 | — | — | Нагрузочный тест NBomber (tests/SoundShare.LoadTests) | — | Проверено |
| NFR-SCALE-01 | — | — | — | DEF-005 | Не покрыто |
| NFR-SCALE-02 | — | — | — | — | Не покрыто |
| NFR-REL-01 | — | — | SecurityScenarioTests.ReadinessCheck_WithDatabase_ReturnsHealthy | — | Проверено |
| NFR-REL-02 | — | — | SecurityScenarioTests.ReadinessCheck_WithDatabase_ReturnsHealthy | — | Проверено |
| NFR-REL-03 | — | TC-28 | SecurityScenarioTests.ReadinessCheck_WithDatabase_ReturnsHealthy | DEF-005, DEF-018 | Проверено |
| NFR-OBS-01 | — | — | SecurityScenarioTests.Response_ContainsCorrelationIdHeader | DEF-012 | Проверено |
| NFR-OBS-02 | — | — | SecurityScenarioTests.Response_ContainsCorrelationIdHeader | — | Проверено |
| NFR-MAINT-01 | — | — | Сборка с анализаторами, dotnet format, покрытие ≥ 70 % | — | Проверено |
| NFR-MAINT-02 | — | — | Сборка с анализаторами, dotnet format, покрытие ≥ 70 % | — | Проверено |
| NFR-MAINT-03 | — | — | Сборка с анализаторами, dotnet format, покрытие ≥ 70 % | — | Проверено |
| NFR-MAINT-04 | — | — | Сборка с анализаторами, dotnet format, покрытие ≥ 70 % | — | Проверено |
| NFR-MAINT-05 | — | — | Сборка с анализаторами, dotnet format, покрытие ≥ 70 % | — | Проверено |
| BR-VOTE-01 | — | — | QueueRulesTests, QueueServiceTests, QueueLifecycleScenarioTests, constraints_test.sql | — | Проверено |
| BR-VOTE-02 | — | TC-23 | QueueRulesTests, QueueServiceTests, QueueLifecycleScenarioTests, constraints_test.sql | — | Проверено |
| BR-VOTE-03 | — | — | QueueRulesTests, QueueServiceTests, QueueLifecycleScenarioTests, constraints_test.sql | DEF-007 | Проверено |
| BR-VOTE-04 | — | TC-21 | QueueRulesTests, QueueServiceTests, QueueLifecycleScenarioTests, constraints_test.sql | — | Проверено |
| BR-VOTE-05 | — | TC-24 | QueueRulesTests, QueueServiceTests, QueueLifecycleScenarioTests, constraints_test.sql | DEF-015 | Проверено |
| BR-VOTE-06 | — | — | QueueRulesTests, QueueServiceTests, QueueLifecycleScenarioTests, constraints_test.sql | — | Проверено |
| BR-VOTE-07 | — | TC-26 | QueueRulesTests, QueueServiceTests, QueueLifecycleScenarioTests, constraints_test.sql | DEF-003, DEF-019 | Есть открытый дефект |
| BR-VOTE-08 | — | TC-25 | QueueRulesTests, QueueServiceTests, QueueLifecycleScenarioTests, constraints_test.sql | DEF-003 | Проверено |
| BR-VOTE-09 | — | — | QueueRulesTests, QueueServiceTests, QueueLifecycleScenarioTests, constraints_test.sql | — | Проверено |
| BR-TRK-01 | — | — | TitleParserTests, CatalogServiceTests, DatabaseConstraintTests.Tracks_* | — | Проверено |
| BR-TRK-02 | — | — | TitleParserTests, CatalogServiceTests, DatabaseConstraintTests.Tracks_* | — | Проверено |
| BR-TRK-03 | — | TC-35 | TitleParserTests, CatalogServiceTests, DatabaseConstraintTests.Tracks_* | — | Проверено |
| BR-TRK-04 | — | — | TitleParserTests, CatalogServiceTests, DatabaseConstraintTests.Tracks_* | — | Проверено |
| BR-TRK-05 | — | TC-29 | TitleParserTests, CatalogServiceTests, DatabaseConstraintTests.Tracks_* | — | Проверено |
| BR-TRK-06 | — | TC-27 | TitleParserTests, CatalogServiceTests, DatabaseConstraintTests.Tracks_* | — | Проверено |
| BR-ROOM-01 | — | TC-17, TC-18 | RoomMemberTests, RoomServiceTests, QueueLifecycleScenarioTests | — | Проверено |
| BR-ROOM-02 | — | — | RoomMemberTests, RoomServiceTests, QueueLifecycleScenarioTests | DEF-002 | Проверено |
| BR-ROOM-03 | — | — | RoomMemberTests, RoomServiceTests, QueueLifecycleScenarioTests | — | Проверено |
| BR-ROOM-04 | — | TC-22 | RoomMemberTests, RoomServiceTests, QueueLifecycleScenarioTests | DEF-003, DEF-008 | Проверено |
| BR-ROOM-05 | — | — | RoomMemberTests, RoomServiceTests, QueueLifecycleScenarioTests | — | Проверено |
| BR-ROOM-06 | — | TC-20 | RoomMemberTests, RoomServiceTests, QueueLifecycleScenarioTests | — | Проверено |
| BR-ROOM-07 | — | — | RoomMemberTests, RoomServiceTests, QueueLifecycleScenarioTests | — | Проверено |
| BR-ROOM-08 | — | TC-19 | RoomMemberTests, RoomServiceTests, QueueLifecycleScenarioTests | DEF-020 | Есть открытый дефект |
| BR-ACC-01 | — | TC-15 | UserAdminServiceTests.DeleteOwnAccount_*, AuthScenarioTests.DeleteOwnAccount_* | DEF-016 | Есть открытый дефект |
