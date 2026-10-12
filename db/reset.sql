-- reset.sql
-- Drops every object in the SOUNDSHARE schema for a clean rebuild. Local development only.
-- Run as SOUNDSHARE with SQL*Plus or SQLcl.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET SERVEROUTPUT ON
SET FEEDBACK OFF

BEGIN
    IF USER <> 'SOUNDSHARE' THEN
        RAISE_APPLICATION_ERROR(-20000, 'reset.sql must be run as SOUNDSHARE, not ' || USER);
    END IF;

    FOR o IN (SELECT object_name, object_type
              FROM user_objects
              WHERE object_type IN ('VIEW', 'PROCEDURE', 'FUNCTION', 'PACKAGE')) LOOP
        EXECUTE IMMEDIATE 'DROP ' || o.object_type || ' ' || DBMS_ASSERT.ENQUOTE_NAME(o.object_name, FALSE);
    END LOOP;

    FOR t IN (SELECT table_name FROM user_tables) LOOP
        EXECUTE IMMEDIATE 'DROP TABLE ' || DBMS_ASSERT.ENQUOTE_NAME(t.table_name, FALSE)
                       || ' CASCADE CONSTRAINTS PURGE';
    END LOOP;

    DBMS_OUTPUT.PUT_LINE('Schema SOUNDSHARE is empty');
END;
/

EXIT
