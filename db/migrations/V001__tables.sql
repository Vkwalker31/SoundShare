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

CREATE TABLE artists (
    id          NUMBER(19)          GENERATED ALWAYS AS IDENTITY,
    name        VARCHAR2(200 CHAR)  NOT NULL,
    created_at  TIMESTAMP(6)        DEFAULT ON NULL SYS_EXTRACT_UTC(SYSTIMESTAMP),
    CONSTRAINT pk_artists      PRIMARY KEY (id),
    CONSTRAINT ck_artists_name CHECK (TRIM(name) IS NOT NULL)
);

CREATE TABLE tracks (
    id                  NUMBER(19)          GENERATED ALWAYS AS IDENTITY,
    title               VARCHAR2(300 CHAR)  NOT NULL,
    duration_sec        NUMBER(6)           NOT NULL,
    external_source     VARCHAR2(16 BYTE)   DEFAULT ON NULL 'YOUTUBE',
    external_source_id  VARCHAR2(64 BYTE)   NOT NULL,
    created_at          TIMESTAMP(6)        DEFAULT ON NULL SYS_EXTRACT_UTC(SYSTIMESTAMP),
    CONSTRAINT pk_tracks             PRIMARY KEY (id),
    CONSTRAINT uq_tracks_external    UNIQUE (external_source, external_source_id),
    CONSTRAINT ck_tracks_title       CHECK (TRIM(title) IS NOT NULL),
    CONSTRAINT ck_tracks_duration    CHECK (duration_sec > 0),
    CONSTRAINT ck_tracks_source      CHECK (external_source IN ('YOUTUBE')),
    CONSTRAINT ck_tracks_youtube_id  CHECK (external_source <> 'YOUTUBE'
                                            OR REGEXP_LIKE(external_source_id, '^[A-Za-z0-9_-]{11}$'))
);

CREATE TABLE track_artists (
    track_id      NUMBER(19)  NOT NULL,
    artist_id     NUMBER(19)  NOT NULL,
    artist_order  NUMBER(2)   NOT NULL,
    CONSTRAINT pk_track_artists       PRIMARY KEY (track_id, artist_id),
    CONSTRAINT uq_track_artists_order UNIQUE (track_id, artist_order),
    CONSTRAINT ck_track_artists_order CHECK (artist_order >= 1)
);

CREATE TABLE rooms (
    id             NUMBER(19)          GENERATED ALWAYS AS IDENTITY,
    name           VARCHAR2(100 CHAR)  NOT NULL,
    created_by_id  NUMBER(19)          NOT NULL,
    is_private     NUMBER(1)           DEFAULT ON NULL 0,
    invite_code    CHAR(6 BYTE)        NOT NULL,
    is_active      NUMBER(1)           DEFAULT ON NULL 1,
    empty_since    TIMESTAMP(6),
    created_at     TIMESTAMP(6)        DEFAULT ON NULL SYS_EXTRACT_UTC(SYSTIMESTAMP),
    CONSTRAINT pk_rooms             PRIMARY KEY (id),
    CONSTRAINT uq_rooms_invite_code UNIQUE (invite_code),
    CONSTRAINT ck_rooms_name        CHECK (TRIM(name) IS NOT NULL),
    CONSTRAINT ck_rooms_is_private  CHECK (is_private IN (0, 1)),
    CONSTRAINT ck_rooms_is_active   CHECK (is_active IN (0, 1)),
    CONSTRAINT ck_rooms_invite_code CHECK (REGEXP_LIKE(invite_code, '^[A-HJ-NP-Z2-9]{6}$', 'c'))
);

CREATE TABLE room_members (
    room_id       NUMBER(19)         NOT NULL,
    user_id       NUMBER(19)         NOT NULL,
    member_role   VARCHAR2(16 BYTE)  DEFAULT ON NULL 'CONTRIBUTOR',
    joined_at     TIMESTAMP(6)       DEFAULT ON NULL SYS_EXTRACT_UTC(SYSTIMESTAMP),
    left_at       TIMESTAMP(6),
    banned_until  TIMESTAMP(6),
    CONSTRAINT pk_room_members      PRIMARY KEY (room_id, user_id),
    CONSTRAINT ck_room_members_role CHECK (member_role IN ('OWNER', 'MODERATOR', 'CONTRIBUTOR')),
    CONSTRAINT ck_room_members_left CHECK (left_at IS NULL OR left_at >= joined_at),
    CONSTRAINT ck_room_members_ban  CHECK (banned_until IS NULL
                                           OR (left_at IS NOT NULL AND banned_until > left_at))
);
