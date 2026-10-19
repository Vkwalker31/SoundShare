-- apply_migrations.sql
-- Applies all schema migrations in order. Used for local runs; the application uses DbUp.
-- Run as SOUNDSHARE: sqlplus soundshare@//localhost:1521/FREEPDB1 @db/apply_migrations.sql

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET DEFINE OFF
SET FEEDBACK OFF

PROMPT V001__tables
@@migrations/V001__tables.sql
PROMPT V002__foreign_keys
@@migrations/V002__foreign_keys.sql
PROMPT V003__indexes
@@migrations/V003__indexes.sql
PROMPT V004__comments
@@migrations/V004__comments.sql
PROMPT V005__grants
@@migrations/V005__grants.sql
PROMPT V006__reference_data
@@migrations/V006__reference_data.sql
PROMPT V007__audit_triggers
@@migrations/V007__audit_triggers.sql
PROMPT V008__queue_audit_trigger
@@migrations/V008__queue_audit_trigger.sql
PROMPT All migrations applied

EXIT
