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
            UseRelationalNulls = RelationalOptionsExtension.Extract(queryCompilationContext.ContextOptions).UseRelationalNulls;
            SqlExpressionFactory = sqlExpressionFactory;
        }

        protected ISqlExpressionFactory SqlExpressionFactory { get; }
        protected bool UseRelationalNulls { get; }

        public override Expression Visit(Expression query)
        {
            query = base.Visit(query);
            query = new SelectExpressionProjectionApplyingExpressionVisitor().Visit(query);
            query = new CollectionJoinApplyingExpressionVisitor().Visit(query);
            query = new SelectExpressionTableAliasUniquifyingExpressionVisitor().Visit(query);

            if (!UseRelationalNulls)
            {
                query = new NullSemanticsRewritingVisitor(SqlExpressionFactory).Visit(query);
            }

            query = new SqlExpressionOptimizingVisitor(SqlExpressionFactory, UseRelationalNulls).Visit(query);
            query = new NullComparisonTransformingExpressionVisitor().Visit(query);

            return query;
        }
    }

    public class CollectionJoinApplyingExpressionVisitor : ExpressionVisitor
    {
        private int _collectionId;

        protected override Expression VisitExtension(Expression extensionExpression)
        {
            if (extensionExpression is CollectionShaperExpression collectionShaperExpression)
            {
                var collectionId = _collectionId++;

                var innerShaper = Visit(collectionShaperExpression.InnerShaper);

                var selectExpression = (SelectExpression)collectionShaperExpression.Projection.QueryExpression;
                return selectExpression.ApplyCollectionJoin(
                    collectionShaperExpression.Projection.Index.Value,
                    collectionId,
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
