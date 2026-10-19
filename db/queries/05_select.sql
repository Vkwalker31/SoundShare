-- 05_select.sql
-- Stage 5: SELECT queries of SoundShare: queue, rooms, statistics, catalog search, administration.
-- Run as SOUNDSHARE after seed/demo.sql. Read only.

SET DEFINE OFF
SET FEEDBACK ON
SET LINESIZE 220
SET PAGESIZE 100
COLUMN title FORMAT A26
COLUMN artists FORMAT A28
COLUMN added_by FORMAT A12
COLUMN username FORMAT A14
COLUMN name FORMAT A18
COLUMN room FORMAT A18
COLUMN now_playing FORMAT A26
COLUMN member_role FORMAT A12
COLUMN state FORMAT A8
COLUMN role FORMAT A6
COLUMN tracks_list FORMAT A60
COLUMN banned_until FORMAT A16
COLUMN started FORMAT A9
COLUMN previous_started FORMAT A9
COLUMN playlist_length FORMAT A8
COLUMN registered FORMAT A10

VARIABLE room_id NUMBER
VARIABLE query   VARCHAR2(100)
BEGIN
    SELECT id INTO :room_id FROM rooms WHERE name = 'Rock & Metal Bar';
    :query := 'ro';
END;
/

PROMPT
PROMPT ===== 5.1 Очередь комнаты в порядке воспроизведения: закреплённые, рейтинг, время добавления =====
WITH scores AS (
    SELECT playlist_track_id, SUM(vote_value) AS score
    FROM track_votes
    GROUP BY playlist_track_id
)
SELECT ROW_NUMBER() OVER (ORDER BY pt.pinned_at NULLS LAST, NVL(s.score, 0) DESC, pt.added_at) AS pos,
       t.title,
       (SELECT LISTAGG(a.name, ', ') WITHIN GROUP (ORDER BY ta.artist_order)
        FROM track_artists ta JOIN artists a ON a.id = ta.artist_id
        WHERE ta.track_id = t.id) AS artists,
       u.username AS added_by,
       NVL(s.score, 0) AS score,
       CASE WHEN pt.pinned_at IS NOT NULL THEN 'yes' ELSE 'no' END AS pinned
FROM playlist_tracks pt
JOIN playlists p ON p.id = pt.playlist_id
JOIN tracks t ON t.id = pt.track_id
JOIN users u ON u.id = pt.added_by_id
LEFT JOIN scores s ON s.playlist_track_id = pt.id
WHERE p.room_id = :room_id
  AND pt.status = 'QUEUED'
ORDER BY pos;

PROMPT ===== 5.2 Текущий трек: сколько секунд прошло и осталось (синхронизация клиентов) =====
WITH playing AS (
    SELECT t.title, t.duration_sec, SYS_EXTRACT_UTC(SYSTIMESTAMP) - pt.started_at AS elapsed
    FROM playlist_tracks pt
    JOIN playlists p ON p.id = pt.playlist_id
    JOIN tracks t ON t.id = pt.track_id
    WHERE p.room_id = :room_id
      AND pt.status = 'PLAYING'
)
SELECT title,
       duration_sec,
       ROUND(EXTRACT(DAY FROM elapsed) * 86400 + EXTRACT(HOUR FROM elapsed) * 3600
           + EXTRACT(MINUTE FROM elapsed) * 60 + EXTRACT(SECOND FROM elapsed)) AS elapsed_sec,
       GREATEST(duration_sec - ROUND(EXTRACT(DAY FROM elapsed) * 86400 + EXTRACT(HOUR FROM elapsed) * 3600
           + EXTRACT(MINUTE FROM elapsed) * 60 + EXTRACT(SECOND FROM elapsed)), 0) AS remaining_sec
FROM playing;

PROMPT ===== 5.3 Список публичных комнат: участники онлайн и текущий трек =====
SELECT r.id,
       r.name,
       COUNT(m.user_id) AS online_cnt,
       (SELECT t.title
        FROM playlists p
        JOIN playlist_tracks pt ON pt.playlist_id = p.id AND pt.status = 'PLAYING'
        JOIN tracks t ON t.id = pt.track_id
        WHERE p.room_id = r.id) AS now_playing
FROM rooms r
LEFT JOIN room_members m ON m.room_id = r.id AND m.left_at IS NULL
WHERE r.is_active = 1
  AND r.is_private = 0
GROUP BY r.id, r.name
ORDER BY online_cnt DESC, r.name;

PROMPT ===== 5.4 Участники комнаты: роль, статус, срок бана =====
SELECT u.username,
       m.member_role,
       CASE
           WHEN m.left_at IS NULL THEN 'online'
           WHEN m.banned_until > SYS_EXTRACT_UTC(SYSTIMESTAMP) THEN 'banned'
           ELSE 'left'
       END AS state,
       CASE
           WHEN m.banned_until IS NULL THEN NULL
           WHEN EXTRACT(YEAR FROM m.banned_until) = 9999 THEN 'forever'
           ELSE TO_CHAR(m.banned_until, 'YYYY-MM-DD HH24:MI')
       END AS banned_until
FROM room_members m
JOIN users u ON u.id = m.user_id
JOIN rooms r ON r.id = m.room_id
WHERE r.name = 'Jazz Lounge'
ORDER BY CASE m.member_role WHEN 'OWNER' THEN 1 WHEN 'MODERATOR' THEN 2 ELSE 3 END, m.joined_at;

PROMPT ===== 5.5 Рейтинг треков по сумме голосов во всех комнатах: RANK и DENSE_RANK =====
SELECT t.title,
       SUM(v.vote_value) AS total_score,
       COUNT(*) AS votes,
       RANK() OVER (ORDER BY SUM(v.vote_value) DESC) AS rnk,
       DENSE_RANK() OVER (ORDER BY SUM(v.vote_value) DESC) AS dense_rnk
FROM track_votes v
JOIN playlist_tracks pt ON pt.id = v.playlist_track_id
JOIN tracks t ON t.id = pt.track_id
GROUP BY t.id, t.title
ORDER BY rnk, t.title;

PROMPT ===== 5.6 Самые активные участники каждой комнаты: треков добавлено, средний рейтинг, место =====
WITH item_scores AS (
    SELECT pt.id, pt.added_by_id, p.room_id, NVL(SUM(v.vote_value), 0) AS score
    FROM playlist_tracks pt
    JOIN playlists p ON p.id = pt.playlist_id AND p.room_id IS NOT NULL
    LEFT JOIN track_votes v ON v.playlist_track_id = pt.id
    GROUP BY pt.id, pt.added_by_id, p.room_id
)
SELECT r.name AS room,
       u.username,
       COUNT(*) AS tracks_added,
       ROUND(AVG(s.score), 2) AS avg_score,
       RANK() OVER (PARTITION BY r.id ORDER BY COUNT(*) DESC, AVG(s.score) DESC) AS place
FROM item_scores s
JOIN rooms r ON r.id = s.room_id
JOIN users u ON u.id = s.added_by_id
GROUP BY r.id, r.name, u.username
ORDER BY r.name, place;

PROMPT ===== 5.7 Исполнители: число треков, сколько раз звучали, список треков =====
SELECT a.name,
       COUNT(DISTINCT ta.track_id) AS tracks,
       COUNT(pt.id) AS times_played,
       LISTAGG(DISTINCT t.title, '; ') WITHIN GROUP (ORDER BY t.title) AS tracks_list
FROM artists a
JOIN track_artists ta ON ta.artist_id = a.id
JOIN tracks t ON t.id = ta.track_id
LEFT JOIN playlist_tracks pt ON pt.track_id = t.id AND pt.status IN ('PLAYING', 'PLAYED', 'SKIPPED')
GROUP BY a.id, a.name
ORDER BY times_played DESC, a.name;

PROMPT ===== 5.8 Администрирование: пользователи с ролью и числом активных сессий, вторая страница по 5 =====
SELECT u.id,
       u.username,
       r.name AS role,
       CASE u.is_active WHEN 1 THEN 'active' ELSE 'blocked' END AS state,
       (SELECT COUNT(*) FROM refresh_tokens rt
        WHERE rt.user_id = u.id AND rt.revoked_at IS NULL AND rt.expires_at > SYS_EXTRACT_UTC(SYSTIMESTAMP)) AS sessions,
       TO_CHAR(u.created_at, 'YYYY-MM-DD') AS registered
FROM users u
JOIN roles r ON r.id = u.role_id
ORDER BY u.id
OFFSET 5 ROWS FETCH NEXT 5 ROWS ONLY;

PROMPT ===== 5.9 Поиск по каталогу без учёта регистра: по названию трека или имени исполнителя =====
SELECT t.id, t.title, t.duration_sec, t.external_source_id
FROM tracks t
WHERE UPPER(t.title) LIKE '%' || UPPER(:query) || '%'
   OR EXISTS (SELECT 1
              FROM track_artists ta
              JOIN artists a ON a.id = ta.artist_id
              WHERE ta.track_id = t.id
                AND UPPER(a.name) LIKE '%' || UPPER(:query) || '%')
ORDER BY t.title
FETCH FIRST 20 ROWS ONLY;

PROMPT ===== 5.10 Прогресс голосования за пропуск: голоса против требуемого большинства =====
SELECT t.title,
       (SELECT COUNT(*) FROM skip_votes sv WHERE sv.playlist_track_id = pt.id) AS skip_votes,
       FLOOR(m.online_cnt / 2) + 1 AS required,
       m.online_cnt
FROM playlist_tracks pt
JOIN playlists p ON p.id = pt.playlist_id
JOIN tracks t ON t.id = pt.track_id
CROSS JOIN (SELECT COUNT(*) AS online_cnt FROM room_members WHERE room_id = :room_id AND left_at IS NULL) m
WHERE p.room_id = :room_id
  AND pt.status = 'PLAYING';

PROMPT ===== 5.11 Треки каталога, которые ещё ни разу не ставили в очередь комнаты (анти-соединение) =====
SELECT t.title
FROM tracks t
WHERE NOT EXISTS (SELECT 1
                  FROM playlist_tracks pt
                  JOIN playlists p ON p.id = pt.playlist_id
                  WHERE pt.track_id = t.id AND p.room_id IS NOT NULL)
ORDER BY t.title;

PROMPT ===== 5.12 Пользователи, чаще всех голосующие против (HAVING) =====
SELECT u.username,
       SUM(CASE WHEN v.vote_value = -1 THEN 1 ELSE 0 END) AS downvotes,
       SUM(CASE WHEN v.vote_value = 1 THEN 1 ELSE 0 END) AS upvotes
FROM track_votes v
JOIN users u ON u.id = v.user_id
GROUP BY u.username
HAVING SUM(CASE WHEN v.vote_value = -1 THEN 1 ELSE 0 END) >= 1
ORDER BY downvotes DESC, u.username;

PROMPT ===== 5.13 История воспроизведения комнаты: порядок и паузы между треками (LAG) =====
SELECT t.title,
       pt.status,
       TO_CHAR(pt.started_at, 'HH24:MI:SS') AS started,
       TO_CHAR(LAG(pt.started_at) OVER (ORDER BY pt.started_at), 'HH24:MI:SS') AS previous_started,
       ROUND((CAST(pt.started_at AS DATE) - CAST(LAG(pt.started_at) OVER (ORDER BY pt.started_at) AS DATE)) * 1440, 1) AS min_after_previous
FROM playlist_tracks pt
JOIN playlists p ON p.id = pt.playlist_id
JOIN tracks t ON t.id = pt.track_id
WHERE p.room_id = :room_id
  AND pt.started_at IS NOT NULL
ORDER BY pt.started_at;

PROMPT ===== 5.14 Личный плейлист: позиции и накопительная длительность (SUM OVER) =====
SELECT pt.position,
       t.title,
       t.duration_sec,
       SUM(t.duration_sec) OVER (ORDER BY pt.position) AS running_total_sec,
       TO_CHAR(TRUNC(SUM(t.duration_sec) OVER () / 60)) || ' min' AS playlist_length
FROM playlist_tracks pt
JOIN playlists p ON p.id = pt.playlist_id
JOIN tracks t ON t.id = pt.track_id
WHERE p.name = 'Road trip'
ORDER BY pt.position;

PROMPT ===== 5.15 Распределение позиций очереди по статусам и комнатам (PIVOT) =====
SELECT *
FROM (
    SELECT r.name AS room, pt.status
    FROM playlist_tracks pt
    JOIN playlists p ON p.id = pt.playlist_id
    JOIN rooms r ON r.id = p.room_id
)
PIVOT (COUNT(*) FOR status IN ('QUEUED' AS queued, 'PLAYING' AS playing, 'PLAYED' AS played,
                               'SKIPPED' AS skipped, 'REMOVED' AS removed, 'AUTO_REMOVED' AS auto_removed))
ORDER BY room;

PROMPT ===== 5.16 Итоги по комнатам и в целом (GROUP BY ROLLUP) =====
SELECT NVL(r.name, '== ВСЕГО ==') AS room,
       COUNT(pt.id) AS queue_items,
       COUNT(DISTINCT pt.added_by_id) AS authors,
       SUM(t.duration_sec) AS total_sec
FROM playlist_tracks pt
JOIN playlists p ON p.id = pt.playlist_id
JOIN rooms r ON r.id = p.room_id
JOIN tracks t ON t.id = pt.track_id
GROUP BY ROLLUP (r.name)
ORDER BY GROUPING(r.name), r.name;

EXIT
