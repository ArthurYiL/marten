using System;
using System.Linq;
using Baseline;
using Marten.Linq;
using Marten.Linq.QueryHandlers;
using Marten.Util;
using Marten.V4Internals.Linq.QueryHandlers;

namespace Marten.V4Internals.Linq
{
    public class StatsSelectClause<T> : ISelectClause
    {
        private readonly QueryStatistics _statistics;

        public StatsSelectClause(ISelectClause inner, QueryStatistics statistics)
        {
            Inner = inner;
            _statistics = statistics;
        }

        public ISelectClause Inner { get; }

        public Type SelectedType => Inner.SelectedType;

        public string FromObject => Inner.FromObject;

        public void WriteSelectClause(CommandBuilder sql)
        {
            sql.Append("select ");
            sql.Append(Inner.SelectFields().Join(", "));
            sql.Append(", ");
            sql.Append(LinqConstants.StatsColumn);
            sql.Append(" from ");
            sql.Append(Inner.FromObject);
            sql.Append(" as d");
        }

        public string[] SelectFields()
        {
            return Inner.SelectFields().Concat(new string[]{LinqConstants.StatsColumn}).ToArray();
        }

        public ISelector BuildSelector(IMartenSession session)
        {
            throw new System.NotSupportedException();
        }

        public IQueryHandler<TResult> BuildHandler<TResult>(IMartenSession session, Statement topStatement,
            Statement currentStatement)
        {
            var selector = (ISelector<T>)Inner.BuildSelector(session);

            var handler = new ListWithStatsQueryHandler<T>(Inner.SelectFields().Length, topStatement, selector, _statistics);

            if (handler is IQueryHandler<TResult> h) return h;

            throw new NotSupportedException("QueryStatistics queries are only supported for enumerable results");
        }

        public ISelectClause UseStatistics(QueryStatistics statistics)
        {
            throw new InvalidOperationException("Double usage of QueryStatistics");
        }
    }
}
