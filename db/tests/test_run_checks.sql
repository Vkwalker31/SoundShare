-- test_run_checks.sql
-- Database part of the test case run: run as SOUNDSHARE right after tests/postman collection on fresh demo data.
-- Each block prints the evidence for one test case step whose expected result is stated in terms of the database.

SET DEFINE OFF
SET FEEDBACK OFF
SET LINESIZE 200
SET PAGESIZE 100
COLUMN username FORMAT A20
COLUMN email FORMAT A34
COLUMN hash_prefix FORMAT A14
COLUMN room FORMAT A18
COLUMN member_role FORMAT A12
COLUMN title FORMAT A26
COLUMN status FORMAT A13
COLUMN action FORMAT A20
COLUMN correlation_id FORMAT A14
COLUMN path FORMAT A16
COLUMN check_result FORMAT A6

PROMPT ===== TC-04 Пароль хранится только как хеш Identity v3 =====
SELECT username,
       SUBSTR(password_hash, 1, 12) AS hash_prefix,
       LENGTH(password_hash) AS hash_len,
       CASE WHEN password_hash <> 'Strong_Pass1' AND password_hash LIKE 'AQAAAAIAAYag%' AND LENGTH(password_hash) = 84
            THEN 'PASS' ELSE 'FAIL' END AS check_result
FROM users
WHERE username = 'new_user';

PROMPT ===== TC-05.3 / TC-07.1 Refresh-токены user_1: только SHA-256, ротация со ссылкой на замену =====
SELECT rt.id,
       LENGTH(RAWTOHEX(rt.token_hash)) / 2 AS hash_bytes,
       CASE WHEN rt.revoked_at IS NULL THEN 'active' ELSE 'revoked' END AS state,
       rt.replaced_by_token_id,
       ROUND((CAST(rt.expires_at AS DATE) - CAST(rt.created_at AS DATE)), 2) AS lifetime_days
FROM refresh_tokens rt
JOIN users u ON u.id = rt.user_id
WHERE u.username = 'user_1'
ORDER BY rt.id;

PROMPT ===== TC-09.1 user_6 заблокирован, все токены отозваны =====
SELECT u.username,
       u.is_active,
       (SELECT COUNT(*) FROM refresh_tokens rt WHERE rt.user_id = u.id AND rt.revoked_at IS NULL) AS active_tokens,
       CASE WHEN u.is_active = 0
                 AND (SELECT COUNT(*) FROM refresh_tokens rt WHERE rt.user_id = u.id AND rt.revoked_at IS NULL) = 0
            THEN 'PASS' ELSE 'FAIL' END AS check_result
FROM users u
WHERE u.username = 'user_6';

PROMPT ===== TC-15.2 Удалённый аккаунт обезличен, позиция трека сохранена =====
SELECT u.username,
       u.email,
       u.is_active,
       SUBSTR(u.password_hash, 1, 14) AS hash_prefix,
       (SELECT COUNT(*) FROM refresh_tokens rt WHERE rt.user_id = u.id AND rt.revoked_at IS NULL) AS active_tokens,
       (SELECT COUNT(*) FROM room_members m WHERE m.user_id = u.id AND m.left_at IS NULL) AS active_rooms,
       (SELECT COUNT(*) FROM playlist_tracks pt WHERE pt.added_by_id = u.id) AS queue_items,
       CASE WHEN u.username = 'deleted_' || u.id AND u.email = 'deleted_' || u.id || '@deleted.invalid' AND u.is_active = 0
            THEN 'PASS' ELSE 'FAIL' END AS check_result
FROM users u
WHERE u.username LIKE 'deleted\_%' ESCAPE '\';

PROMPT ===== TC-15.2 IP и User-Agent в журнале у удалённого пользователя =====
SELECT COUNT(*) AS audit_rows,
       COUNT(a.ip_address) AS rows_with_ip
FROM audit_logs a
JOIN users u ON u.id = a.user_id
WHERE u.username LIKE 'deleted\_%' ESCAPE '\';

PROMPT ===== TC-16.2 Комната Indie Night: очередь и членство создателя созданы вместе (роль OWNER снята, когда user_3 ушёл в Jazz Lounge в TC-17) =====
SELECT r.name AS room,
       (SELECT COUNT(*) FROM playlists p WHERE p.room_id = r.id) AS queues,
       (SELECT COUNT(*) FROM room_members m JOIN users u ON u.id = m.user_id
        WHERE m.room_id = r.id AND u.username = 'user_3') AS creator_rows,
       (SELECT COUNT(*) FROM audit_logs a WHERE a.action = 'Room.Create' AND a.status_code = 201
        AND a.user_id = r.created_by_id) AS create_audits,
       CASE WHEN (SELECT COUNT(*) FROM playlists p WHERE p.room_id = r.id) = 1
                 AND (SELECT COUNT(*) FROM room_members m WHERE m.room_id = r.id AND m.user_id = r.created_by_id) = 1
            THEN 'PASS' ELSE 'FAIL' END AS check_result
FROM rooms r
WHERE r.name = 'Indie Night';

PROMPT ===== TC-17.3 Одно членство user_3 в Jazz Lounge =====
SELECT COUNT(*) AS rows_in_jazz,
       CASE WHEN COUNT(*) = 1 THEN 'PASS' ELSE 'FAIL' END AS check_result
FROM room_members m
JOIN users u ON u.id = m.user_id
JOIN rooms r ON r.id = m.room_id
WHERE u.username = 'user_3' AND r.name = 'Jazz Lounge';

PROMPT ===== TC-19.2 user_spammer исключён, бан около 10 минут =====
SELECT u.username,
       CASE WHEN m.left_at IS NULL THEN 'online' ELSE 'left' END AS state,
       ROUND((CAST(m.banned_until AS DATE) - CAST(m.left_at AS DATE)) * 1440, 1) AS ban_minutes,
       CASE WHEN m.left_at IS NOT NULL AND (CAST(m.banned_until AS DATE) - CAST(m.left_at AS DATE)) * 1440 BETWEEN 9.9 AND 10.1
            THEN 'PASS' ELSE 'FAIL' END AS check_result
FROM room_members m
JOIN users u ON u.id = m.user_id
JOIN rooms r ON r.id = m.room_id
WHERE u.username = 'user_spammer' AND r.name = 'Rock & Metal Bar';

PROMPT ===== TC-20.2 У каждого пользователя не больше одного активного членства =====
SELECT COUNT(*) AS users_in_two_rooms,
       CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'FAIL' END AS check_result
FROM (SELECT user_id FROM room_members WHERE left_at IS NULL GROUP BY user_id HAVING COUNT(*) > 1);

PROMPT ===== TC-24 Статус автоудалённой позиции и записи журнала о ней =====
SELECT t.title, pt.status
FROM playlist_tracks pt
JOIN tracks t ON t.id = pt.track_id
WHERE pt.status = 'AUTO_REMOVED';

SELECT a.action, a.status_code, a.path
FROM audit_logs a
JOIN users u ON u.id = a.user_id
WHERE u.username = 'user_3' AND a.action LIKE 'Queue.%'
ORDER BY a.id;

PROMPT ===== TC-26.2 Пропущенный трек SKIPPED, ровно один PLAYING в очереди =====
SELECT t.title, pt.status
FROM playlist_tracks pt
JOIN playlists p ON p.id = pt.playlist_id
JOIN rooms r ON r.id = p.room_id
JOIN tracks t ON t.id = pt.track_id
WHERE r.name = 'Rock & Metal Bar' AND pt.status IN ('SKIPPED', 'PLAYING')
ORDER BY pt.status, pt.id;

PROMPT ===== TC-30.3 Запись Room.Create с CorrelationId tc30-check, GET не журналируется =====
SELECT a.action, a.http_method, a.path, a.status_code, a.correlation_id,
       CASE WHEN a.ip_address IS NOT NULL THEN 'yes' ELSE 'no' END AS has_ip,
       CASE WHEN a.user_agent IS NOT NULL THEN 'yes' ELSE 'no' END AS has_user_agent,
       a.duration_ms
FROM audit_logs a
WHERE a.correlation_id = 'tc30-check';

SELECT COUNT(*) AS get_rows,
       CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'FAIL' END AS check_result
FROM audit_logs
WHERE http_method = 'GET' AND path = '/api/rooms' AND status_code < 400;

PROMPT ===== TC-14 Отказ в доступе к администрированию журналируется (DEF-014) =====
SELECT a.action, a.path, a.status_code
FROM audit_logs a
JOIN users u ON u.id = a.user_id
WHERE u.username = 'user_1' AND a.status_code IN (401, 403)
ORDER BY a.id;

PROMPT ===== TC-24 Автоудаление записано в журнал триггером (DEF-015) =====
SELECT a.source, a.action, a.entity_type || ':' || a.entity_id AS entity
FROM audit_logs a
WHERE a.action IN ('Queue.AutoRemoved', 'Queue.Skipped')
ORDER BY a.id;

PROMPT ===== TC-31 В журнале нет паролей и токенов =====
SELECT COUNT(*) AS leaks,
       CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'FAIL' END AS check_result
FROM audit_logs
WHERE details LIKE '%Strong_Pass1%'
   OR details LIKE '%Demo_Pass1%'
   OR details LIKE '%Wrong_Pass1%'
   OR details LIKE '%Bearer%'
   OR path LIKE '%access_token%';

PROMPT ===== TC-31 Записи журнала по источникам после прогона =====
SELECT source, COUNT(*) AS cnt FROM audit_logs GROUP BY source ORDER BY source;

EXIT
