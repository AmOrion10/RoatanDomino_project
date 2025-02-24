using FluentMigrator;

[Migration(2024030306)]
public class CreateChatTable : Migration
{
    public override void Up()
    {
        Create.Table("Chat")
            .WithColumn("id").AsInt64().NotNullable().PrimaryKey().Identity()
            .WithColumn("sender_user_id").AsInt64().NotNullable().ForeignKey("Users", "id")
            .WithColumn("receiver_user_id").AsInt64().NotNullable().ForeignKey("Users", "id")
            .WithColumn("content").AsString().NotNullable()
            .WithColumn("send_time").AsDateTime().NotNullable()
            .WithColumn("channel").AsString().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Chat");
    }
}
