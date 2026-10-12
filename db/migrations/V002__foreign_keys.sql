-- V002__foreign_keys.sql
-- Foreign keys with referential actions. Supporting indexes are created in V003.
-- Run as SOUNDSHARE. Oracle 19c+ syntax only.

ALTER TABLE users
    ADD CONSTRAINT fk_users_role
    FOREIGN KEY (role_id) REFERENCES roles (id);

ALTER TABLE refresh_tokens
    ADD CONSTRAINT fk_refresh_tokens_user
    FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE;

ALTER TABLE refresh_tokens
    ADD CONSTRAINT fk_refresh_tokens_replaced_by
    FOREIGN KEY (replaced_by_token_id) REFERENCES refresh_tokens (id) ON DELETE SET NULL;

ALTER TABLE track_artists
    ADD CONSTRAINT fk_track_artists_track
    FOREIGN KEY (track_id) REFERENCES tracks (id) ON DELETE CASCADE;

ALTER TABLE track_artists
    ADD CONSTRAINT fk_track_artists_artist
    FOREIGN KEY (artist_id) REFERENCES artists (id);

ALTER TABLE rooms
    ADD CONSTRAINT fk_rooms_created_by
    FOREIGN KEY (created_by_id) REFERENCES users (id);

ALTER TABLE room_members
    ADD CONSTRAINT fk_room_members_room
    FOREIGN KEY (room_id) REFERENCES rooms (id);

ALTER TABLE room_members
    ADD CONSTRAINT fk_room_members_user
    FOREIGN KEY (user_id) REFERENCES users (id);

ALTER TABLE playlists
    ADD CONSTRAINT fk_playlists_room
    FOREIGN KEY (room_id) REFERENCES rooms (id);

ALTER TABLE playlists
    ADD CONSTRAINT fk_playlists_owner
    FOREIGN KEY (owner_id) REFERENCES users (id);

ALTER TABLE playlist_tracks
    ADD CONSTRAINT fk_playlist_tracks_playlist
    FOREIGN KEY (playlist_id) REFERENCES playlists (id) ON DELETE CASCADE;

ALTER TABLE playlist_tracks
    ADD CONSTRAINT fk_playlist_tracks_track
    FOREIGN KEY (track_id) REFERENCES tracks (id);

ALTER TABLE playlist_tracks
    ADD CONSTRAINT fk_playlist_tracks_added_by
    FOREIGN KEY (added_by_id) REFERENCES users (id);

ALTER TABLE track_votes
    ADD CONSTRAINT fk_track_votes_item
    FOREIGN KEY (playlist_track_id) REFERENCES playlist_tracks (id) ON DELETE CASCADE;

ALTER TABLE track_votes
    ADD CONSTRAINT fk_track_votes_user
    FOREIGN KEY (user_id) REFERENCES users (id);

ALTER TABLE skip_votes
    ADD CONSTRAINT fk_skip_votes_item
    FOREIGN KEY (playlist_track_id) REFERENCES playlist_tracks (id) ON DELETE CASCADE;

ALTER TABLE skip_votes
    ADD CONSTRAINT fk_skip_votes_user
    FOREIGN KEY (user_id) REFERENCES users (id);

ALTER TABLE audit_logs
    ADD CONSTRAINT fk_audit_logs_user
    FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE SET NULL;
