using Oracle.ManagedDataAccess.Client;
using SoundShare.IntegrationTests.Infrastructure;

namespace SoundShare.IntegrationTests.Persistence;

[Collection(SoundShareTestGroup.Name)]
public class DatabaseConstraintTests(SoundShareFixture fixture)
{
    [Theory]
    [InlineData("ab")]
    [InlineData("bad name")]
    [InlineData("имя_юзера")]
    public async Task Users_InvalidUsername_RejectedByCheckConstraint(string username)
    {
        var error = await Should.ThrowAsync<OracleException>(() => fixture.ExecuteOwnerSqlAsync(
            "INSERT INTO users (username, email, password_hash, role_id) VALUES (:name, 'x@example.com', 'hash', 2)",
            ("name", username)));

        error.Number.ShouldBe(2290);
        error.Message.ShouldContain("CK_USERS_USERNAME");
    }

    [Fact]
    public async Task Users_SameUsernameDifferentCase_RejectedByUniqueIndex()
    {
        var username = TestApi.UniqueName("Cs");
        await InsertUserAsync(username);

        var error = await Should.ThrowAsync<OracleException>(() => InsertUserAsync(username.ToUpperInvariant()));

        error.Number.ShouldBe(1);
    }

    [Fact]
    public async Task Tracks_ZeroDuration_RejectedByCheckConstraint()
    {
        var error = await Should.ThrowAsync<OracleException>(() => fixture.ExecuteOwnerSqlAsync(
            "INSERT INTO tracks (title, duration_sec, external_source, external_source_id) VALUES ('Song', 0, 'YOUTUBE', :id)",
            ("id", TestApi.NewVideoId())));

        error.Number.ShouldBe(2290);
        error.Message.ShouldContain("CK_TRACKS_DURATION");
    }

    [Theory]
    [InlineData("short")]
    [InlineData("has spaces!")]
    public async Task Tracks_MalformedYouTubeId_RejectedByCheckConstraint(string videoId)
    {
        var error = await Should.ThrowAsync<OracleException>(() => fixture.ExecuteOwnerSqlAsync(
            "INSERT INTO tracks (title, duration_sec, external_source, external_source_id) VALUES ('Song', 200, 'YOUTUBE', :id)",
            ("id", videoId)));

        error.Number.ShouldBe(2290);
    }

    [Fact]
    public async Task TrackVotes_UnknownQueueItem_RejectedByForeignKey()
    {
        var userId = await InsertUserAsync(TestApi.UniqueName("Fk"));

        var error = await Should.ThrowAsync<OracleException>(() => fixture.ExecuteOwnerSqlAsync(
            "INSERT INTO track_votes (playlist_track_id, user_id, vote_value) VALUES (-1, :userId, 1)",
            ("userId", userId)));

        error.Number.ShouldBe(2291);
    }

    [Fact]
    public async Task AuditLogs_DetailsNotJson_RejectedByCheckConstraint()
    {
        var error = await Should.ThrowAsync<OracleException>(() => fixture.ExecuteOwnerSqlAsync(
            "INSERT INTO audit_logs (source, action, details) VALUES ('SYSTEM', 'Test', 'not json')"));

        error.Number.ShouldBe(2290);
    }

    [Theory]
    [InlineData("DELETE FROM soundshare.users WHERE id = -1")]
    [InlineData("DELETE FROM soundshare.audit_logs WHERE id = -1")]
    [InlineData("UPDATE soundshare.audit_logs SET action = 'x' WHERE id = -1")]
    [InlineData("DROP TABLE soundshare.rooms")]
    public async Task AppUser_ForbiddenStatement_IsDenied(string sql)
    {
        await using var connection = new OracleConnection(fixture.AppConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var error = await Should.ThrowAsync<OracleException>(() => command.ExecuteNonQueryAsync());

        error.Number.ShouldBeOneOf(1031, 41900);
    }

    private async Task<long> InsertUserAsync(string username)
    {
        await using var connection = new OracleConnection(fixture.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = "INSERT INTO users (username, email, password_hash, role_id) VALUES (:name, :email, 'hash', 2) RETURNING id INTO :id";
        command.Parameters.Add(new OracleParameter("name", username));
        command.Parameters.Add(new OracleParameter("email", $"{Guid.NewGuid():N}@example.com"));
        var id = new OracleParameter("id", OracleDbType.Int64) { Direction = System.Data.ParameterDirection.Output };
        command.Parameters.Add(id);
        await command.ExecuteNonQueryAsync();
        return long.Parse(id.Value.ToString()!, System.Globalization.CultureInfo.InvariantCulture);
    }
}
