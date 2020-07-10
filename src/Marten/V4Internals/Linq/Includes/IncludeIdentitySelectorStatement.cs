using System;
using System.Collections.Generic;
using System.Linq;
using Baseline;
using Marten.Linq;
using Marten.Linq.QueryHandlers;
using Marten.Util;

namespace Marten.V4Internals.Linq.Includes
{
    public class IncludeIdentitySelectorStatement : Statement, ISelectClause
    {
        private readonly IList<IIncludePlan> _includes;

        public IncludeIdentitySelectorStatement(DocumentStatement original, IList<IIncludePlan> includes) : base(null, null)
        {
            _includes = includes;
            Inner = original.Clone();
            Inner.SelectClause = this;

            // Retrieve data from the original table
            FromObject = original.SelectClause.FromObject;

            Statement current = this;
            foreach (var include in includes)
            {
                var includeStatement = include.BuildStatement();
                current.Next = includeStatement;
                current = includeStatement;
            }

            current.Next = original;

            original.Where = new InTempTableWhereFragment(LinqConstants.IdListTableName, "id");
            original.Limit = 0;
            original.Offset = 0;
        }

        public Type SelectedType => typeof(void);

        public Statement Inner { get; }

        protected override void configure(CommandBuilder sql)
        {
            sql.Append("create temp table ");
            sql.Append(LinqConstants.IdListTableName);
            sql.Append(" as (\n");
            Inner.Configure(sql);
            sql.Append("\n);");
        }

        public string FromObject { get; }
        public void WriteSelectClause(CommandBuilder sql)
        {
            sql.Append("select id, ");
            sql.Append(_includes.Select(x => x.TempSelector).Join(", "));
            sql.Append(" from ");
            sql.Append(FromObject);
            sql.Append(" as d");
        }

        public string[] SelectFields()
        {
            return _includes.Select(x => x.TempSelector).ToArray();
        }

        public ISelector BuildSelector(IMartenSession session)
        {
            throw new System.NotSupportedException();
        }

        public IQueryHandler<T> BuildHandler<T>(IMartenSession session, Statement topStatement,
            Statement currentStatement)
        {
            throw new System.NotSupportedException();
        }

        public ISelectClause UseStatistics(QueryStatistics statistics)
        {
            throw new System.NotSupportedException();
        }
    }
}
