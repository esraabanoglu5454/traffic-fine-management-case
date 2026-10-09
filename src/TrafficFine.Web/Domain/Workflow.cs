namespace TrafficFine.Web.Domain;

public sealed class BusinessRuleException(string message) : Exception(message);
public sealed record Actor(string Id, string Name, IReadOnlySet<string> Roles)
{
    public bool HasRole(string role) => Roles.Contains(role);
}
public static class Workflow
{
    public static FineStatus Next(FineRecord fine, FineAction action, Actor actor, string? comment)
    {
        var owns = fine.CreatedById == actor.Id && actor.HasRole(Roles.Operations);
        return (fine.Status, action) switch
        {
            (FineStatus.New, FineAction.Submitted) when owns => FineStatus.ManagerApproval,
            (FineStatus.New, FineAction.Edited) when owns => FineStatus.New,
            (FineStatus.Rejected, FineAction.Revised) when owns => FineStatus.New,
            (FineStatus.ManagerApproval, FineAction.ManagerApproved) when actor.HasRole(Roles.Manager)
                && fine.CreatedById != actor.Id => FineStatus.FinanceApproval,
            (FineStatus.FinanceApproval, FineAction.FinanceApproved) when actor.HasRole(Roles.Finance)
                && fine.CreatedById != actor.Id => FineStatus.Completed,
            (FineStatus.ManagerApproval, FineAction.Rejected) when actor.HasRole(Roles.Manager)
                && fine.CreatedById != actor.Id => Reject(comment),
            (FineStatus.FinanceApproval, FineAction.Rejected) when actor.HasRole(Roles.Finance)
                && fine.CreatedById != actor.Id => Reject(comment),
            _ => throw new BusinessRuleException("Bu işlem için yetkiniz yok veya cezanın durumu değişmiş. Sayfayı yenileyin.")
        };
    }
    private static FineStatus Reject(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            throw new BusinessRuleException("Ret nedeni zorunludur.");
        return FineStatus.Rejected;
    }
}
