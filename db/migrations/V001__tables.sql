-- V001__tables.sql
-- Tables with PK, UNIQUE, CHECK and DEFAULT. FKs in V002, indexes in V003.
-- Run as SOUNDSHARE. Oracle 19c+ syntax only.

CREATE TABLE roles (
    id    NUMBER(5)          NOT NULL,
    name  VARCHAR2(32 CHAR)  NOT NULL,
    CONSTRAINT pk_roles      PRIMARY KEY (id),
    CONSTRAINT uq_roles_name UNIQUE (name)
);

CREATE TABLE users (
    id             NUMBER(19)          GENERATED ALWAYS AS IDENTITY,
    username       VARCHAR2(32 CHAR)   NOT NULL,
    email          VARCHAR2(254 CHAR)  NOT NULL,
    password_hash  VARCHAR2(256 BYTE)  NOT NULL,
    role_id        NUMBER(5)           DEFAULT ON NULL 2,
    is_active      NUMBER(1)           DEFAULT ON NULL 1,
    created_at     TIMESTAMP(6)        DEFAULT ON NULL SYS_EXTRACT_UTC(SYSTIMESTAMP),
    CONSTRAINT pk_users           PRIMARY KEY (id),
    CONSTRAINT ck_users_is_active CHECK (is_active IN (0, 1)),
    CONSTRAINT ck_users_username  CHECK (REGEXP_LIKE(username, '^[A-Za-z0-9_]{3,32}$')),
    CONSTRAINT ck_users_email     CHECK (INSTR(email, '@') > 1)
);

CREATE TABLE refresh_tokens (
    id                    NUMBER(19)         GENERATED ALWAYS AS IDENTITY,
    user_id               NUMBER(19)         NOT NULL,
    token_hash            RAW(32)            NOT NULL,
    expires_at            TIMESTAMP(6)       NOT NULL,
    created_at            TIMESTAMP(6)       DEFAULT ON NULL SYS_EXTRACT_UTC(SYSTIMESTAMP),
    created_by_ip         VARCHAR2(45 BYTE),
    revoked_at            TIMESTAMP(6),
    replaced_by_token_id  NUMBER(19),
    CONSTRAINT pk_refresh_tokens            PRIMARY KEY (id),
    CONSTRAINT uq_refresh_tokens_token_hash UNIQUE (token_hash),
    CONSTRAINT ck_refresh_tokens_expiry     CHECK (expires_at > created_at),
    CONSTRAINT ck_refresh_tokens_revoked    CHECK (revoked_at IS NULL OR revoked_at >= created_at),
    CONSTRAINT ck_refresh_tokens_not_self   CHECK (replaced_by_token_id IS NULL OR replaced_by_token_id <> id)
);
