using FluentMigrator.Runner;

using Microsoft.Extensions.DependencyInjection;

namespace FluentMigration;

public class Program
{
    private static long MigrationTargetNumber { get; set; }

    public static void Main(string[] args)
    {
        using ServiceProvider serviceProvider = CreateServices();
        using IServiceScope scope = serviceProvider.CreateScope();
        var runner = serviceProvider.GetRequiredService<IMigrationRunner>();

        if (SpecificMigrationIsSet(args))
        {
            runner.MigrateDown(MigrationTargetNumber);
        }
        else
        {
            runner.MigrateUp();
        }
    }

    private static bool SpecificMigrationIsSet(string[] args)
    {
        if (args.Length == 0) return false;
        MigrationTargetNumber = long.Parse(args[0]);
        return true;
    }

    private static ServiceProvider CreateServices()
    {
        string mysqlConnectionString = Environment.GetEnvironmentVariable("MysqlConnectionString") ?? "Server=localhost;Port=6033;Database=db;Uid=user;Pwd=password;";

        return new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddMySql8()
                .WithGlobalConnectionString(mysqlConnectionString)
                .ScanIn(typeof(Program).Assembly).For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole())
            .BuildServiceProvider(false);
    }
}