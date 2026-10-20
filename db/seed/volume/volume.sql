-- volume.sql
-- Stage 7: synthetic volume for index experiments (about 1.9 million rows).
-- Run as SOUNDSHARE on an empty schema after migrations V001-V008, with the API stopped.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET SERVEROUTPUT ON
SET TIMING ON
SET FEEDBACK ON

DECLARE
    u0 NUMBER;
    a0 NUMBER;
    t0 NUMBER;
    r0 NUMBER;
    v_now TIMESTAMP := SYS_EXTRACT_UTC(SYSTIMESTAMP);
    c_alphabet CONSTANT VARCHAR2(32) := 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
BEGIN
    INSERT INTO users (username, email, password_hash, role_id)
    SELECT 'vol_' || LEVEL, 'vol_' || LEVEL || '@example.com', 'volume-hash', CASE WHEN LEVEL <= 3 THEN 1 ELSE 2 END
    FROM dual CONNECT BY LEVEL <= 20000;
    SELECT MIN(id) INTO u0 FROM users;

    INSERT INTO artists (name)
    SELECT 'Artist ' || LEVEL FROM dual CONNECT BY LEVEL <= 5000;
    SELECT MIN(id) INTO a0 FROM artists;

    INSERT INTO tracks (title, duration_sec, external_source_id)
    SELECT 'Track ' || LEVEL || ' ' || DBMS_RANDOM.STRING('U', 6), 120 + MOD(LEVEL * 37, 300), 'v' || LPAD(LEVEL, 10, '0')
    FROM dual CONNECT BY LEVEL <= 50000;
    SELECT MIN(id) INTO t0 FROM tracks;

    INSERT INTO track_artists (track_id, artist_id, artist_order)
    SELECT t0 + LEVEL - 1, a0 + MOD(LEVEL - 1, 5000), 1 FROM dual CONNECT BY LEVEL <= 50000;

    INSERT INTO rooms (name, created_by_id, is_private, invite_code)
    SELECT 'Room ' || LEVEL,
           u0 + LEVEL - 1,
           CASE WHEN MOD(LEVEL, 5) = 0 THEN 1 ELSE 0 END,
           SUBSTR(c_alphabet, MOD(TRUNC(LEVEL / 32768), 32) + 1, 1)
               || SUBSTR(c_alphabet, MOD(TRUNC(LEVEL / 1024), 32) + 1, 1)
               || SUBSTR(c_alphabet, MOD(TRUNC(LEVEL / 32), 32) + 1, 1)
               || SUBSTR(c_alphabet, MOD(LEVEL, 32) + 1, 1) || 'VV'
    FROM dual CONNECT BY LEVEL <= 2000;
    SELECT MIN(id) INTO r0 FROM rooms;

    INSERT INTO playlists (name, room_id)
    SELECT 'Queue: ' || name, id FROM rooms;

    INSERT INTO room_members (room_id, user_id, member_role, joined_at)
    SELECT r.id, r.created_by_id, 'OWNER', v_now - INTERVAL '1' DAY FROM rooms r;

    INSERT INTO room_members (room_id, user_id, member_role, joined_at)
    SELECT r.id, u0 + 2000 + (r.id - r0) * 5 + k.j, 'CONTRIBUTOR', v_now - INTERVAL '12' HOUR
    FROM rooms r CROSS JOIN (SELECT LEVEL - 1 AS j FROM dual CONNECT BY LEVEL <= 5) k;

    INSERT INTO room_members (room_id, user_id, member_role, joined_at, left_at)
    SELECT r.id, u0 + 12000 + MOD((r.id - r0) * 10 + k.j, 8000), 'CONTRIBUTOR',
           v_now - INTERVAL '3' DAY, v_now - INTERVAL '2' DAY
    FROM rooms r CROSS JOIN (SELECT LEVEL - 1 AS j FROM dual CONNECT BY LEVEL <= 10) k;

    INSERT INTO playlist_tracks (playlist_id, track_id, added_by_id, added_at, status, started_at)
    SELECT p.id,
           t0 + MOD((p.room_id - r0) * 160 + k.n, 50000),
           u0 + 2000 + (p.room_id - r0) * 5 + MOD(k.n, 5),
           v_now - NUMTODSINTERVAL(200 - k.n, 'MINUTE'),
           CASE WHEN k.n <= 150 THEN CASE WHEN MOD(k.n, 4) = 0 THEN 'SKIPPED' ELSE 'PLAYED' END
                WHEN k.n = 160 THEN 'PLAYING'
                ELSE 'QUEUED' END,
           CASE WHEN k.n <= 150 OR k.n = 160 THEN v_now - NUMTODSINTERVAL(199 - k.n, 'MINUTE') END
    FROM playlists p CROSS JOIN (SELECT LEVEL AS n FROM dual CONNECT BY LEVEL <= 160) k
    WHERE p.room_id IS NOT NULL;

    INSERT INTO track_votes (playlist_track_id, user_id, vote_value)
    SELECT pt.id,
           u0 + 2000 + (p.room_id - r0) * 5 + k.j,
           CASE WHEN MOD(pt.id + k.j, 3) = 0 THEN -1 ELSE 1 END
    FROM playlist_tracks pt
    JOIN playlists p ON p.id = pt.playlist_id
    CROSS JOIN (SELECT LEVEL - 1 AS j FROM dual CONNECT BY LEVEL <= 5) k
    WHERE k.j < CASE WHEN pt.status IN ('PLAYED', 'SKIPPED') THEN 3 ELSE 5 END;

    INSERT INTO audit_logs (created_at, source, user_id, action, http_method, path, status_code, ip_address, duration_ms)
    SELECT v_now - NUMTODSINTERVAL(MOD(LEVEL * 7919, 30 * 86400), 'SECOND'),
           'HTTP',
           u0 + MOD(LEVEL, 20000),
           CASE MOD(LEVEL, 12)
               WHEN 6 THEN 'Queue.AddTrack' WHEN 7 THEN 'Room.Join' WHEN 8 THEN 'Auth.Login'
               WHEN 9 THEN 'Auth.LoginFailed' WHEN 10 THEN 'Queue.PinFailed' WHEN 11 THEN 'Admin.BlockUser'
               ELSE 'Queue.Vote' END,
           CASE WHEN MOD(LEVEL, 12) < 6 THEN 'PUT' ELSE 'POST' END,
           '/api/rooms/' || MOD(LEVEL, 2000),
           CASE MOD(LEVEL, 12) WHEN 9 THEN 401 WHEN 10 THEN 403 WHEN 6 THEN 201 ELSE 200 END,
           '10.0.' || MOD(LEVEL, 250) || '.' || MOD(LEVEL, 200),
           5 + MOD(LEVEL, 90)
    FROM dual CONNECT BY LEVEL <= 500000;

    COMMIT;
    DBMS_STATS.GATHER_SCHEMA_STATS(ownname => USER, cascade => TRUE);
    DBMS_OUTPUT.PUT_LINE('Volume data loaded');
END;
/

SELECT 'USERS' AS tbl, COUNT(*) AS cnt FROM users UNION ALL
SELECT 'TRACKS', COUNT(*) FROM tracks UNION ALL
SELECT 'ROOMS', COUNT(*) FROM rooms UNION ALL
SELECT 'ROOM_MEMBERS', COUNT(*) FROM room_members UNION ALL
SELECT 'PLAYLIST_TRACKS', COUNT(*) FROM playlist_tracks UNION ALL
SELECT 'TRACK_VOTES', COUNT(*) FROM track_votes UNION ALL
SELECT 'AUDIT_LOGS', COUNT(*) FROM audit_logs;

EXIT
