-- V005__grants.sql
-- Object privileges for the application user SOUNDSHARE_APP (least privilege).
-- Run as SOUNDSHARE. Re-running is safe.

GRANT SELECT ON roles TO soundshare_app;

GRANT SELECT, INSERT, UPDATE ON users TO soundshare_app;
GRANT SELECT, INSERT, UPDATE ON rooms TO soundshare_app;
GRANT SELECT, INSERT, UPDATE ON room_members TO soundshare_app;

GRANT SELECT, INSERT, UPDATE, DELETE ON refresh_tokens TO soundshare_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON artists TO soundshare_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON tracks TO soundshare_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON track_artists TO soundshare_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON playlists TO soundshare_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON playlist_tracks TO soundshare_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON track_votes TO soundshare_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON skip_votes TO soundshare_app;

GRANT SELECT, INSERT ON audit_logs TO soundshare_app;
