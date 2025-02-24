using FluentMigrator;

[Migration(2024031222)]
public class CreateUserRoleTable : Migration
{
    public override void Up()
    {
        Create.Table("UserRole")
            .WithColumn("user_id").AsInt64().PrimaryKey().ForeignKey("Users", "id")
            .WithColumn("role_id").AsInt64().PrimaryKey().ForeignKey("Roles", "id");
    }

    public override void Down()
    {
        Delete.Table("UserRole");
    }
}
