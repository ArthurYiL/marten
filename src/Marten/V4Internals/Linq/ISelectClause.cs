using Marten.Linq;
using Marten.Util;

namespace Marten.V4Internals.Linq
{
    public interface ISelectClause
    {
        string FromObject { get; }

        void WriteSelectClause(CommandBuilder sql);

        string[] SelectFields();

        ISelector BuildSelector(IMartenSession session);
        IQueryHandler<T> BuildHandler<T>(IMartenSession session, Statement topStatement);
        ISelectClause UseStatistics(QueryStatistics statistics);
    }


}
