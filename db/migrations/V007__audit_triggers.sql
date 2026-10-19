-- V007__audit_triggers.sql
-- Database-level audit: role changes, blocking and bans are written to AUDIT_LOGS with source DOMAIN,
-- whoever changes the row (API, administrator in SQL*Plus, migration script).
-- Run as SOUNDSHARE (needs CREATE TRIGGER, granted in bootstrap).

CREATE OR REPLACE TRIGGER trg_users_audit
AFTER UPDATE OF role_id, is_active ON users
FOR EACH ROW
WHEN (OLD.role_id <> NEW.role_id OR OLD.is_active <> NEW.is_active)
BEGIN
    IF :OLD.role_id <> :NEW.role_id THEN
        INSERT INTO audit_logs (source, action, entity_type, entity_id, details)
        VALUES ('DOMAIN', 'User.RoleChanged', 'User', TO_CHAR(:NEW.id),
                JSON_OBJECT('oldRoleId' VALUE :OLD.role_id, 'newRoleId' VALUE :NEW.role_id,
                            'dbUser' VALUE SYS_CONTEXT('USERENV', 'SESSION_USER')));
    END IF;

    IF :OLD.is_active <> :NEW.is_active THEN
        INSERT INTO audit_logs (source, action, entity_type, entity_id, details)
        VALUES ('DOMAIN', CASE WHEN :NEW.is_active = 0 THEN 'User.Deactivated' ELSE 'User.Activated' END,
                'User', TO_CHAR(:NEW.id),
                JSON_OBJECT('username' VALUE :NEW.username,
                            'dbUser' VALUE SYS_CONTEXT('USERENV', 'SESSION_USER')));
    END IF;
END;
/

CREATE OR REPLACE TRIGGER trg_room_members_ban_audit
AFTER UPDATE OF banned_until ON room_members
FOR EACH ROW
WHEN (NEW.banned_until IS NOT NULL AND (OLD.banned_until IS NULL OR OLD.banned_until <> NEW.banned_until))
BEGIN
    INSERT INTO audit_logs (source, user_id, action, entity_type, entity_id, details)
    VALUES ('DOMAIN', :NEW.user_id, 'Room.MemberBanned', 'Room', TO_CHAR(:NEW.room_id),
            JSON_OBJECT('bannedUntil' VALUE TO_CHAR(:NEW.banned_until, 'YYYY-MM-DD"T"HH24:MI:SS"Z"'),
                        'dbUser' VALUE SYS_CONTEXT('USERENV', 'SESSION_USER')));
END;
/
