using FluentMigrator;

[Migration(2024031212)]
public class CreateFriendshipTable : Migration
{
    public override void Up()
    {
        Create.Table("Friendship")
            .WithColumn("user_id").AsInt64().NotNullable().PrimaryKey().ForeignKey("Users", "id")
            .WithColumn("friend_id").AsInt64().NotNullable().PrimaryKey().ForeignKey("Users", "id")
            .WithColumn("date_start").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime);
    }

    public override void Down()
    {
        Delete.Table("Friendship");
    }
}
