﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Pipeline;
using Microsoft.EntityFrameworkCore.Relational.Query.Pipeline.SqlExpressions;

namespace Microsoft.EntityFrameworkCore.Relational.Query.Pipeline
{
    public class RelationalShapedQueryOptimizer : ShapedQueryOptimizer
    {
        public RelationalShapedQueryOptimizer(
            QueryCompilationContext queryCompilationContext,
            ISqlExpressionFactory sqlExpressionFactory)
        {
            QueryCompilationContext = queryCompilationContext;
            SqlExpressionFactory = sqlExpressionFactory;
        }

        protected QueryCompilationContext QueryCompilationContext { get; private set; }
        protected ISqlExpressionFactory SqlExpressionFactory { get; private set; }

        public override Expression Visit(Expression query)
        {
            query = base.Visit(query);
            query = new SelectExpressionProjectionApplyingExpressionVisitor().Visit(query);
            query = new CollectionJoinApplyingExpressionVisitor().Visit(query);
            query = new SelectExpressionTableAliasUniquifyingExpressionVisitor().Visit(query);

            var useRelationalNulls = RelationalOptionsExtension.Extract(QueryCompilationContext.ContextOptions).UseRelationalNulls;
            if (!useRelationalNulls)
            {
                query = new NullSemanticsRewritingVisitor(SqlExpressionFactory).Visit(query);
            }

            query = new SqlExpressionOptimizingVisitor(SqlExpressionFactory, useRelationalNulls).Visit(query);
            query = new NullComparisonTransformingExpressionVisitor().Visit(query);

            if (query is ShapedQueryExpression shapedQueryExpression)
            {
                shapedQueryExpression.ShaperExpression
                    = new ShaperExpressionProcessingExpressionVisitor((SelectExpression)shapedQueryExpression.QueryExpression)
                        .Inject(shapedQueryExpression.ShaperExpression);
            }

            return query;
        }
    }

    public class CollectionJoinApplyingExpressionVisitor : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression extensionExpression)
        {
            if (extensionExpression is CollectionShaperExpression collectionShaperExpression)
            {
                var innerShaper = Visit(collectionShaperExpression.InnerShaper);

                var selectExpression = (SelectExpression)collectionShaperExpression.Projection.QueryExpression;
                return selectExpression.ApplyCollectionJoin(
                    collectionShaperExpression.Projection.Index.Value,
                    innerShaper,
                    collectionShaperExpression.Navigation);
            }

            if (extensionExpression is ShapedQueryExpression shapedQueryExpression)
            {
                shapedQueryExpression.ShaperExpression = Visit(shapedQueryExpression.ShaperExpression);

                return shapedQueryExpression;
            }

            return base.VisitExtension(extensionExpression);
        }
    }
}
