using FluentMigrator;

[Migration(2026112502)]
public class CreateTeamTables : Migration
{
    public override void Up()
    {
        Create.Table("Teams")
            .WithColumn("id").AsInt64().NotNullable().PrimaryKey().Identity()
            .WithColumn("name").AsString(64).NotNullable().Unique()
            .WithColumn("tag").AsString(8).NotNullable().Unique()
            .WithColumn("team_elo").AsInt32().NotNullable().WithDefaultValue(1000)
            .WithColumn("team_wins").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("team_losses").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("max_members").AsInt32().NotNullable().WithDefaultValue(12)
            .WithColumn("join_policy").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("min_elo").AsInt32().NotNullable().WithDefaultValue(100)
            .WithColumn("activity_requirement").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("created_at_utc").AsDateTime().NotNullable();

        Create.Table("TeamMembers")
            .WithColumn("team_id").AsInt64().NotNullable().ForeignKey("Teams", "id")
            .WithColumn("user_id").AsInt64().NotNullable().Unique().ForeignKey("Users", "id")
            .WithColumn("role").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("joined_at_utc").AsDateTime().NotNullable();

        Create.PrimaryKey("PK_TeamMembers")
            .OnTable("TeamMembers")
            .Columns("team_id", "user_id");

        Create.Table("TeamJoinRequests")
            .WithColumn("id").AsInt64().NotNullable().PrimaryKey().Identity()
            .WithColumn("team_id").AsInt64().NotNullable().ForeignKey("Teams", "id")
            .WithColumn("user_id").AsInt64().NotNullable().ForeignKey("Users", "id")
            .WithColumn("created_at_utc").AsDateTime().NotNullable();

        Create.Table("TeamInvites")
            .WithColumn("id").AsInt64().NotNullable().PrimaryKey().Identity()
            .WithColumn("team_id").AsInt64().NotNullable().ForeignKey("Teams", "id")
            .WithColumn("invited_user_id").AsInt64().NotNullable().ForeignKey("Users", "id")
            .WithColumn("invited_by_user_id").AsInt64().NotNullable().ForeignKey("Users", "id")
            .WithColumn("created_at_utc").AsDateTime().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("TeamInvites");
        Delete.Table("TeamJoinRequests");
        Delete.Table("TeamMembers");
        Delete.Table("Teams");
    }
}
