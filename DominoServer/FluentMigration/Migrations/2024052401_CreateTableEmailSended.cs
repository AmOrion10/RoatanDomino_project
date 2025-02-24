using FluentMigrator;

namespace FluentMigration.Migrations;

[Migration(2024052401)]
public class CreateTableEmailSended : Migration
{
    public override void Up()
    {
        Create.Table("EmailSended")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("date_sended").AsDateTime().NotNullable()
            .WithColumn("to_email_address").AsString().NotNullable()
            .WithColumn("email_type").AsString().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("EmailSended");
    }
}