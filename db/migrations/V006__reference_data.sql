-- V006__reference_data.sql
-- Reference data the schema depends on: system roles (USERS.ROLE_ID defaults to 2).
-- Run as SOUNDSHARE. Idempotent.

MERGE INTO roles r
USING (
    SELECT 1 AS id, 'Admin' AS name FROM dual
    UNION ALL
    SELECT 2, 'User' FROM dual
) s
ON (r.id = s.id)
WHEN MATCHED THEN
    UPDATE SET r.name = s.name WHERE r.name <> s.name
WHEN NOT MATCHED THEN
    INSERT (id, name) VALUES (s.id, s.name);

COMMIT;
