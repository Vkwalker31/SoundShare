-- 01_create_users.sql
-- Creates schema owner SOUNDSHARE and application user SOUNDSHARE_APP. Idempotent.
-- Run as SYSTEM in the PDB (FREEPDB1 / XEPDB1) with SQL*Plus or SQLcl.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET VERIFY OFF
SET SERVEROUTPUT ON

ACCEPT owner_password CHAR PROMPT 'Password for SOUNDSHARE: '     HIDE
ACCEPT app_password   CHAR PROMPT 'Password for SOUNDSHARE_APP: ' HIDE

DECLARE
    e_user_exists EXCEPTION;
    PRAGMA EXCEPTION_INIT(e_user_exists, -1920);

    PROCEDURE create_user(p_username IN VARCHAR2, p_password IN VARCHAR2) IS
    BEGIN
        EXECUTE IMMEDIATE
               'CREATE USER ' || p_username
            || ' IDENTIFIED BY "' || p_password || '"'
            || ' DEFAULT TABLESPACE users'
            || ' TEMPORARY TABLESPACE temp';
        DBMS_OUTPUT.PUT_LINE('Created user ' || p_username);
    EXCEPTION
        WHEN e_user_exists THEN
            DBMS_OUTPUT.PUT_LINE('User ' || p_username || ' already exists - skipped');
    END create_user;
BEGIN
    create_user('SOUNDSHARE',     '&owner_password');
    create_user('SOUNDSHARE_APP', '&app_password');
END;
/

ALTER USER soundshare QUOTA UNLIMITED ON users;
GRANT CREATE SESSION, CREATE TABLE, CREATE SEQUENCE, CREATE VIEW, CREATE PROCEDURE TO soundshare;

GRANT CREATE SESSION TO soundshare_app;

UNDEFINE owner_password
UNDEFINE app_password

EXIT
