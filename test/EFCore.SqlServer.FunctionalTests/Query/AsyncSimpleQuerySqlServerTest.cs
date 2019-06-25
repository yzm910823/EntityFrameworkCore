﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.TestUtilities.Xunit;
using Xunit;
using Xunit.Abstractions;

// ReSharper disable AccessToDisposedClosure
// ReSharper disable InconsistentNaming
namespace Microsoft.EntityFrameworkCore.Query
{
    public class AsyncSimpleQuerySqlServerTest : AsyncSimpleQueryTestBase<NorthwindQuerySqlServerFixture<NoopModelCustomizer>>
    {
        // ReSharper disable once UnusedParameter.Local
        public AsyncSimpleQuerySqlServerTest(
            NorthwindQuerySqlServerFixture<NoopModelCustomizer> fixture, ITestOutputHelper testOutputHelper)
            : base(fixture)
        {
            Fixture.TestSqlLoggerFactory.Clear();
            //Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
        }

        [ConditionalFact]
        public Task Query_compiler_concurrency()
        {
            const int threadCount = 50;

            var tasks = new Task[threadCount];

            for (var i = 0; i < threadCount; i++)
            {
                tasks[i] = Task.Run(
                    () =>
                    {
                        using (var context = CreateContext())
                        {
                            using ((from c in context.Customers
                                    where c.City == "London"
                                    orderby c.CustomerID
                                    select (from o1 in context.Orders
                                            where o1.CustomerID == c.CustomerID
                                                  && o1.OrderDate.Value.Year == 1997
                                            orderby o1.OrderID
                                            select (from o2 in context.Orders
                                                    where o1.CustomerID == c.CustomerID
                                                    orderby o2.OrderID
                                                    select o1.OrderID)))
                                .GetEnumerator())
                            {
                            }
                        }
                    });
            }

            return Task.WhenAll(tasks);
        }

        [ConditionalFact(Skip = "Issue#16218")]
        public Task Race_when_context_disposed_before_query_termination()
        {
            DbSet<Customer> task;

            using (var context = CreateContext())
            {
                task = context.Customers;
            }

            return Assert.ThrowsAsync<ObjectDisposedException>(() => task.SingleAsync(c => c.CustomerID == "ALFKI"));
        }

        [ConditionalFact]
        public Task Single_Predicate_Cancellation()
        {
            return Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => Single_Predicate_Cancellation_test(Fixture.TestSqlLoggerFactory.CancelQuery()));
        }

        [ConditionalFact(Skip = "Issue#10914")]
        public async Task Concurrent_async_queries_are_serialized()
        {
            using (var context = CreateContext())
            {
                var task1 = context.Customers.Where(c => c.City == "México D.F.").ToListAsync();
                var task2 = context.Customers.Where(c => c.City == "London").ToListAsync();
                var task3 = context.Customers.Where(c => c.City == "Sao Paulo").ToListAsync();

                var tasks = await Task.WhenAll(task1, task2, task3);

                Assert.Equal(5, tasks[0].Count);
                Assert.Equal(6, tasks[1].Count);
                Assert.Equal(4, tasks[2].Count);
            }
        }

        [ConditionalFact(Skip = "Issue #14935. Cannot eval 'Except({from Customer c in value(Microsoft.EntityFrameworkCore.Query.Internal.EntityQueryable`1[Microsoft.EntityFrameworkCore.TestModels.Northwind.Customer]) where ([c].City == \"México D.F.\") select [c].CustomerID})'")]
        public async Task Concurrent_async_queries_are_serialized2()
        {
            using (var context = CreateContext())
            {
                await context.OrderDetails
                    .Where(od => od.OrderID > 0)
                    .Intersect(
                        context.OrderDetails
                            .Where(od => od.OrderID > 0))
                    .Intersect(
                        context.OrderDetails
                            .Where(od => od.OrderID > 0)).ToListAsync();
            }
        }

        [ConditionalFact(Skip = "Issue#10914")]
        public async Task Concurrent_async_queries_are_serialized_find()
        {
            using (var context = CreateContext())
            {
                var task1 = context.Customers.FindAsync("ALFKI").AsTask();
                var task2 = context.Customers.FindAsync("ANATR").AsTask();
                var task3 = context.Customers.FindAsync("FISSA").AsTask();

                var tasks = await Task.WhenAll(task1, task2, task3);

                Assert.NotNull(tasks[0]);
                Assert.NotNull(tasks[1]);
                Assert.NotNull(tasks[2]);
            }
        }

        [ConditionalFact(Skip = "Issue#10914")]
        public async Task Concurrent_async_queries_are_serialized_mixed1()
        {
            using (var context = CreateContext())
            {
                await context.Customers.ForEachAsync(
                    // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                    c => context.Orders.Where(o => o.CustomerID == c.CustomerID).ToList());
            }
        }

        [ConditionalFact(Skip = "Issue#10914")]
        public async Task Concurrent_async_queries_are_serialized_mixed2()
        {
            using (var context = CreateContext())
            {
                foreach (var c in context.Customers)
                {
                    await context.Orders.Where(o => o.CustomerID == c.CustomerID).ToListAsync();
                }
            }
        }

        [ConditionalFact]
        public async Task Concurrent_async_queries_when_raw_query()
        {
            using (var context = CreateContext())
            {
                await using (var asyncEnumerator = context.Customers.AsAsyncEnumerable().GetAsyncEnumerator())
                {
                    while (await asyncEnumerator.MoveNextAsync())
                    {
                        if (!context.GetService<IRelationalConnection>().IsMultipleActiveResultSetsEnabled)
                        {
                            // Not supported, we could make it work by triggering buffering
                            // from RelationalCommand.

                            await Assert.ThrowsAsync<InvalidOperationException>(
                                () => context.Database.ExecuteSqlRawAsync(
                                    "[dbo].[CustOrderHist] @CustomerID = {0}",
                                    asyncEnumerator.Current.CustomerID));
                        }
                        else
                        {
                            await context.Database.ExecuteSqlRawAsync(
                                "[dbo].[CustOrderHist] @CustomerID = {0}",
                                asyncEnumerator.Current.CustomerID);
                        }
                    }
                }
            }
        }

        [ConditionalFact(Skip = "Issue#16218")]
        public override Task Select_bitwise_and_with_logical_and()
        {
            return base.Select_bitwise_and_with_logical_and();
        }

        [ConditionalFact(Skip = "Issue#16218")]
        public override Task Mixed_sync_async_in_query_cache()
        {
            return base.Mixed_sync_async_in_query_cache();
        }
    }
}
