using TrafficFine.Web.Domain;
using Xunit;
namespace TrafficFine.Tests;
public sealed class WorkflowTests
{
    private static Actor User(string role, string id = "actor") => new(id, id, new HashSet<string> { role });
    [Theory]
    [InlineData(FineStatus.New, FineAction.Submitted, Roles.Operations, "owner", FineStatus.ManagerApproval)]
    [InlineData(FineStatus.ManagerApproval, FineAction.ManagerApproved, Roles.Manager, "manager", FineStatus.FinanceApproval)]
    [InlineData(FineStatus.FinanceApproval, FineAction.FinanceApproved, Roles.Finance, "finance", FineStatus.Completed)]
    [InlineData(FineStatus.ManagerApproval, FineAction.Rejected, Roles.Manager, "manager", FineStatus.Rejected)]
    [InlineData(FineStatus.FinanceApproval, FineAction.Rejected, Roles.Finance, "finance", FineStatus.Rejected)]
    [InlineData(FineStatus.Rejected, FineAction.Revised, Roles.Operations, "owner", FineStatus.New)]
    public void Authorized_transition_reaches_expected_stage(FineStatus status, FineAction action, string role, string id, FineStatus expected)
    {
        var fine = new FineRecord { Status = status, CreatedById = "owner" };
        Assert.Equal(expected, Workflow.Next(fine, action, User(role, id), "Checked"));
    }
    [Theory]
    [InlineData(FineStatus.ManagerApproval, FineAction.ManagerApproved, Roles.Finance, "other")]
    [InlineData(FineStatus.New, FineAction.Submitted, Roles.Operations, "other")]
    [InlineData(FineStatus.ManagerApproval, FineAction.ManagerApproved, Roles.Manager, "owner")]
    [InlineData(FineStatus.FinanceApproval, FineAction.FinanceApproved, Roles.Finance, "owner")]
    [InlineData(FineStatus.New, FineAction.FinanceApproved, Roles.Finance, "other")]
    [InlineData(FineStatus.Completed, FineAction.Edited, Roles.Operations, "owner")]
    [InlineData(FineStatus.Completed, FineAction.Rejected, Roles.Finance, "other")]
    [InlineData(FineStatus.Rejected, FineAction.ManagerApproved, Roles.Manager, "other")]
    [InlineData(FineStatus.FinanceApproval, FineAction.Edited, Roles.Operations, "owner")]
    public void Unauthorized_or_invalid_transition_is_rejected(FineStatus status, FineAction action, string role, string id)
    {
        var fine = new FineRecord { Status = status, CreatedById = "owner" };
        Assert.Throws<BusinessRuleException>(() => Workflow.Next(fine, action, User(role, id), "Reason"));
    }
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("  ")]
    public void Rejection_requires_nonblank_reason(string? reason)
    {
        var fine = new FineRecord { Status = FineStatus.ManagerApproval, CreatedById = "owner" };
        Assert.Throws<BusinessRuleException>(() => Workflow.Next(fine, FineAction.Rejected, User(Roles.Manager), reason));
    }
}
