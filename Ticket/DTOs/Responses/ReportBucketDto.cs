namespace Ticket.DTOs.Responses;

public class ReportBucketDto
{
    public string Bucket { get; set; } = string.Empty;
    public int Count { get; set; }
    public DateTimeOffset? PeriodStartUtc { get; set; }
    public DateTimeOffset? PeriodEndUtc { get; set; }
}
