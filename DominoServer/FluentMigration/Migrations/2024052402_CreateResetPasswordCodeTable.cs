using FluentMigrator;

namespace FluentMigration.Migrations;

[Migration(2024052402)]
public class CreateTableResetPasswordCode : Migration
{
    public override void Up()
    {
        Create.Table("ResetPasswordCode")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("code").AsString().NotNullable()
            .WithColumn("email_address").AsString().NotNullable()
            .WithColumn("date_generated").AsDateTime().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("ResetPasswordCode");
    }
}