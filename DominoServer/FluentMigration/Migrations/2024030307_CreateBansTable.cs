using FluentMigrator;

[Migration(2024030307)]
public class CreateBansTable : Migration
{
    public override void Up()
    {
        Create.Table("Bans")
            .WithColumn("id").AsInt64().NotNullable().PrimaryKey().Identity()
            .WithColumn("user_id").AsInt64().NotNullable().ForeignKey("Users", "id")
            .WithColumn("start_time").AsDateTime().NotNullable()
            .WithColumn("end_time").AsDateTime().NotNullable()
            .WithColumn("reason").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Table("Bans");
    }
}
