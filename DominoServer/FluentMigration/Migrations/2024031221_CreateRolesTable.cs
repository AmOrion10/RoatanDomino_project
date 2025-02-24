using FluentMigrator;

[Migration(2024031221)]
public class CreateRolesTable : Migration
{
    public override void Up()
    {
        Create.Table("Roles")
            .WithColumn("id").AsInt64().NotNullable().PrimaryKey().Identity()
            .WithColumn("name").AsString().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Roles");
    }
}
