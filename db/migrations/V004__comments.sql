-- V004__comments.sql
-- Data dictionary descriptions of tables and columns (visible in USER_TAB_COMMENTS, USER_COL_COMMENTS).
-- Run as SOUNDSHARE.

COMMENT ON TABLE roles IS 'Справочник системных ролей: 1 - Admin, 2 - User';
COMMENT ON COLUMN roles.id IS 'Фиксированный идентификатор роли, используется в коде приложения';
COMMENT ON COLUMN roles.name IS 'Название роли, передаётся в claim role токена';

COMMENT ON TABLE users IS 'Учётные записи пользователей; удаление - обезличивание с IS_ACTIVE = 0';
COMMENT ON COLUMN users.username IS 'Имя пользователя, 3-32 символа [A-Za-z0-9_], уникально без учёта регистра';
COMMENT ON COLUMN users.email IS 'Email, уникален без учёта регистра';
COMMENT ON COLUMN users.password_hash IS 'Хеш пароля с солью (PBKDF2); пароль не хранится';
COMMENT ON COLUMN users.role_id IS 'Системная роль, по умолчанию User';
COMMENT ON COLUMN users.is_active IS '1 - активен, 0 - заблокирован или удалён';
COMMENT ON COLUMN users.created_at IS 'Время регистрации, UTC';

COMMENT ON TABLE refresh_tokens IS 'Сессии входа: refresh-токены с ротацией и отзывом';
COMMENT ON COLUMN refresh_tokens.token_hash IS 'SHA-256 токена (32 байта); сам токен не хранится';
COMMENT ON COLUMN refresh_tokens.expires_at IS 'Окончание срока действия, UTC';
COMMENT ON COLUMN refresh_tokens.created_by_ip IS 'IP-адрес клиента при выдаче токена';
COMMENT ON COLUMN refresh_tokens.revoked_at IS 'Время отзыва, UTC; NULL - токен действует';
COMMENT ON COLUMN refresh_tokens.replaced_by_token_id IS 'Токен, выданный взамен при ротации';

COMMENT ON TABLE artists IS 'Исполнители';
COMMENT ON COLUMN artists.name IS 'Имя исполнителя, уникально без учёта регистра';

COMMENT ON TABLE tracks IS 'Каталог треков: метаданные роликов внешнего источника';
COMMENT ON COLUMN tracks.title IS 'Название трека';
COMMENT ON COLUMN tracks.duration_sec IS 'Длительность в секундах, нужна для автоперехода';
COMMENT ON COLUMN tracks.external_source IS 'Источник трека: YOUTUBE';
COMMENT ON COLUMN tracks.external_source_id IS 'Идентификатор ролика в источнике; обложка вычисляется из него';

COMMENT ON TABLE track_artists IS 'Исполнители трека с порядком (feat.)';
COMMENT ON COLUMN track_artists.artist_order IS 'Порядковый номер исполнителя; 1 - основной';

COMMENT ON TABLE rooms IS 'Комнаты совместного прослушивания';
COMMENT ON COLUMN rooms.created_by_id IS 'Создатель комнаты; текущий владелец - в ROOM_MEMBERS';
COMMENT ON COLUMN rooms.is_private IS '1 - вход только по инвайт-коду, 0 - комната видна в поиске';
COMMENT ON COLUMN rooms.invite_code IS 'Инвайт-код из 6 символов без 0, O, 1, I';
COMMENT ON COLUMN rooms.is_active IS '1 - активна, 0 - закрыта';
COMMENT ON COLUMN rooms.empty_since IS 'Время, с которого в комнате нет соединений; NULL - в комнате есть участники';

COMMENT ON TABLE room_members IS 'Участники комнат: роль, история участия, бан';
COMMENT ON COLUMN room_members.member_role IS 'Роль в комнате: OWNER, MODERATOR, CONTRIBUTOR';
COMMENT ON COLUMN room_members.joined_at IS 'Время последнего входа в комнату, UTC';
COMMENT ON COLUMN room_members.left_at IS 'Время выхода, UTC; NULL - участник активен';
COMMENT ON COLUMN room_members.banned_until IS 'Окончание бана, UTC; 9999-12-31 - бессрочный бан';

COMMENT ON TABLE playlists IS 'Плейлисты: очередь комнаты (ROOM_ID) либо личный плейлист (OWNER_ID)';
COMMENT ON COLUMN playlists.room_id IS 'Комната, чьей очередью является плейлист';
COMMENT ON COLUMN playlists.owner_id IS 'Владелец личного плейлиста';
COMMENT ON COLUMN playlists.queue_version IS 'Версия очереди, растёт при каждом изменении; токен оптимистичной блокировки';

COMMENT ON TABLE playlist_tracks IS 'Позиции треков в плейлистах и их статус воспроизведения';
COMMENT ON COLUMN playlist_tracks.added_by_id IS 'Пользователь, добавивший трек';
COMMENT ON COLUMN playlist_tracks.status IS 'QUEUED, PLAYING, PLAYED, SKIPPED, REMOVED, AUTO_REMOVED';
COMMENT ON COLUMN playlist_tracks.pinned_at IS 'Время закрепления, UTC; NULL - не закреплён';
COMMENT ON COLUMN playlist_tracks.started_at IS 'Время начала воспроизведения, UTC';
COMMENT ON COLUMN playlist_tracks.position IS 'Ручной порядок в личном плейлисте; NULL в очереди комнаты';

COMMENT ON TABLE track_votes IS 'Голоса за позиции очереди; рейтинг позиции - сумма голосов';
COMMENT ON COLUMN track_votes.vote_value IS '+1 или -1; снятый голос удаляется';

COMMENT ON TABLE skip_votes IS 'Голоса за пропуск играющего трека';

COMMENT ON TABLE audit_logs IS 'Журнал действий пользователей и системы; только добавление записей';
COMMENT ON COLUMN audit_logs.source IS 'Источник записи: HTTP, HUB, DOMAIN, SYSTEM';
COMMENT ON COLUMN audit_logs.action IS 'Код действия, например Room.Create, Auth.LoginFailed';
COMMENT ON COLUMN audit_logs.entity_type IS 'Тип затронутой сущности';
COMMENT ON COLUMN audit_logs.entity_id IS 'Идентификатор затронутой сущности (без внешнего ключа)';
COMMENT ON COLUMN audit_logs.correlation_id IS 'Сквозной идентификатор запроса X-Correlation-Id';
COMMENT ON COLUMN audit_logs.duration_ms IS 'Длительность обработки запроса, мс';
COMMENT ON COLUMN audit_logs.details IS 'Дополнительные данные в формате JSON';
