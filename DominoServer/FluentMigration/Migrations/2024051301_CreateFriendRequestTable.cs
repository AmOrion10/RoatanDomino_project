using FluentMigrator;

namespace FluentMigration.Migrations;

[Migration(2024051303)]
public class CreateUserRoleTable : Migration
{
    public override void Up()
    {
        Create.Table("FriendRequests")
            .WithColumn("user_id_applicant").AsInt64().PrimaryKey().ForeignKey("Users", "id")
            .WithColumn("user_id_asked").AsInt64().PrimaryKey().ForeignKey("Users", "id");
    }

    public override void Down()
    {
        Delete.Table("FriendRequests");
    }
}