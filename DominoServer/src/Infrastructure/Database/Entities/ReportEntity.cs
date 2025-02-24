using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Database.Entities;

public class ReportEntity
{
    public long Id { get; set; }

    public long ReportedUserId { get; set; }

    public long ReporterUserId { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime DateReport { get; set; }

    public string Reason { get; set; }

    public virtual UserEntity UserReported { get; set; } = null!;
    public virtual UserEntity UserReporter { get; set; } = null!;
}
