-- demo.sql
-- Demo data for the defense scenario. Password of every demo user: Demo_Pass1 (demo only).
-- Run as SOUNDSHARE on an empty schema after migrations.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET DEFINE OFF
SET FEEDBACK OFF
SET SERVEROUTPUT ON

DECLARE
    v_count PLS_INTEGER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM users;
    IF v_count > 0 THEN
        RAISE_APPLICATION_ERROR(-20000, 'Schema already has data: run reset.sql and migrations first');
    END IF;
END;
/

INSERT INTO users (username, email, password_hash, role_id)
SELECT u.username, u.username || '@example.com', h.hash, u.role_id
FROM (
    SELECT 'admin' AS username, 1 AS role_id FROM dual UNION ALL
    SELECT 'user_owner',   2 FROM dual UNION ALL
    SELECT 'user_mod',     2 FROM dual UNION ALL
    SELECT 'user_1',       2 FROM dual UNION ALL
    SELECT 'user_2',       2 FROM dual UNION ALL
    SELECT 'user_3',       2 FROM dual UNION ALL
    SELECT 'user_4',       2 FROM dual UNION ALL
    SELECT 'user_5',       2 FROM dual UNION ALL
    SELECT 'user_6',       2 FROM dual UNION ALL
    SELECT 'user_7',       2 FROM dual UNION ALL
    SELECT 'user_spammer', 2 FROM dual UNION ALL
    SELECT 'user_deleted', 2 FROM dual UNION ALL
    SELECT 'jazz_host',    2 FROM dual
) u
CROSS JOIN (
    SELECT 'AQAAAAIAAYagAAAAEEpnK5MMjkNs58sFajENOfwBq3J9989pJ06MGEoSg7oTTVK7lE8QqE3TTjLcAzwz7w==' AS hash FROM dual
) h;

INSERT INTO artists (name)
SELECT name FROM (
    SELECT 'AC/DC' AS name FROM dual UNION ALL
    SELECT 'Metallica'          FROM dual UNION ALL
    SELECT 'Nirvana'            FROM dual UNION ALL
    SELECT 'Rick Astley'        FROM dual UNION ALL
    SELECT 'Queen'              FROM dual UNION ALL
    SELECT 'Guns N'' Roses'     FROM dual UNION ALL
    SELECT 'Linkin Park'        FROM dual UNION ALL
    SELECT 'Michael Jackson'    FROM dual UNION ALL
    SELECT 'The Weeknd'         FROM dual UNION ALL
    SELECT 'Luis Fonsi'         FROM dual UNION ALL
    SELECT 'Daddy Yankee'       FROM dual
);

INSERT INTO tracks (title, duration_sec, external_source_id)
SELECT title, duration_sec, video_id FROM (
    SELECT 'Thunderstruck' AS title, 292 AS duration_sec, 'v2AC41dglnM' AS video_id FROM dual UNION ALL
    SELECT 'Enter Sandman',                331, 'CD-E-LDc384' FROM dual UNION ALL
    SELECT 'Smells Like Teen Spirit',      301, 'hTWKbfoikeg' FROM dual UNION ALL
    SELECT 'Never Gonna Give You Up',      213, 'dQw4w9WgXcQ' FROM dual UNION ALL
    SELECT 'Bohemian Rhapsody',            355, 'fJ9rUzIMcZQ' FROM dual UNION ALL
    SELECT 'Sweet Child O'' Mine',         356, '1w7OgIMMRc4' FROM dual UNION ALL
    SELECT 'November Rain',                537, '8SbUC-UaAxE' FROM dual UNION ALL
    SELECT 'Numb',                         187, 'kXYiU_JCYtU' FROM dual UNION ALL
    SELECT 'Billie Jean',                  294, 'Zi_XLOBDo_Y' FROM dual UNION ALL
    SELECT 'Blinding Lights',              201, '4NRXx6U8ABQ' FROM dual UNION ALL
    SELECT 'Despacito',                    282, 'kJQP7kiw5Fk' FROM dual
);

INSERT INTO track_artists (track_id, artist_id, artist_order)
SELECT t.id, a.id, m.artist_order
FROM (
    SELECT 'Thunderstruck' AS title, 'AC/DC' AS artist, 1 AS artist_order FROM dual UNION ALL
    SELECT 'Enter Sandman',           'Metallica',       1 FROM dual UNION ALL
    SELECT 'Smells Like Teen Spirit', 'Nirvana',         1 FROM dual UNION ALL
    SELECT 'Never Gonna Give You Up', 'Rick Astley',     1 FROM dual UNION ALL
    SELECT 'Bohemian Rhapsody',       'Queen',           1 FROM dual UNION ALL
    SELECT 'Sweet Child O'' Mine',    'Guns N'' Roses',  1 FROM dual UNION ALL
    SELECT 'November Rain',           'Guns N'' Roses',  1 FROM dual UNION ALL
    SELECT 'Numb',                    'Linkin Park',     1 FROM dual UNION ALL
    SELECT 'Billie Jean',             'Michael Jackson', 1 FROM dual UNION ALL
    SELECT 'Blinding Lights',         'The Weeknd',      1 FROM dual UNION ALL
    SELECT 'Despacito',               'Luis Fonsi',      1 FROM dual UNION ALL
    SELECT 'Despacito',               'Daddy Yankee',    2 FROM dual
) m
JOIN tracks t ON t.title = m.title
JOIN artists a ON a.name = m.artist;

INSERT INTO rooms (name, created_by_id, is_private, invite_code)
SELECT r.name, u.id, r.is_private, r.invite_code
FROM (
    SELECT 'Rock & Metal Bar' AS name, 'user_owner' AS creator, 0 AS is_private, 'RCKBAR' AS invite_code FROM dual UNION ALL
    SELECT 'Jazz Lounge',              'jazz_host',             1,               'JZZ7Q2'                FROM dual UNION ALL
    SELECT 'Lo-Fi Study',              'user_7',                0,               'LFSTDY'                FROM dual
) r
JOIN users u ON u.username = r.creator;

INSERT INTO playlists (name, room_id)
SELECT 'Queue: ' || name, id FROM rooms;

INSERT INTO playlists (name, owner_id)
SELECT 'Road trip', id FROM users WHERE username = 'user_1';

INSERT INTO room_members (room_id, user_id, member_role, joined_at, left_at, banned_until)
SELECT r.id, u.id, m.member_role,
       SYS_EXTRACT_UTC(SYSTIMESTAMP) - NUMTODSINTERVAL(m.joined_min_ago, 'MINUTE'),
       CASE WHEN m.left_min_ago IS NOT NULL
            THEN SYS_EXTRACT_UTC(SYSTIMESTAMP) - NUMTODSINTERVAL(m.left_min_ago, 'MINUTE') END,
       m.banned_until
FROM (
    SELECT 'Rock & Metal Bar' AS room, 'user_owner' AS username, 'OWNER' AS member_role,
           90 AS joined_min_ago, CAST(NULL AS NUMBER) AS left_min_ago, CAST(NULL AS TIMESTAMP) AS banned_until FROM dual UNION ALL
    SELECT 'Rock & Metal Bar', 'user_mod',     'MODERATOR',   85, NULL, NULL FROM dual UNION ALL
    SELECT 'Rock & Metal Bar', 'user_1',       'CONTRIBUTOR', 80, NULL, NULL FROM dual UNION ALL
    SELECT 'Rock & Metal Bar', 'user_2',       'CONTRIBUTOR', 75, NULL, NULL FROM dual UNION ALL
    SELECT 'Rock & Metal Bar', 'user_4',       'CONTRIBUTOR', 70, NULL, NULL FROM dual UNION ALL
    SELECT 'Rock & Metal Bar', 'user_5',       'CONTRIBUTOR', 65, NULL, NULL FROM dual UNION ALL
    SELECT 'Rock & Metal Bar', 'user_6',       'CONTRIBUTOR', 60, NULL, NULL FROM dual UNION ALL
    SELECT 'Rock & Metal Bar', 'user_spammer', 'CONTRIBUTOR', 30, NULL, NULL FROM dual UNION ALL
    SELECT 'Jazz Lounge',      'jazz_host',    'OWNER',       120, NULL, NULL FROM dual UNION ALL
    SELECT 'Jazz Lounge',      'user_deleted', 'CONTRIBUTOR', 110, NULL, NULL FROM dual UNION ALL
    SELECT 'Jazz Lounge',      'user_7',       'CONTRIBUTOR', 200, 150, TIMESTAMP '9999-12-31 00:00:00' FROM dual
) m
JOIN rooms r ON r.name = m.room
JOIN users u ON u.username = m.username;

INSERT INTO playlist_tracks (playlist_id, track_id, added_by_id, added_at, status, pinned_at, started_at, position)
SELECT p.id, t.id, u.id,
       SYS_EXTRACT_UTC(SYSTIMESTAMP) - NUMTODSINTERVAL(i.added_min_ago, 'MINUTE'),
       i.status,
       CASE WHEN i.pinned_min_ago IS NOT NULL
            THEN SYS_EXTRACT_UTC(SYSTIMESTAMP) - NUMTODSINTERVAL(i.pinned_min_ago, 'MINUTE') END,
       CASE WHEN i.started_min_ago IS NOT NULL
            THEN SYS_EXTRACT_UTC(SYSTIMESTAMP) - NUMTODSINTERVAL(i.started_min_ago, 'MINUTE') END,
       i.position
FROM (
    SELECT 'Queue: Rock & Metal Bar' AS playlist, 'Bohemian Rhapsody' AS title, 'user_owner' AS added_by, 'PLAYED' AS status,
           88 AS added_min_ago, CAST(NULL AS NUMBER) AS pinned_min_ago, 20 AS started_min_ago, CAST(NULL AS NUMBER) AS position FROM dual UNION ALL
    SELECT 'Queue: Rock & Metal Bar', 'November Rain',           'user_mod',     'SKIPPED', 84, NULL, 14,   NULL FROM dual UNION ALL
    SELECT 'Queue: Rock & Metal Bar', 'Sweet Child O'' Mine',    'user_4',       'PLAYED',  70, NULL, 12,   NULL FROM dual UNION ALL
    SELECT 'Queue: Rock & Metal Bar', 'Thunderstruck',           'user_owner',   'PLAYING', 60, NULL, 2,    NULL FROM dual UNION ALL
    SELECT 'Queue: Rock & Metal Bar', 'Enter Sandman',           'user_owner',   'QUEUED',  50, 40,   NULL, NULL FROM dual UNION ALL
    SELECT 'Queue: Rock & Metal Bar', 'Smells Like Teen Spirit', 'user_1',       'QUEUED',  45, NULL, NULL, NULL FROM dual UNION ALL
    SELECT 'Queue: Rock & Metal Bar', 'Never Gonna Give You Up', 'user_2',       'QUEUED',  42, NULL, NULL, NULL FROM dual UNION ALL
    SELECT 'Queue: Rock & Metal Bar', 'Numb',                    'user_spammer', 'QUEUED',  25, NULL, NULL, NULL FROM dual UNION ALL
    SELECT 'Queue: Jazz Lounge',      'Billie Jean',             'jazz_host',    'PLAYING', 30, NULL, 1,    NULL FROM dual UNION ALL
    SELECT 'Queue: Jazz Lounge',      'Blinding Lights',         'user_deleted', 'QUEUED',  20, NULL, NULL, NULL FROM dual UNION ALL
    SELECT 'Road trip',               'Despacito',               'user_1',       'QUEUED',  300, NULL, NULL, 1   FROM dual UNION ALL
    SELECT 'Road trip',               'Numb',                    'user_1',       'QUEUED',  299, NULL, NULL, 2   FROM dual UNION ALL
    SELECT 'Road trip',               'Thunderstruck',           'user_1',       'QUEUED',  298, NULL, NULL, 3   FROM dual
) i
JOIN playlists p ON p.name = i.playlist
JOIN tracks t ON t.title = i.title
JOIN users u ON u.username = i.added_by;

INSERT INTO track_votes (playlist_track_id, user_id, vote_value)
SELECT pt.id, pt.added_by_id, 1
FROM playlist_tracks pt
JOIN playlists p ON p.id = pt.playlist_id
WHERE p.room_id IS NOT NULL;

INSERT INTO track_votes (playlist_track_id, user_id, vote_value)
SELECT pt.id, u.id, v.vote_value
FROM (
    SELECT 'Smells Like Teen Spirit' AS title, 'user_owner' AS username, 1 AS vote_value FROM dual UNION ALL
    SELECT 'Smells Like Teen Spirit', 'user_mod', 1  FROM dual UNION ALL
    SELECT 'Smells Like Teen Spirit', 'user_2',   1  FROM dual UNION ALL
    SELECT 'Smells Like Teen Spirit', 'user_4',   1  FROM dual UNION ALL
    SELECT 'Never Gonna Give You Up', 'user_mod', -1 FROM dual UNION ALL
    SELECT 'Never Gonna Give You Up', 'user_4',   -1 FROM dual UNION ALL
    SELECT 'Never Gonna Give You Up', 'user_5',   -1 FROM dual UNION ALL
    SELECT 'Never Gonna Give You Up', 'user_6',   -1 FROM dual UNION ALL
    SELECT 'Never Gonna Give You Up', 'user_1',   -1 FROM dual UNION ALL
    SELECT 'Numb',                    'user_5',   1  FROM dual
) v
JOIN tracks t ON t.title = v.title
JOIN playlist_tracks pt ON pt.track_id = t.id AND pt.status = 'QUEUED'
JOIN playlists p ON p.id = pt.playlist_id AND p.name = 'Queue: Rock & Metal Bar'
JOIN users u ON u.username = v.username;

INSERT INTO audit_logs (created_at, source, user_id, action, entity_type, entity_id,
                        http_method, path, status_code, ip_address, duration_ms, details)
SELECT SYS_EXTRACT_UTC(SYSTIMESTAMP) - NUMTODSINTERVAL(a.min_ago, 'MINUTE'),
       a.source, u.id, a.action, a.entity_type, a.entity_id,
       a.http_method, a.path, a.status_code, a.ip_address, a.duration_ms, a.details
FROM (
    SELECT 95 AS min_ago, 'HTTP' AS source, 'user_owner' AS username, 'Auth.Login' AS action,
           CAST(NULL AS VARCHAR2(64)) AS entity_type, CAST(NULL AS VARCHAR2(64)) AS entity_id,
           'POST' AS http_method, '/api/auth/login' AS path, 200 AS status_code,
           '172.18.0.1' AS ip_address, 41 AS duration_ms, CAST(NULL AS VARCHAR2(200)) AS details FROM dual UNION ALL
    SELECT 92, 'HTTP', 'user_owner', 'Room.Create', 'Room', '1', 'POST', '/api/rooms', 201, '172.18.0.1', 58,
           '{"name":"Rock & Metal Bar","isPrivate":false}' FROM dual UNION ALL
    SELECT 50, 'HTTP', 'user_owner', 'Queue.Pin', 'QueueItem', NULL, 'PATCH', '/api/rooms/1/queue/items/pin', 204, '172.18.0.1', 23, NULL FROM dual UNION ALL
    SELECT 35, 'HTTP', 'user_3', 'Auth.LoginFailed', NULL, NULL, 'POST', '/api/auth/login', 401, '172.18.0.7', 112,
           '{"reason":"invalid_password"}' FROM dual UNION ALL
    SELECT 14, 'HUB', 'user_mod', 'Playback.Skip', 'QueueItem', NULL, NULL, NULL, NULL, NULL, 9, NULL FROM dual UNION ALL
    SELECT 5, 'SYSTEM', NULL, 'Room.AutoCloseCheck', 'Room', NULL, NULL, NULL, NULL, NULL, 3,
           '{"closed":0}' FROM dual
) a
LEFT JOIN users u ON u.username = a.username;

COMMIT;

SET FEEDBACK ON
COLUMN title FORMAT A26
COLUMN added_by FORMAT A12
COLUMN pin FORMAT A6
PROMPT Queue of "Rock & Metal Bar" in play order
SELECT ROW_NUMBER() OVER (ORDER BY pt.pinned_at NULLS LAST, NVL(s.score, 0) DESC, pt.added_at) AS pos,
       t.title, u.username AS added_by, NVL(s.score, 0) AS score,
       CASE WHEN pt.pinned_at IS NOT NULL THEN 'pinned' END AS pin
FROM playlist_tracks pt
JOIN playlists p ON p.id = pt.playlist_id
JOIN tracks t ON t.id = pt.track_id
JOIN users u ON u.id = pt.added_by_id
LEFT JOIN (SELECT playlist_track_id, SUM(vote_value) AS score FROM track_votes GROUP BY playlist_track_id) s
       ON s.playlist_track_id = pt.id
WHERE p.name = 'Queue: Rock & Metal Bar' AND pt.status = 'QUEUED'
ORDER BY pos;

EXIT
