-- constraints_test.sql
-- Schema constraint tests: every case must fail with the expected ORA code or succeed.
-- Run as SOUNDSHARE after migrations. All changes are rolled back; non-zero exit code on failure.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET DEFINE OFF
SET FEEDBACK OFF
SET SERVEROUTPUT ON SIZE UNLIMITED

DECLARE
    c_unique      CONSTANT PLS_INTEGER := -1;
    c_not_null    CONSTANT PLS_INTEGER := -1400;
    c_check       CONSTANT PLS_INTEGER := -2290;
    c_no_parent   CONSTANT PLS_INTEGER := -2291;
    c_has_child   CONSTANT PLS_INTEGER := -2292;
    c_identity    CONSTANT PLS_INTEGER := -32795;
    c_bad_year    CONSTANT PLS_INTEGER := -1841;

    v_passed      PLS_INTEGER := 0;
    v_failed      PLS_INTEGER := 0;

    v_user1       users.id%TYPE;
    v_user2       users.id%TYPE;
    v_user3       users.id%TYPE;
    v_artist      artists.id%TYPE;
    v_artist2     artists.id%TYPE;
    v_track       tracks.id%TYPE;
    v_spare_track tracks.id%TYPE;
    v_room_a      rooms.id%TYPE;
    v_room_b      rooms.id%TYPE;
    v_queue_a     playlists.id%TYPE;
    v_queue_b     playlists.id%TYPE;
    v_personal    playlists.id%TYPE;
    v_item        playlist_tracks.id%TYPE;

    PROCEDURE report(p_name IN VARCHAR2, p_ok IN BOOLEAN, p_detail IN VARCHAR2) IS
    BEGIN
        IF p_ok THEN
            v_passed := v_passed + 1;
            DBMS_OUTPUT.PUT_LINE('PASS  ' || p_name);
        ELSE
            v_failed := v_failed + 1;
            DBMS_OUTPUT.PUT_LINE('FAIL  ' || p_name || '  (' || p_detail || ')');
        END IF;
    END report;

    FUNCTION ora(p_code IN PLS_INTEGER) RETURN VARCHAR2 IS
    BEGIN
        RETURN 'ORA-' || LPAD(ABS(p_code), 5, '0');
    END ora;

    PROCEDURE expect_error(p_name IN VARCHAR2, p_sql IN VARCHAR2, p_code IN PLS_INTEGER) IS
    BEGIN
        SAVEPOINT test_case;
        EXECUTE IMMEDIATE p_sql;
        ROLLBACK TO test_case;
        report(p_name, FALSE, 'expected ' || ora(p_code) || ', statement succeeded');
    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK TO test_case;
            report(p_name, SQLCODE = p_code, 'expected ' || ora(p_code) || ', got ' || ora(SQLCODE));
    END expect_error;

    PROCEDURE expect_ok(p_name IN VARCHAR2, p_sql IN VARCHAR2) IS
    BEGIN
        EXECUTE IMMEDIATE p_sql;
        report(p_name, TRUE, NULL);
    EXCEPTION
        WHEN OTHERS THEN
            report(p_name, FALSE, 'unexpected ' || ora(SQLCODE));
    END expect_ok;

    PROCEDURE expect_count(p_name IN VARCHAR2, p_sql IN VARCHAR2, p_expected IN PLS_INTEGER) IS
        v_actual PLS_INTEGER;
    BEGIN
        EXECUTE IMMEDIATE p_sql INTO v_actual;
        report(p_name, v_actual = p_expected, 'expected ' || p_expected || ', got ' || v_actual);
    END expect_count;

    FUNCTION vote_merge(p_item IN NUMBER, p_user IN NUMBER, p_value IN NUMBER, p_keep_old IN BOOLEAN) RETURN VARCHAR2 IS
    BEGIN
        RETURN 'MERGE INTO track_votes v'
            || ' USING (SELECT ' || p_item || ' AS pt, ' || p_user || ' AS u, ' || p_value || ' AS val FROM dual) s'
            || ' ON (v.playlist_track_id = s.pt AND v.user_id = s.u)'
            || ' WHEN MATCHED THEN UPDATE SET v.vote_value = '
            || CASE WHEN p_keep_old THEN 'CASE WHEN s.val = 0 THEN v.vote_value ELSE s.val END' ELSE 's.val' END
            || ' DELETE WHERE s.val = 0'
            || ' WHEN NOT MATCHED THEN INSERT (playlist_track_id, user_id, vote_value)'
            || ' VALUES (s.pt, s.u, s.val) WHERE s.val <> 0';
    END vote_merge;
BEGIN
    INSERT INTO users (username, email, password_hash) VALUES ('t_user1', 't_user1@example.com', 'h') RETURNING id INTO v_user1;
    INSERT INTO users (username, email, password_hash) VALUES ('t_user2', 't_user2@example.com', 'h') RETURNING id INTO v_user2;
    INSERT INTO users (username, email, password_hash) VALUES ('t_user3', 't_user3@example.com', 'h') RETURNING id INTO v_user3;
    INSERT INTO artists (name) VALUES ('Test Artist') RETURNING id INTO v_artist;
    INSERT INTO artists (name) VALUES ('Test Guest') RETURNING id INTO v_artist2;
    INSERT INTO tracks (title, duration_sec, external_source_id) VALUES ('Test Song', 200, 'aaaaaaaaaaa') RETURNING id INTO v_track;
    INSERT INTO tracks (title, duration_sec, external_source_id) VALUES ('Spare Song', 200, 'bbbbbbbbbbb') RETURNING id INTO v_spare_track;
    INSERT INTO track_artists (track_id, artist_id, artist_order) VALUES (v_track, v_artist, 1);
    INSERT INTO track_artists (track_id, artist_id, artist_order) VALUES (v_spare_track, v_artist, 1);
    INSERT INTO rooms (name, created_by_id, invite_code) VALUES ('Test Room A', v_user1, 'TSTAAA') RETURNING id INTO v_room_a;
    INSERT INTO rooms (name, created_by_id, invite_code) VALUES ('Test Room B', v_user1, 'TSTBBB') RETURNING id INTO v_room_b;
    INSERT INTO playlists (name, room_id) VALUES ('Queue A', v_room_a) RETURNING id INTO v_queue_a;
    INSERT INTO playlists (name, room_id) VALUES ('Queue B', v_room_b) RETURNING id INTO v_queue_b;
    INSERT INTO playlists (name, owner_id) VALUES ('Personal', v_user1) RETURNING id INTO v_personal;
    INSERT INTO playlist_tracks (playlist_id, track_id, added_by_id) VALUES (v_queue_a, v_track, v_user1) RETURNING id INTO v_item;

    DBMS_OUTPUT.PUT_LINE('--- USERS');
    expect_error('username is unique ignoring case',
        'INSERT INTO users (username, email, password_hash) VALUES (''T_USER1'', ''x1@example.com'', ''h'')', c_unique);
    expect_error('email is unique ignoring case',
        'INSERT INTO users (username, email, password_hash) VALUES (''other1'', ''T_USER1@EXAMPLE.COM'', ''h'')', c_unique);
    expect_error('empty email is NULL in Oracle',
        'INSERT INTO users (username, email, password_hash) VALUES (''other2'', '''', ''h'')', c_not_null);
    expect_error('username shorter than 3 characters',
        'INSERT INTO users (username, email, password_hash) VALUES (''ab'', ''ab@example.com'', ''h'')', c_check);
    expect_error('email without @',
        'INSERT INTO users (username, email, password_hash) VALUES (''other3'', ''example.com'', ''h'')', c_check);
    expect_error('is_active outside 0/1',
        'INSERT INTO users (username, email, password_hash, is_active) VALUES (''other4'', ''o4@example.com'', ''h'', 2)', c_check);
    expect_error('role must exist',
        'INSERT INTO users (username, email, password_hash, role_id) VALUES (''other5'', ''o5@example.com'', ''h'', 9)', c_no_parent);
    expect_error('identity column rejects explicit value',
        'INSERT INTO users (id, username, email, password_hash) VALUES (1, ''other6'', ''o6@example.com'', ''h'')', c_identity);
    expect_count('omitted role gets default User',
        'SELECT role_id FROM users WHERE id = ' || v_user1, 2);

    DBMS_OUTPUT.PUT_LINE('--- CATALOG');
    expect_error('blank artist name',
        'INSERT INTO artists (name) VALUES (''   '')', c_check);
    expect_error('artist name is unique ignoring case',
        'INSERT INTO artists (name) VALUES (''TEST ARTIST'')', c_unique);
    expect_error('track duration must be positive',
        'INSERT INTO tracks (title, duration_sec, external_source_id) VALUES (''X'', 0, ''ccccccccccc'')', c_check);
    expect_error('YouTube id must have 11 characters',
        'INSERT INTO tracks (title, duration_sec, external_source_id) VALUES (''X'', 10, ''short'')', c_check);
    expect_error('same external video twice',
        'INSERT INTO tracks (title, duration_sec, external_source_id) VALUES (''X'', 10, ''aaaaaaaaaaa'')', c_unique);
    expect_error('two artists with the same order in a track',
        'INSERT INTO track_artists (track_id, artist_id, artist_order) VALUES (' || v_track || ', ' || v_artist2 || ', 1)', c_unique);
    expect_ok('featured artist with order 2',
        'INSERT INTO track_artists (track_id, artist_id, artist_order) VALUES (' || v_track || ', ' || v_artist2 || ', 2)');
    expect_error('artist with tracks cannot be deleted',
        'DELETE FROM artists WHERE id = ' || v_artist, c_has_child);
    expect_ok('deleting a track cascades to its artists',
        'DELETE FROM tracks WHERE id = ' || v_spare_track);
    expect_count('no track_artists rows left for the deleted track',
        'SELECT COUNT(*) FROM track_artists WHERE track_id = ' || v_spare_track, 0);

    DBMS_OUTPUT.PUT_LINE('--- ROOMS');
    expect_error('invite code with digit 0',
        'INSERT INTO rooms (name, created_by_id, invite_code) VALUES (''X'', ' || v_user1 || ', ''AB0CDE'')', c_check);
    expect_error('invite code in lower case',
        'INSERT INTO rooms (name, created_by_id, invite_code) VALUES (''X'', ' || v_user1 || ', ''abcdef'')', c_check);
    expect_error('invite code shorter than 6 is blank-padded and rejected',
        'INSERT INTO rooms (name, created_by_id, invite_code) VALUES (''X'', ' || v_user1 || ', ''ABC'')', c_check);
    expect_error('invite code is unique',
        'INSERT INTO rooms (name, created_by_id, invite_code) VALUES (''X'', ' || v_user1 || ', ''TSTAAA'')', c_unique);

    DBMS_OUTPUT.PUT_LINE('--- ROOM_MEMBERS');
    expect_ok('user joins room A as owner',
        'INSERT INTO room_members (room_id, user_id, member_role) VALUES (' || v_room_a || ', ' || v_user1 || ', ''OWNER'')');
    expect_error('user cannot be active in two rooms',
        'INSERT INTO room_members (room_id, user_id) VALUES (' || v_room_b || ', ' || v_user1 || ')', c_unique);
    expect_error('second active owner of the same room',
        'INSERT INTO room_members (room_id, user_id, member_role) VALUES (' || v_room_a || ', ' || v_user2 || ', ''OWNER'')', c_unique);
    expect_error('room role outside the list',
        'INSERT INTO room_members (room_id, user_id, member_role) VALUES (' || v_room_a || ', ' || v_user2 || ', ''ADMIN'')', c_check);
    expect_error('active member cannot be banned',
        'INSERT INTO room_members (room_id, user_id, banned_until) VALUES (' || v_room_a || ', ' || v_user2 || ', SYSTIMESTAMP + 1)', c_check);
    expect_error('ban must end after leaving',
        'INSERT INTO room_members (room_id, user_id, joined_at, left_at, banned_until) VALUES (' || v_room_a || ', ' || v_user2
        || ', TIMESTAMP ''2026-01-01 10:00:00'', TIMESTAMP ''2026-01-01 12:00:00'', TIMESTAMP ''2026-01-01 11:00:00'')', c_check);
    expect_ok('user leaves room A',
        'UPDATE room_members SET left_at = SYS_EXTRACT_UTC(SYSTIMESTAMP) WHERE room_id = ' || v_room_a || ' AND user_id = ' || v_user1);
    expect_ok('after leaving the user joins room B',
        'INSERT INTO room_members (room_id, user_id) VALUES (' || v_room_b || ', ' || v_user1 || ')');
    expect_ok('after the owner left a new owner is allowed',
        'INSERT INTO room_members (room_id, user_id, member_role) VALUES (' || v_room_a || ', ' || v_user2 || ', ''OWNER'')');
    expect_error('banned_until with 7 fractional digits overflows TIMESTAMP(6)',
        'INSERT INTO room_members (room_id, user_id, joined_at, left_at, banned_until) VALUES (' || v_room_b || ', ' || v_user3
        || ', TIMESTAMP ''2026-01-01 10:00:00'', TIMESTAMP ''2026-01-01 12:00:00'', TIMESTAMP ''9999-12-31 23:59:59.9999999'')', c_bad_year);

    DBMS_OUTPUT.PUT_LINE('--- PLAYLISTS');
    expect_error('playlist with both room and owner',
        'INSERT INTO playlists (name, room_id, owner_id) VALUES (''X'', ' || v_room_a || ', ' || v_user1 || ')', c_check);
    expect_error('playlist with neither room nor owner',
        'INSERT INTO playlists (name) VALUES (''X'')', c_check);
    expect_error('second queue for the same room',
        'INSERT INTO playlists (name, room_id) VALUES (''X'', ' || v_room_a || ')', c_unique);

    DBMS_OUTPUT.PUT_LINE('--- PLAYLIST_TRACKS');
    expect_error('unknown status',
        'INSERT INTO playlist_tracks (playlist_id, track_id, added_by_id, status) VALUES (' || v_queue_a || ', ' || v_track || ', ' || v_user1 || ', ''PAUSED'')', c_check);
    expect_error('PLAYING without started_at',
        'INSERT INTO playlist_tracks (playlist_id, track_id, added_by_id, status) VALUES (' || v_queue_a || ', ' || v_track || ', ' || v_user1 || ', ''PLAYING'')', c_check);
    expect_error('QUEUED with started_at',
        'INSERT INTO playlist_tracks (playlist_id, track_id, added_by_id, started_at) VALUES (' || v_queue_a || ', ' || v_track || ', ' || v_user1 || ', SYSTIMESTAMP)', c_check);
    expect_ok('PLAYING item in queue A',
        'INSERT INTO playlist_tracks (playlist_id, track_id, added_by_id, status, started_at) VALUES (' || v_queue_a || ', ' || v_track || ', ' || v_user1 || ', ''PLAYING'', SYSTIMESTAMP)');
    expect_error('second PLAYING item in queue A',
        'INSERT INTO playlist_tracks (playlist_id, track_id, added_by_id, status, started_at) VALUES (' || v_queue_a || ', ' || v_track || ', ' || v_user1 || ', ''PLAYING'', SYSTIMESTAMP)', c_unique);
    expect_ok('PLAYING item in queue B',
        'INSERT INTO playlist_tracks (playlist_id, track_id, added_by_id, status, started_at) VALUES (' || v_queue_b || ', ' || v_track || ', ' || v_user1 || ', ''PLAYING'', SYSTIMESTAMP)');
    expect_ok('several items without position in a queue',
        'INSERT INTO playlist_tracks (playlist_id, track_id, added_by_id) SELECT ' || v_queue_a || ', ' || v_track || ', ' || v_user1 || ' FROM dual CONNECT BY LEVEL <= 2');
    expect_ok('position 1 in personal playlist',
        'INSERT INTO playlist_tracks (playlist_id, track_id, added_by_id, position) VALUES (' || v_personal || ', ' || v_track || ', ' || v_user1 || ', 1)');
    expect_error('position 1 twice in personal playlist',
        'INSERT INTO playlist_tracks (playlist_id, track_id, added_by_id, position) VALUES (' || v_personal || ', ' || v_track || ', ' || v_user1 || ', 1)', c_unique);
    expect_error('position must be positive',
        'INSERT INTO playlist_tracks (playlist_id, track_id, added_by_id, position) VALUES (' || v_personal || ', ' || v_track || ', ' || v_user1 || ', 0)', c_check);
    expect_error('pin earlier than add',
        'INSERT INTO playlist_tracks (playlist_id, track_id, added_by_id, added_at, pinned_at) VALUES (' || v_queue_a || ', ' || v_track || ', ' || v_user1
        || ', TIMESTAMP ''2026-01-01 12:00:00'', TIMESTAMP ''2026-01-01 11:00:00'')', c_check);

    DBMS_OUTPUT.PUT_LINE('--- VOTES');
    expect_error('vote value 0',
        'INSERT INTO track_votes (playlist_track_id, user_id, vote_value) VALUES (' || v_item || ', ' || v_user1 || ', 0)', c_check);
    expect_error('vote value 2',
        'INSERT INTO track_votes (playlist_track_id, user_id, vote_value) VALUES (' || v_item || ', ' || v_user1 || ', 2)', c_check);
    expect_error('vote for a missing item',
        'INSERT INTO track_votes (playlist_track_id, user_id, vote_value) VALUES (-1, ' || v_user1 || ', 1)', c_no_parent);
    expect_ok('MERGE inserts a new vote', vote_merge(v_item, v_user1, 1, TRUE));
    expect_error('second vote by the same user',
        'INSERT INTO track_votes (playlist_track_id, user_id, vote_value) VALUES (' || v_item || ', ' || v_user1 || ', -1)', c_unique);
    expect_ok('MERGE changes the vote', vote_merge(v_item, v_user1, -1, TRUE));
    expect_count('vote changed to -1',
        'SELECT vote_value FROM track_votes WHERE playlist_track_id = ' || v_item || ' AND user_id = ' || v_user1, -1);
    expect_error('naive MERGE writes 0 before DELETE WHERE', vote_merge(v_item, v_user1, 0, FALSE), c_check);
    expect_ok('MERGE keeping the old value removes the vote', vote_merge(v_item, v_user1, 0, TRUE));
    expect_count('vote removed',
        'SELECT COUNT(*) FROM track_votes WHERE playlist_track_id = ' || v_item || ' AND user_id = ' || v_user1, 0);
    expect_ok('skip vote',
        'INSERT INTO skip_votes (playlist_track_id, user_id) VALUES (' || v_item || ', ' || v_user1 || ')');
    expect_error('second skip vote by the same user',
        'INSERT INTO skip_votes (playlist_track_id, user_id) VALUES (' || v_item || ', ' || v_user1 || ')', c_unique);
    expect_ok('deleting a playlist cascades to items, votes and skip votes',
        'DELETE FROM playlists WHERE id = ' || v_queue_a);
    expect_count('no skip votes left for the deleted item',
        'SELECT COUNT(*) FROM skip_votes WHERE playlist_track_id = ' || v_item, 0);

    DBMS_OUTPUT.PUT_LINE('--- AUDIT_LOGS AND TOKENS');
    expect_error('unknown audit source',
        'INSERT INTO audit_logs (source, action) VALUES (''CRON'', ''X'')', c_check);
    expect_error('HTTP entry without method, path and status',
        'INSERT INTO audit_logs (source, action) VALUES (''HTTP'', ''X'')', c_check);
    expect_error('status code outside 100-599',
        'INSERT INTO audit_logs (source, action, http_method, path, status_code) VALUES (''HTTP'', ''X'', ''GET'', ''/'', 999)', c_check);
    expect_error('details must be strict JSON',
        'INSERT INTO audit_logs (source, action, details) VALUES (''DOMAIN'', ''X'', ''{bad: json}'')', c_check);
    expect_ok('audit entry and refresh token of user 3',
        'BEGIN INSERT INTO audit_logs (source, user_id, action) VALUES (''DOMAIN'', ' || v_user3 || ', ''X'');'
        || ' INSERT INTO refresh_tokens (user_id, token_hash, expires_at) VALUES (' || v_user3 || ', HEXTORAW(RPAD(''AB'', 64, ''CD'')), SYSTIMESTAMP + 7); END;');
    expect_error('token must expire after creation',
        'INSERT INTO refresh_tokens (user_id, token_hash, expires_at) VALUES (' || v_user3 || ', HEXTORAW(RPAD(''EF'', 64, ''CD'')), SYSTIMESTAMP - 1)', c_check);
    expect_ok('deleting user 3 cascades tokens and keeps the audit entry',
        'DELETE FROM users WHERE id = ' || v_user3);
    expect_count('audit entry kept without user',
        'SELECT COUNT(*) FROM audit_logs WHERE action = ''X'' AND user_id IS NULL', 1);
    expect_count('tokens of deleted user removed',
        'SELECT COUNT(*) FROM refresh_tokens WHERE user_id = ' || v_user3, 0);

    ROLLBACK;

    DBMS_OUTPUT.PUT_LINE('---');
    DBMS_OUTPUT.PUT_LINE('Passed: ' || v_passed || ', failed: ' || v_failed);
    IF v_failed > 0 THEN
        RAISE_APPLICATION_ERROR(-20001, v_failed || ' constraint test(s) failed');
    END IF;
END;
/

EXIT
