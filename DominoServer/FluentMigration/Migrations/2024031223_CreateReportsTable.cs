using FluentMigrator;

[Migration(2024031223)]
public class CreateReportsTable : Migration
{
    public override void Up()
    {
        Create.Table("Reports")
            .WithColumn("id").AsInt64().NotNullable().PrimaryKey().Identity()
            .WithColumn("user_id").AsInt64().NotNullable().ForeignKey("Users", "id")
            .WithColumn("reporter_id").AsInt64().NotNullable().ForeignKey("Users", "id")
            .WithColumn("reason").AsString().NotNullable()
            .WithColumn("date_time").AsDateTime().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Reports");
    }
}
