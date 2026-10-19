-- V008__queue_audit_trigger.sql
-- Queue items leaving the queue (auto-removal by votes, removal, skip) are written to AUDIT_LOGS with source DOMAIN.
-- Run as SOUNDSHARE.

CREATE OR REPLACE TRIGGER trg_playlist_tracks_status_audit
AFTER UPDATE OF status ON playlist_tracks
FOR EACH ROW
WHEN (NEW.status IN ('AUTO_REMOVED', 'REMOVED', 'SKIPPED') AND OLD.status <> NEW.status)
BEGIN
    INSERT INTO audit_logs (source, user_id, action, entity_type, entity_id, details)
    VALUES ('DOMAIN',
            :NEW.added_by_id,
            CASE :NEW.status
                WHEN 'AUTO_REMOVED' THEN 'Queue.AutoRemoved'
                WHEN 'REMOVED' THEN 'Queue.Removed'
                ELSE 'Queue.Skipped'
            END,
            'QueueItem',
            TO_CHAR(:NEW.id),
            JSON_OBJECT('playlistId' VALUE :NEW.playlist_id, 'trackId' VALUE :NEW.track_id,
                        'oldStatus' VALUE :OLD.status, 'newStatus' VALUE :NEW.status));
END;
/
