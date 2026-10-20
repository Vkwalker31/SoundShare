-- V009__performance_indexes.sql
-- Indexes chosen from EXPLAIN PLAN on volume data (stage 7, docs/07-index-report.md).
-- Run as SOUNDSHARE.

CREATE INDEX ix_playlist_tracks_queue ON playlist_tracks (playlist_id, status, added_by_id);
DROP INDEX ix_playlist_tracks_playlist;

CREATE INDEX ix_playlist_tracks_status ON playlist_tracks (status, started_at);

CREATE INDEX ix_room_members_room_active ON room_members (room_id, left_at);

CREATE INDEX ix_audit_logs_action_id ON audit_logs (action, id);

CREATE INDEX ix_audit_logs_created ON audit_logs (created_at);

CREATE INDEX ix_tracks_title_upper ON tracks (UPPER(title));
