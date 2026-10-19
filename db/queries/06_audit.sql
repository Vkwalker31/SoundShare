-- 06_audit.sql
-- Stage 6: audit log queries for the administrator: feed, filters, failures, brute force, performance,
-- correlation, JSON details, database-level triggers and privileges.
-- Run as SOUNDSHARE after seed/demo.sql and V007. Trigger demo is rolled back.

SET DEFINE OFF
SET FEEDBACK ON
SET LINESIZE 220
SET PAGESIZE 100
COLUMN created FORMAT A19
COLUMN username FORMAT A14
COLUMN action FORMAT A22
COLUMN path FORMAT A30
COLUMN ip_address FORMAT A14
COLUMN details FORMAT A70
COLUMN source FORMAT A6
COLUMN privilege FORMAT A10
COLUMN grantee FORMAT A16
COLUMN table_name FORMAT A16
COLUMN entity FORMAT A12
COLUMN room_name FORMAT A20
COLUMN is_private FORMAT A10
COLUMN hour FORMAT A16
COLUMN bar FORMAT A50
COLUMN last_seen FORMAT A16
COLUMN first_try FORMAT A9
COLUMN last_try FORMAT A9

VARIABLE cursor_id NUMBER
VARIABLE user_name VARCHAR2(32)
BEGIN
    :cursor_id := NULL;
    :user_name := 'user_owner';
END;
/

PROMPT
PROMPT ===== 6.1 Лента журнала: последние события, постраничная навигация по ключу (keyset) =====
SELECT a.id,
       TO_CHAR(a.created_at, 'YYYY-MM-DD HH24:MI:SS') AS created,
       a.source,
       NVL(u.username, '-') AS username,
       a.action,
       a.status_code,
       a.duration_ms
FROM audit_logs a
LEFT JOIN users u ON u.id = a.user_id
WHERE (:cursor_id IS NULL OR a.id < :cursor_id)
ORDER BY a.id DESC
FETCH FIRST 20 ROWS ONLY;

PROMPT ===== 6.2 Фильтр: действия конкретного пользователя за последние 2 часа =====
SELECT TO_CHAR(a.created_at, 'HH24:MI:SS') AS created, a.action, a.http_method, a.path, a.status_code
FROM audit_logs a
JOIN users u ON u.id = a.user_id
WHERE u.username = :user_name
  AND a.created_at >= SYS_EXTRACT_UTC(SYSTIMESTAMP) - INTERVAL '2' HOUR
ORDER BY a.created_at;

PROMPT ===== 6.3 Неуспешные запросы: группировка по действию и коду ответа =====
SELECT a.action, a.status_code, COUNT(*) AS cnt, MAX(TO_CHAR(a.created_at, 'YYYY-MM-DD HH24:MI')) AS last_seen
FROM audit_logs a
WHERE a.status_code >= 400
GROUP BY a.action, a.status_code
ORDER BY cnt DESC, a.action;

PROMPT ===== 6.4 Подозрение на подбор пароля: неудачные входы с одного IP за последний час =====
SELECT a.ip_address,
       COUNT(*) AS failed_logins,
       COUNT(DISTINCT a.user_id) AS accounts,
       MIN(TO_CHAR(a.created_at, 'HH24:MI:SS')) AS first_try,
       MAX(TO_CHAR(a.created_at, 'HH24:MI:SS')) AS last_try
FROM audit_logs a
WHERE a.action = 'Auth.LoginFailed'
  AND a.created_at >= SYS_EXTRACT_UTC(SYSTIMESTAMP) - INTERVAL '1' HOUR
GROUP BY a.ip_address
HAVING COUNT(*) >= 1
ORDER BY failed_logins DESC;

PROMPT ===== 6.5 Активность пользователей по действиям с подытогами (ROLLUP) =====
SELECT CASE WHEN GROUPING(u.username) = 1 THEN '== ВСЕГО ==' ELSE NVL(u.username, '(system)') END AS username,
       CASE WHEN GROUPING(a.action) = 1 THEN '-- итого --' ELSE a.action END AS action,
       COUNT(*) AS events
FROM audit_logs a
LEFT JOIN users u ON u.id = a.user_id
GROUP BY ROLLUP (u.username, a.action)
ORDER BY GROUPING(u.username), u.username, GROUPING(a.action), a.action;

PROMPT ===== 6.6 Производительность эндпоинтов: среднее, медиана и 95-й перцентиль времени ответа =====
SELECT a.http_method,
       a.path,
       COUNT(*) AS requests,
       ROUND(AVG(a.duration_ms), 1) AS avg_ms,
       PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY a.duration_ms) AS p50_ms,
       PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY a.duration_ms) AS p95_ms,
       MAX(a.duration_ms) AS max_ms
FROM audit_logs a
WHERE a.source = 'HTTP'
GROUP BY a.http_method, a.path
ORDER BY p95_ms DESC;

PROMPT ===== 6.7 Трассировка последнего запроса по CorrelationId (все записи HTTP и DOMAIN) =====
SELECT a.id, a.source, a.action, a.status_code, TO_CHAR(a.created_at, 'HH24:MI:SS.FF3') AS created
FROM audit_logs a
WHERE a.correlation_id = (SELECT MAX(correlation_id) KEEP (DENSE_RANK LAST ORDER BY id)
                          FROM audit_logs WHERE correlation_id IS NOT NULL)
ORDER BY a.id;

PROMPT ===== 6.8 Разбор JSON-деталей: какие комнаты создавались и с какими параметрами =====
SELECT a.id,
       JSON_VALUE(a.details, '$.name') AS room_name,
       JSON_VALUE(a.details, '$.isPrivate') AS is_private,
       NVL(u.username, '-') AS username
FROM audit_logs a
LEFT JOIN users u ON u.id = a.user_id
WHERE a.action = 'Room.Create'
  AND a.details IS NOT NULL
ORDER BY a.id;

PROMPT ===== 6.9 Гистограмма активности по часам =====
SELECT TO_CHAR(TRUNC(a.created_at, 'HH24'), 'YYYY-MM-DD HH24') || ':00' AS hour,
       COUNT(*) AS events,
       RPAD('#', LEAST(COUNT(*), 50), '#') AS bar
FROM audit_logs a
GROUP BY TRUNC(a.created_at, 'HH24')
ORDER BY TRUNC(a.created_at, 'HH24');

PROMPT ===== 6.10 Триггеры: блокировка, смена роли и бан попадают в журнал автоматически =====
UPDATE users SET is_active = 0 WHERE username = 'user_spammer';
UPDATE users SET role_id = 1 WHERE username = 'user_mod';
UPDATE room_members
SET left_at = SYS_EXTRACT_UTC(SYSTIMESTAMP), banned_until = SYS_EXTRACT_UTC(SYSTIMESTAMP) + INTERVAL '1' HOUR
WHERE user_id = (SELECT id FROM users WHERE username = 'user_6') AND left_at IS NULL;
SELECT a.action, a.entity_type || ':' || a.entity_id AS entity, a.details
FROM audit_logs a
WHERE a.source = 'DOMAIN'
ORDER BY a.id DESC
FETCH FIRST 3 ROWS ONLY;

PROMPT ===== 6.11 Действия администраторов и изменения прав (HTTP Admin.* и записи триггеров DOMAIN) =====
SELECT TO_CHAR(a.created_at, 'YYYY-MM-DD HH24:MI:SS') AS created, a.source, a.action,
       a.entity_type || ':' || a.entity_id AS entity
FROM audit_logs a
WHERE a.action LIKE 'Admin.%'
   OR a.source = 'DOMAIN'
ORDER BY a.id DESC
FETCH FIRST 20 ROWS ONLY;
ROLLBACK;

PROMPT ===== 6.12 Журнал только для добавления: права приложения на AUDIT_LOGS и USERS =====
SELECT grantee, table_name, privilege
FROM user_tab_privs
WHERE table_name IN ('AUDIT_LOGS', 'USERS')
ORDER BY table_name, privilege;

PROMPT ===== 6.13 Записи старше 180 дней, которые подлежат архивированию =====
SELECT COUNT(*) AS to_archive, MIN(created_at) AS oldest
FROM audit_logs
WHERE created_at < SYS_EXTRACT_UTC(SYSTIMESTAMP) - INTERVAL '180' DAY(3);

EXIT
