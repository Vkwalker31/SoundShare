-- 04_dml.sql
-- Stage 4: DML of SoundShare business operations (INSERT, UPDATE, DELETE, MERGE) on demo data.
-- Run as SOUNDSHARE after seed/demo.sql. Everything is rolled back at the end, demo data stays unchanged.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET DEFINE OFF
SET SERVEROUTPUT ON
SET FEEDBACK ON
SET LINESIZE 200
SET PAGESIZE 100
COLUMN username FORMAT A16
COLUMN title FORMAT A26
COLUMN status FORMAT A13
COLUMN member_role FORMAT A12

VARIABLE user_id   NUMBER
VARIABLE room_id   NUMBER
VARIABLE queue_id  NUMBER
VARIABLE item_id   NUMBER
VARIABLE first_id  NUMBER
VARIABLE track_id  NUMBER
VARIABLE actor_id  NUMBER
VARIABLE rock_id   NUMBER
VARIABLE vote      NUMBER

PROMPT
PROMPT ===== 4.1 Регистрация пользователя: INSERT ... RETURNING =====
BEGIN
    INSERT INTO users (username, email, password_hash)
    VALUES ('new_listener', 'new_listener@example.com',
            'AQAAAAIAAYagAAAAEEpnK5MMjkNs58sFajENOfwBq3J9989pJ06MGEoSg7oTTVK7lE8QqE3TTjLcAzwz7w==')
    RETURNING id INTO :user_id;
END;
/
SELECT id, username, role_id, is_active FROM users WHERE id = :user_id;

PROMPT ===== 4.2 Выдача refresh-токена: в БД хранится только SHA-256 =====
INSERT INTO refresh_tokens (user_id, token_hash, expires_at, created_by_ip)
VALUES (:user_id, STANDARD_HASH('demo-refresh-token', 'SHA256'),
        SYS_EXTRACT_UTC(SYSTIMESTAMP) + INTERVAL '7' DAY, '127.0.0.1');

PROMPT ===== 4.3 Ротация refresh-токена: новый токен + отзыв старого со ссылкой на замену =====
DECLARE
    v_new_id refresh_tokens.id%TYPE;
BEGIN
    INSERT INTO refresh_tokens (user_id, token_hash, expires_at)
    VALUES (:user_id, STANDARD_HASH('demo-refresh-token-2', 'SHA256'), SYS_EXTRACT_UTC(SYSTIMESTAMP) + INTERVAL '7' DAY)
    RETURNING id INTO v_new_id;

    UPDATE refresh_tokens
    SET revoked_at = SYS_EXTRACT_UTC(SYSTIMESTAMP),
        replaced_by_token_id = v_new_id
    WHERE token_hash = STANDARD_HASH('demo-refresh-token', 'SHA256')
      AND revoked_at IS NULL;
    DBMS_OUTPUT.PUT_LINE('Revoked: ' || SQL%ROWCOUNT || ', replacement id: ' || v_new_id);
END;
/

PROMPT ===== 4.4 Создание комнаты: комната, очередь и участник-владелец в одной транзакции =====
BEGIN
    INSERT INTO rooms (name, created_by_id, is_private, invite_code)
    VALUES ('Indie Evening', :user_id, 0, 'NDV3EV')
    RETURNING id INTO :room_id;

    INSERT INTO playlists (name, room_id)
    VALUES ('Queue: Indie Evening', :room_id)
    RETURNING id INTO :queue_id;

    INSERT INTO room_members (room_id, user_id, member_role)
    VALUES (:room_id, :user_id, 'OWNER');
END;
/
SELECT r.id, r.name, r.invite_code, p.id AS queue_id FROM rooms r JOIN playlists p ON p.room_id = r.id WHERE r.id = :room_id;

PROMPT ===== 4.5 Вход в комнату: выйти из текущей комнаты, затем MERGE участника =====
BEGIN
    SELECT id INTO :actor_id FROM users WHERE username = 'user_1';
END;
/
UPDATE room_members
SET left_at = SYS_EXTRACT_UTC(SYSTIMESTAMP)
WHERE user_id = :actor_id
  AND left_at IS NULL
  AND room_id <> :room_id;

MERGE INTO room_members m
USING (SELECT :room_id AS room_id, :actor_id AS user_id FROM dual) s
ON (m.room_id = s.room_id AND m.user_id = s.user_id)
WHEN MATCHED THEN UPDATE
    SET m.left_at = NULL,
        m.banned_until = NULL,
        m.member_role = 'CONTRIBUTOR',
        m.joined_at = SYS_EXTRACT_UTC(SYSTIMESTAMP)
    WHERE m.left_at IS NOT NULL
      AND (m.banned_until IS NULL OR m.banned_until <= SYS_EXTRACT_UTC(SYSTIMESTAMP))
WHEN NOT MATCHED THEN
    INSERT (room_id, user_id) VALUES (s.room_id, s.user_id);

PROMPT ===== 4.6 Добавление трека с проверкой лимита 3 активных трека на пользователя =====
BEGIN
    SELECT id INTO :track_id FROM tracks WHERE external_source_id = 'kXYiU_JCYtU';
END;
/
INSERT INTO playlist_tracks (playlist_id, track_id, added_by_id)
SELECT :queue_id, :track_id, :user_id FROM dual
WHERE (SELECT COUNT(*) FROM playlist_tracks
       WHERE playlist_id = :queue_id AND added_by_id = :user_id AND status IN ('QUEUED', 'PLAYING')) < 3;
BEGIN
    SELECT MAX(id) INTO :first_id FROM playlist_tracks WHERE playlist_id = :queue_id AND added_by_id = :user_id;
END;
/
INSERT INTO track_votes (playlist_track_id, user_id, vote_value) VALUES (:first_id, :user_id, 1);

BEGIN
    SELECT id INTO :track_id FROM tracks WHERE external_source_id = 'fJ9rUzIMcZQ';
END;
/
INSERT INTO playlist_tracks (playlist_id, track_id, added_by_id)
SELECT :queue_id, :track_id, :actor_id FROM dual
WHERE (SELECT COUNT(*) FROM playlist_tracks
       WHERE playlist_id = :queue_id AND added_by_id = :actor_id AND status IN ('QUEUED', 'PLAYING')) < 3;
BEGIN
    SELECT MAX(id) INTO :item_id FROM playlist_tracks WHERE playlist_id = :queue_id AND added_by_id = :actor_id;
END;
/
INSERT INTO track_votes (playlist_track_id, user_id, vote_value) VALUES (:item_id, :actor_id, 1);

PROMPT ===== 4.7 Запуск воспроизведения, если ничего не играет: первый по правилам очереди трек =====
UPDATE playlist_tracks
SET status = 'PLAYING', started_at = SYS_EXTRACT_UTC(SYSTIMESTAMP), pinned_at = NULL
WHERE id = (
        SELECT pt.id
        FROM playlist_tracks pt
        WHERE pt.playlist_id = :queue_id AND pt.status = 'QUEUED'
        ORDER BY pt.pinned_at NULLS LAST,
                 (SELECT NVL(SUM(v.vote_value), 0) FROM track_votes v WHERE v.playlist_track_id = pt.id) DESC,
                 pt.added_at
        FETCH FIRST 1 ROW ONLY)
  AND NOT EXISTS (SELECT 1 FROM playlist_tracks WHERE playlist_id = :queue_id AND status = 'PLAYING');

UPDATE playlists SET queue_version = queue_version + 1 WHERE id = :queue_id;

PROMPT ===== 4.8 Голос: MERGE ставит -1, затем меняет на +1, затем снимает голос (значение 0) =====
EXEC :vote := -1
MERGE INTO track_votes v
USING (SELECT :item_id AS item_id, :user_id AS user_id, :vote AS vote_value FROM dual) s
ON (v.playlist_track_id = s.item_id AND v.user_id = s.user_id)
WHEN MATCHED THEN UPDATE
    SET v.vote_value = CASE WHEN s.vote_value = 0 THEN v.vote_value ELSE s.vote_value END,
        v.voted_at = SYS_EXTRACT_UTC(SYSTIMESTAMP)
    DELETE WHERE s.vote_value = 0
WHEN NOT MATCHED THEN
    INSERT (playlist_track_id, user_id, vote_value) VALUES (s.item_id, s.user_id, s.vote_value)
    WHERE s.vote_value <> 0;

EXEC :vote := 1
MERGE INTO track_votes v
USING (SELECT :item_id AS item_id, :user_id AS user_id, :vote AS vote_value FROM dual) s
ON (v.playlist_track_id = s.item_id AND v.user_id = s.user_id)
WHEN MATCHED THEN UPDATE
    SET v.vote_value = CASE WHEN s.vote_value = 0 THEN v.vote_value ELSE s.vote_value END,
        v.voted_at = SYS_EXTRACT_UTC(SYSTIMESTAMP)
    DELETE WHERE s.vote_value = 0
WHEN NOT MATCHED THEN
    INSERT (playlist_track_id, user_id, vote_value) VALUES (s.item_id, s.user_id, s.vote_value)
    WHERE s.vote_value <> 0;

EXEC :vote := 0
MERGE INTO track_votes v
USING (SELECT :item_id AS item_id, :user_id AS user_id, :vote AS vote_value FROM dual) s
ON (v.playlist_track_id = s.item_id AND v.user_id = s.user_id)
WHEN MATCHED THEN UPDATE
    SET v.vote_value = CASE WHEN s.vote_value = 0 THEN v.vote_value ELSE s.vote_value END,
        v.voted_at = SYS_EXTRACT_UTC(SYSTIMESTAMP)
    DELETE WHERE s.vote_value = 0
WHEN NOT MATCHED THEN
    INSERT (playlist_track_id, user_id, vote_value) VALUES (s.item_id, s.user_id, s.vote_value)
    WHERE s.vote_value <> 0;

SELECT COUNT(*) AS votes_of_new_listener FROM track_votes WHERE playlist_track_id = :item_id AND user_id = :user_id;

PROMPT ===== 4.9 Автоудаление: трек с рейтингом <= -5 получает статус AUTO_REMOVED =====
BEGIN
    SELECT id INTO :rock_id FROM rooms WHERE name = 'Rock & Metal Bar';
    SELECT id INTO :actor_id FROM users WHERE username = 'user_owner';
END;
/
INSERT INTO track_votes (playlist_track_id, user_id, vote_value)
SELECT pt.id, :actor_id, -1
FROM playlist_tracks pt
JOIN playlists p ON p.id = pt.playlist_id
JOIN tracks t ON t.id = pt.track_id
WHERE p.room_id = :rock_id AND t.title = 'Never Gonna Give You Up' AND pt.status = 'QUEUED';

UPDATE playlist_tracks pt
SET pt.status = 'AUTO_REMOVED'
WHERE pt.status = 'QUEUED'
  AND pt.pinned_at IS NULL
  AND (SELECT NVL(SUM(v.vote_value), 0) FROM track_votes v WHERE v.playlist_track_id = pt.id) <= -5;

PROMPT ===== 4.10 Закрепление трека: проходит только для модератора или владельца =====
BEGIN
    SELECT id INTO :actor_id FROM users WHERE username = 'user_1';
END;
/
PROMPT Попытка участника (Contributor) - 0 строк:
UPDATE playlist_tracks pt
SET pt.pinned_at = SYS_EXTRACT_UTC(SYSTIMESTAMP)
WHERE pt.id = :item_id
  AND pt.status = 'QUEUED'
  AND EXISTS (SELECT 1 FROM room_members m
              WHERE m.room_id = :room_id AND m.user_id = :actor_id
                AND m.left_at IS NULL AND m.member_role IN ('OWNER', 'MODERATOR'));
PROMPT Попытка владельца - 1 строка:
UPDATE playlist_tracks pt
SET pt.pinned_at = SYS_EXTRACT_UTC(SYSTIMESTAMP)
WHERE pt.id = :item_id
  AND pt.status = 'QUEUED'
  AND EXISTS (SELECT 1 FROM room_members m
              WHERE m.room_id = :room_id AND m.user_id = :user_id
                AND m.left_at IS NULL AND m.member_role IN ('OWNER', 'MODERATOR'));

PROMPT ===== 4.11 Голос за пропуск и пропуск при большинстве: PLAYING -> SKIPPED, следующий -> PLAYING =====
INSERT INTO skip_votes (playlist_track_id, user_id)
SELECT pt.id, :actor_id FROM playlist_tracks pt WHERE pt.playlist_id = :queue_id AND pt.status = 'PLAYING';
INSERT INTO skip_votes (playlist_track_id, user_id)
SELECT pt.id, :user_id FROM playlist_tracks pt WHERE pt.playlist_id = :queue_id AND pt.status = 'PLAYING';

UPDATE playlist_tracks pt
SET pt.status = 'SKIPPED'
WHERE pt.playlist_id = :queue_id
  AND pt.status = 'PLAYING'
  AND (SELECT COUNT(*) FROM skip_votes s WHERE s.playlist_track_id = pt.id) * 2 >
      (SELECT COUNT(*) FROM room_members m WHERE m.room_id = :room_id AND m.left_at IS NULL);

UPDATE playlist_tracks
SET status = 'PLAYING', started_at = SYS_EXTRACT_UTC(SYSTIMESTAMP), pinned_at = NULL
WHERE id = (
        SELECT pt.id
        FROM playlist_tracks pt
        WHERE pt.playlist_id = :queue_id AND pt.status = 'QUEUED'
        ORDER BY pt.pinned_at NULLS LAST,
                 (SELECT NVL(SUM(v.vote_value), 0) FROM track_votes v WHERE v.playlist_track_id = pt.id) DESC,
                 pt.added_at
        FETCH FIRST 1 ROW ONLY)
  AND NOT EXISTS (SELECT 1 FROM playlist_tracks WHERE playlist_id = :queue_id AND status = 'PLAYING');

SELECT pt.id, t.title, pt.status FROM playlist_tracks pt JOIN tracks t ON t.id = pt.track_id WHERE pt.playlist_id = :queue_id ORDER BY pt.id;

PROMPT ===== 4.12 Бан участника на 60 минут: выход из комнаты + срок бана =====
BEGIN
    SELECT id INTO :actor_id FROM users WHERE username = 'user_spammer';
END;
/
UPDATE room_members
SET left_at = SYS_EXTRACT_UTC(SYSTIMESTAMP),
    banned_until = SYS_EXTRACT_UTC(SYSTIMESTAMP) + INTERVAL '60' MINUTE
WHERE room_id = :rock_id AND user_id = :actor_id AND left_at IS NULL;

PROMPT ===== 4.13 Выход владельца: передача роли OWNER модератору или самому давнему участнику =====
UPDATE room_members
SET left_at = SYS_EXTRACT_UTC(SYSTIMESTAMP), member_role = 'CONTRIBUTOR'
WHERE room_id = :rock_id AND member_role = 'OWNER' AND left_at IS NULL;

UPDATE room_members
SET member_role = 'OWNER'
WHERE (room_id, user_id) = (
        SELECT room_id, user_id
        FROM room_members
        WHERE room_id = :rock_id AND left_at IS NULL
        ORDER BY CASE member_role WHEN 'MODERATOR' THEN 0 ELSE 1 END, joined_at
        FETCH FIRST 1 ROW ONLY);

SELECT u.username, m.member_role, CASE WHEN m.left_at IS NULL THEN 'online' ELSE 'left' END AS state
FROM room_members m JOIN users u ON u.id = m.user_id
WHERE m.room_id = :rock_id ORDER BY m.joined_at;

PROMPT ===== 4.14 Администратор блокирует пользователя и отзывает его refresh-токены =====
UPDATE users SET is_active = 0 WHERE id = :actor_id;
UPDATE refresh_tokens SET revoked_at = SYS_EXTRACT_UTC(SYSTIMESTAMP) WHERE user_id = :actor_id AND revoked_at IS NULL;

PROMPT ===== 4.15 Смена роли с защитой последнего администратора =====
PROMPT Снять роль Admin с единственного администратора - 0 строк:
UPDATE users
SET role_id = 2
WHERE username = 'admin'
  AND (SELECT COUNT(*) FROM users WHERE role_id = 1 AND is_active = 1) > 1;
UPDATE users SET role_id = 1 WHERE username = 'user_mod';
PROMPT После назначения второго администратора - 1 строка:
UPDATE users
SET role_id = 2
WHERE username = 'admin'
  AND (SELECT COUNT(*) FROM users WHERE role_id = 1 AND is_active = 1) > 1;

PROMPT ===== 4.16 Удаление аккаунта: анонимизация вместо физического удаления =====
UPDATE room_members
SET left_at = SYS_EXTRACT_UTC(SYSTIMESTAMP)
WHERE user_id = (SELECT id FROM users WHERE username = 'user_deleted') AND left_at IS NULL;
UPDATE users
SET username = 'deleted_' || id,
    email = 'deleted_' || id || '@deleted.invalid',
    password_hash = '!deleted-account',
    is_active = 0
WHERE username = 'user_deleted';

PROMPT ===== 4.17 Комнаты без участников: отметка EMPTY_SINCE и закрытие через 10 минут =====
UPDATE rooms r
SET r.empty_since = SYS_EXTRACT_UTC(SYSTIMESTAMP) - INTERVAL '11' MINUTE
WHERE r.is_active = 1
  AND r.empty_since IS NULL
  AND NOT EXISTS (SELECT 1 FROM room_members m WHERE m.room_id = r.id AND m.left_at IS NULL);
UPDATE rooms
SET is_active = 0
WHERE is_active = 1
  AND empty_since < SYS_EXTRACT_UTC(SYSTIMESTAMP) - INTERVAL '10' MINUTE;

PROMPT ===== 4.18 Личный плейлист: удаление трека и сдвиг позиций =====
DELETE FROM playlist_tracks
WHERE playlist_id = (SELECT id FROM playlists WHERE name = 'Road trip') AND position = 2;
UPDATE playlist_tracks
SET position = position - 1
WHERE playlist_id = (SELECT id FROM playlists WHERE name = 'Road trip') AND position > 2;
SELECT pt.position, t.title FROM playlist_tracks pt JOIN tracks t ON t.id = pt.track_id
WHERE pt.playlist_id = (SELECT id FROM playlists WHERE name = 'Road trip') ORDER BY pt.position;

PROMPT ===== 4.19 Удаление плейлиста: позиции удаляются каскадно (ON DELETE CASCADE) =====
DELETE FROM playlists WHERE name = 'Road trip';
SELECT COUNT(*) AS orphan_items FROM playlist_tracks pt WHERE NOT EXISTS (SELECT 1 FROM playlists p WHERE p.id = pt.playlist_id);

PROMPT ===== 4.20 Очистка просроченных и давно отозванных refresh-токенов =====
DELETE FROM refresh_tokens
WHERE expires_at < SYS_EXTRACT_UTC(SYSTIMESTAMP)
   OR revoked_at < SYS_EXTRACT_UTC(SYSTIMESTAMP) - INTERVAL '30' DAY;

PROMPT ===== 4.21 Запись в журнал аудита с JSON-деталями =====
INSERT INTO audit_logs (source, user_id, action, entity_type, entity_id, http_method, path, status_code, duration_ms, details)
VALUES ('HTTP', :user_id, 'Room.Create', 'Room', TO_CHAR(:room_id), 'POST', '/api/rooms', 201, 37,
        JSON_OBJECT('name' VALUE 'Indie Evening', 'isPrivate' VALUE 'false' FORMAT JSON));

ROLLBACK;
PROMPT
PROMPT Все изменения отменены (ROLLBACK), демо-данные не изменились.
EXIT
