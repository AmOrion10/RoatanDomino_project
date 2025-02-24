using FluentMigrator;

[Migration(2024051604)]
public class InsertIntoDominoTable : Migration
{
    public override void Up()
    {
        for (int i = 0; i < 7; i++)
        {
            for (int j = i; j < 7; j++)
            {
                Insert.IntoTable("Dominos")
                    .Row(new
                    {
                        side_one = i,
                        side_two = j
                    });
            }
        }
    }

    public override void Down()
    {
        for (int i = 0; i < 7; i++)
        {
            for (int j = i; j < 7; j++)
            {
                Delete.FromTable("Dominos")
                    .Row(new
                    {
                        side_one = i,
                        side_two = j
                    });
            }
        }
    }
}
