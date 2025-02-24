using FluentMigrator;

[Migration(2024030201)]
public class CreateNationalityTable : Migration
{
    public override void Up()
    {
        Create.Table("Nationality")
            .WithColumn("id").AsInt64().NotNullable().PrimaryKey().Identity()
            .WithColumn("name").AsString().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Nationality");
    }
}
