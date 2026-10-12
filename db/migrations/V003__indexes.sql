-- V003__indexes.sql
-- Indexes for foreign keys and function-based unique indexes for conditional uniqueness.
-- Performance indexes are added later, each justified by EXPLAIN PLAN. Run as SOUNDSHARE.

CREATE INDEX ix_users_role                 ON users (role_id);
CREATE INDEX ix_refresh_tokens_user        ON refresh_tokens (user_id);
CREATE INDEX ix_refresh_tokens_replaced_by ON refresh_tokens (replaced_by_token_id);
CREATE INDEX ix_track_artists_artist       ON track_artists (artist_id);
CREATE INDEX ix_rooms_created_by           ON rooms (created_by_id);
CREATE INDEX ix_room_members_user          ON room_members (user_id);
CREATE INDEX ix_playlists_owner            ON playlists (owner_id);
CREATE INDEX ix_playlist_tracks_playlist   ON playlist_tracks (playlist_id);
CREATE INDEX ix_playlist_tracks_track      ON playlist_tracks (track_id);
CREATE INDEX ix_playlist_tracks_added_by   ON playlist_tracks (added_by_id);
CREATE INDEX ix_track_votes_user           ON track_votes (user_id);
CREATE INDEX ix_skip_votes_user            ON skip_votes (user_id);
CREATE INDEX ix_audit_logs_user            ON audit_logs (user_id);

CREATE UNIQUE INDEX ux_users_username_ci ON users (UPPER(username));
CREATE UNIQUE INDEX ux_users_email_ci    ON users (UPPER(email));
CREATE UNIQUE INDEX ux_artists_name_ci   ON artists (UPPER(name));

CREATE UNIQUE INDEX ux_room_members_active_user
    ON room_members (CASE WHEN left_at IS NULL THEN user_id END);

CREATE UNIQUE INDEX ux_room_members_active_owner
    ON room_members (CASE WHEN left_at IS NULL AND member_role = 'OWNER' THEN room_id END);

CREATE UNIQUE INDEX ux_playlist_tracks_playing
    ON playlist_tracks (CASE WHEN status = 'PLAYING' THEN playlist_id END);

CREATE UNIQUE INDEX ux_playlist_tracks_position
    ON playlist_tracks (CASE WHEN position IS NOT NULL THEN playlist_id END, position);
