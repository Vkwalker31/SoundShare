-- measure.sql
-- Stage 7: execution plans and measured cost of the application's hot queries on volume data.
-- Prints EXPLAIN PLAN for each query and a line "RESULT|<query>|<avg ms>|<logical reads per execution>".
-- Run as SOUNDSHARE after seed/volume/volume.sql (needs SELECT on V_$MYSTAT and V_$STATNAME, see header of the report).

SET DEFINE OFF
SET SERVEROUTPUT ON SIZE UNLIMITED
SET LINESIZE 200
SET PAGESIZE 0
SET FEEDBACK OFF
SET VERIFY OFF

VARIABLE p NUMBER
VARIABLE r NUMBER
VARIABLE u NUMBER
VARIABLE i NUMBER
BEGIN
    SELECT r.id, p.id INTO :r, :p FROM rooms r JOIN playlists p ON p.room_id = r.id WHERE r.name = 'Room 1000';
    SELECT MIN(user_id) INTO :u FROM room_members WHERE room_id = :r AND member_role = 'CONTRIBUTOR' AND left_at IS NULL;
    SELECT MIN(id) INTO :i FROM playlist_tracks WHERE playlist_id = :p AND status = 'QUEUED';
END;
/

DELETE FROM plan_table;
PROMPT ===== Q1. Активные позиции очереди комнаты =====
EXPLAIN PLAN SET STATEMENT_ID = 'Q1' FOR
SELECT pt.id, pt.status, pt.added_at, pt.pinned_at FROM playlist_tracks pt WHERE pt.playlist_id = :p AND pt.status IN ('QUEUED', 'PLAYING');
SELECT plan_table_output FROM TABLE(DBMS_XPLAN.DISPLAY(NULL, 'Q1', 'BASIC +ROWS +COST +PREDICATE'));

PROMPT ===== Q2. Число участников комнаты онлайн =====
EXPLAIN PLAN SET STATEMENT_ID = 'Q2' FOR
SELECT room_id, user_id FROM room_members WHERE room_id = :r AND left_at IS NULL;
SELECT plan_table_output FROM TABLE(DBMS_XPLAN.DISPLAY(NULL, 'Q2', 'BASIC +ROWS +COST +PREDICATE'));

PROMPT ===== Q3. Журнал: последние 50 записей по действию (keyset) =====
EXPLAIN PLAN SET STATEMENT_ID = 'Q3' FOR
SELECT id, created_at, user_id, status_code FROM audit_logs WHERE action = 'Admin.BlockUser' ORDER BY id DESC FETCH FIRST 50 ROWS ONLY;
SELECT plan_table_output FROM TABLE(DBMS_XPLAN.DISPLAY(NULL, 'Q3', 'BASIC +ROWS +COST +PREDICATE'));

PROMPT ===== Q4. Журнал за последний час =====
EXPLAIN PLAN SET STATEMENT_ID = 'Q4' FOR
SELECT id, action, status_code FROM audit_logs WHERE created_at >= SYS_EXTRACT_UTC(SYSTIMESTAMP) - INTERVAL '1' HOUR;
SELECT plan_table_output FROM TABLE(DBMS_XPLAN.DISPLAY(NULL, 'Q4', 'BASIC +ROWS +COST +PREDICATE'));

PROMPT ===== Q5. Поиск трека по началу названия =====
EXPLAIN PLAN SET STATEMENT_ID = 'Q5' FOR
SELECT id, title FROM tracks WHERE UPPER(title) LIKE 'TRACK 4999%';
SELECT plan_table_output FROM TABLE(DBMS_XPLAN.DISPLAY(NULL, 'Q5', 'BASIC +ROWS +COST +PREDICATE'));

PROMPT ===== Q6. Активные треки пользователя в очереди (лимит 3) =====
EXPLAIN PLAN SET STATEMENT_ID = 'Q6' FOR
SELECT id FROM playlist_tracks WHERE playlist_id = :p AND added_by_id = :u AND status IN ('QUEUED', 'PLAYING');
SELECT plan_table_output FROM TABLE(DBMS_XPLAN.DISPLAY(NULL, 'Q6', 'BASIC +ROWS +COST +PREDICATE'));

PROMPT ===== Q7. Рейтинг позиции (сумма голосов) =====
EXPLAIN PLAN SET STATEMENT_ID = 'Q7' FOR
SELECT vote_value FROM track_votes WHERE playlist_track_id = :i;
SELECT plan_table_output FROM TABLE(DBMS_XPLAN.DISPLAY(NULL, 'Q7', 'BASIC +ROWS +COST +PREDICATE'));

PROMPT ===== Q8. Закончившиеся треки для автоперехода =====
EXPLAIN PLAN SET STATEMENT_ID = 'Q8' FOR
SELECT pt.id, pt.playlist_id FROM playlist_tracks pt JOIN tracks t ON t.id = pt.track_id WHERE pt.status = 'PLAYING' AND pt.started_at + NUMTODSINTERVAL(t.duration_sec, 'SECOND') <= SYS_EXTRACT_UTC(SYSTIMESTAMP);
SELECT plan_table_output FROM TABLE(DBMS_XPLAN.DISPLAY(NULL, 'Q8', 'BASIC +ROWS +COST +PREDICATE'));

DECLARE
    v_p NUMBER := :p;
    v_r NUMBER := :r;
    v_u NUMBER := :u;
    v_i NUMBER := :i;
    c_runs CONSTANT PLS_INTEGER := 30;

    FUNCTION logical_reads RETURN NUMBER IS
        v NUMBER;
    BEGIN
        SELECT m.value INTO v
        FROM v$mystat m JOIN v$statname n ON n.statistic# = m.statistic#
        WHERE n.name = 'session logical reads';
        RETURN v;
    END;

    PROCEDURE measure(p_id VARCHAR2, p_sql VARCHAR2, p_binds PLS_INTEGER, p_b1 NUMBER, p_b2 NUMBER) IS
        v_result NUMBER;
        v_start  PLS_INTEGER;
        v_reads  NUMBER;
    BEGIN
        IF p_binds = 0 THEN EXECUTE IMMEDIATE p_sql INTO v_result;
        ELSIF p_binds = 1 THEN EXECUTE IMMEDIATE p_sql INTO v_result USING p_b1;
        ELSE EXECUTE IMMEDIATE p_sql INTO v_result USING p_b1, p_b2;
        END IF;
        v_reads := logical_reads;
        v_start := DBMS_UTILITY.GET_TIME;
        FOR k IN 1 .. c_runs LOOP
            IF p_binds = 0 THEN EXECUTE IMMEDIATE p_sql INTO v_result;
            ELSIF p_binds = 1 THEN EXECUTE IMMEDIATE p_sql INTO v_result USING p_b1;
            ELSE EXECUTE IMMEDIATE p_sql INTO v_result USING p_b1, p_b2;
            END IF;
        END LOOP;
        DBMS_OUTPUT.PUT_LINE('RESULT|' || p_id || '|'
            || TO_CHAR((DBMS_UTILITY.GET_TIME - v_start) * 10 / c_runs, 'FM99990.00') || '|'
            || TO_CHAR(ROUND((logical_reads - v_reads) / c_runs)));
    END;
BEGIN
    measure('Q1', q'[SELECT SUM(ORA_HASH(id || status || added_at || pinned_at)) FROM (SELECT pt.id, pt.status, pt.added_at, pt.pinned_at FROM playlist_tracks pt WHERE pt.playlist_id = :1 AND pt.status IN ('QUEUED', 'PLAYING'))]', 1, v_p, NULL);
    measure('Q2', q'[SELECT COUNT(*) + SUM(ORA_HASH(user_id)) FROM (SELECT room_id, user_id FROM room_members WHERE room_id = :1 AND left_at IS NULL)]', 1, v_r, NULL);
    measure('Q3', q'[SELECT SUM(ORA_HASH(id || created_at || user_id || status_code)) FROM (SELECT id, created_at, user_id, status_code FROM audit_logs WHERE action = 'Admin.BlockUser' ORDER BY id DESC FETCH FIRST 50 ROWS ONLY)]', 0, NULL, NULL);
    measure('Q4', q'[SELECT SUM(ORA_HASH(id || action || status_code)) FROM (SELECT id, action, status_code FROM audit_logs WHERE created_at >= SYS_EXTRACT_UTC(SYSTIMESTAMP) - INTERVAL '1' HOUR)]', 0, NULL, NULL);
    measure('Q5', q'[SELECT SUM(ORA_HASH(id || title)) FROM (SELECT id, title FROM tracks WHERE UPPER(title) LIKE 'TRACK 4999%')]', 0, NULL, NULL);
    measure('Q6', q'[SELECT COUNT(*) + SUM(ORA_HASH(id)) FROM (SELECT id FROM playlist_tracks WHERE playlist_id = :1 AND added_by_id = :2 AND status IN ('QUEUED', 'PLAYING'))]', 2, v_p, v_u);
    measure('Q7', q'[SELECT NVL(SUM(vote_value), 0) FROM (SELECT vote_value FROM track_votes WHERE playlist_track_id = :1)]', 1, v_i, NULL);
    measure('Q8', q'[SELECT SUM(ORA_HASH(id || playlist_id)) FROM (SELECT pt.id, pt.playlist_id FROM playlist_tracks pt JOIN tracks t ON t.id = pt.track_id WHERE pt.status = 'PLAYING' AND pt.started_at + NUMTODSINTERVAL(t.duration_sec, 'SECOND') <= SYS_EXTRACT_UTC(SYSTIMESTAMP))]', 0, NULL, NULL);
END;
/

EXIT
