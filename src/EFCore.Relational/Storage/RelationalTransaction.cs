// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Utilities;

namespace Microsoft.EntityFrameworkCore.Storage
{
    /// <summary>
    ///     <para>
    ///         A transaction against the database.
    ///     </para>
    ///     <para>
    ///         Instances of this class are typically obtained from <see cref="DatabaseFacade.BeginTransaction" /> and it is not designed
    ///         to be directly constructed in your application code.
    ///     </para>
    /// </summary>
    public class RelationalTransaction : IDbContextTransaction, IInfrastructure<DbTransaction>
    {
        private readonly DbTransaction _dbTransaction;
        private readonly bool _transactionOwned;

        private bool _connectionClosed;
        private bool _disposed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RelationalTransaction" /> class.
        /// </summary>
        /// <param name="connection"> The connection to the database. </param>
        /// <param name="transaction"> The underlying <see cref="DbTransaction" />. </param>
        /// <param name="transactionId"> The correlation ID for the transaction. </param>
        /// <param name="logger"> The logger to write to. </param>
        /// <param name="transactionOwned">
        ///     A value indicating whether the transaction is owned by this class (i.e. if it can be disposed when this class is disposed).
        /// </param>
        public RelationalTransaction(
            [NotNull] IRelationalConnection connection,
            [NotNull] DbTransaction transaction,
            Guid transactionId,
            [NotNull] IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger,
            bool transactionOwned)
        {
            Check.NotNull(connection, nameof(connection));
            Check.NotNull(transaction, nameof(transaction));
            Check.NotNull(logger, nameof(logger));

            if (connection.DbConnection != transaction.Connection)
            {
                throw new InvalidOperationException(RelationalStrings.TransactionAssociatedWithDifferentConnection);
            }

            Connection = connection;
            TransactionId = transactionId;

            _dbTransaction = transaction;
            Logger = logger;
            _transactionOwned = transactionOwned;
        }

        /// <summary>
        ///     The connection.
        /// </summary>
        protected virtual IRelationalConnection Connection { get; }

        /// <summary>
        ///     The logger.
        /// </summary>
        protected virtual IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> Logger { get; }

        /// <summary>
        ///     A correlation ID that allows this transaction to be identified and
        ///     correlated across multiple database calls.
        /// </summary>
        public virtual Guid TransactionId { get; }

        /// <summary>
        ///     Commits all changes made to the database in the current transaction.
        /// </summary>
        public virtual void Commit()
        {
            var startTime = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var interceptionResult = Logger.TransactionCommitting(
                    Connection,
                    _dbTransaction,
                    TransactionId,
                    startTime);

                if (interceptionResult == null)
                {
                    _dbTransaction.Commit();
                }

                Logger.TransactionCommitted(
                    Connection,
                    _dbTransaction,
                    TransactionId,
                    startTime,
                    stopwatch.Elapsed);
            }
            catch (Exception e)
            {
                Logger.TransactionError(
                    Connection,
                    _dbTransaction,
                    TransactionId,
                    "Commit",
                    e,
                    startTime,
                    stopwatch.Elapsed);

                throw;
            }

            ClearTransaction();
        }

        /// <summary>
        ///     Discards all changes made to the database in the current transaction.
        /// </summary>
        public virtual void Rollback()
        {
            var startTime = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var interceptionResult = Logger.TransactionRollingBack(
                    Connection,
                    _dbTransaction,
                    TransactionId,
                    startTime);

                if (interceptionResult == null)
                {
                    _dbTransaction.Rollback();
                }

                Logger.TransactionRolledBack(
                    Connection,
                    _dbTransaction,
                    TransactionId,
                    startTime,
                    stopwatch.Elapsed);
            }
            catch (Exception e)
            {
                Logger.TransactionError(
                    Connection,
                    _dbTransaction,
                    TransactionId,
                    "Rollback",
                    e,
                    startTime,
                    stopwatch.Elapsed);

                throw;
            }

            ClearTransaction();
        }

        /// <summary>
        ///     Commits all changes made to the database in the current transaction asynchronously.
        /// </summary>
        /// <param name="cancellationToken"> The cancellation token. </param>
        /// <returns> A <see cref="Task"/> representing the asynchronous operation. </returns>
        public virtual async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            var startTime = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var interceptionResult = await Logger.TransactionCommittingAsync(
                    Connection,
                    _dbTransaction,
                    TransactionId,
                    startTime,
                    cancellationToken);

                if (interceptionResult == null)
                {
                    _dbTransaction.Commit();
                }

                await Logger.TransactionCommittedAsync(
                    Connection,
                    _dbTransaction,
                    TransactionId,
                    startTime,
                    stopwatch.Elapsed,
                    cancellationToken);
            }
            catch (Exception e)
            {
                await Logger.TransactionErrorAsync(
                    Connection,
                    _dbTransaction,
                    TransactionId,
                    "Commit",
                    e,
                    startTime,
                    stopwatch.Elapsed,
                    cancellationToken);

                throw;
            }

            ClearTransaction();
        }

        /// <summary>
        ///     Discards all changes made to the database in the current transaction asynchronously.
        /// </summary>
        /// <param name="cancellationToken"> The cancellation token. </param>
        /// <returns> A <see cref="Task"/> representing the asynchronous operation. </returns>
        public virtual async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            var startTime = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var interceptionResult = await Logger.TransactionRollingBackAsync(
                    Connection,
                    _dbTransaction,
                    TransactionId,
                    startTime,
                    cancellationToken);

                if (interceptionResult == null)
                {
                    _dbTransaction.Rollback(); // Use RollbackAsync when available
                }

                await Logger.TransactionRolledBackAsync(
                    Connection,
                    _dbTransaction,
                    TransactionId,
                    startTime,
                    stopwatch.Elapsed,
                    cancellationToken);
            }
            catch (Exception e)
            {
                await Logger.TransactionErrorAsync(
                    Connection,
                    _dbTransaction,
                    TransactionId,
                    "Rollback",
                    e,
                    startTime,
                    stopwatch.Elapsed,
                    cancellationToken);

                throw;
            }

            ClearTransaction();
        }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public virtual void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                if (_transactionOwned)
                {
                    _dbTransaction.Dispose();

                    Logger.TransactionDisposed(
                        Connection,
                        _dbTransaction,
                        TransactionId,
                        DateTimeOffset.UtcNow);
                }

                ClearTransaction();
            }
        }

        /// <summary>
        ///     Remove the underlying transaction from the connection
        /// </summary>
        protected virtual void ClearTransaction()
        {
            Debug.Assert(Connection.CurrentTransaction == null || Connection.CurrentTransaction == this);

            Connection.UseTransaction(null);

            if (!_connectionClosed)
            {
                _connectionClosed = true;

                Connection.Close();
            }
        }

        DbTransaction IInfrastructure<DbTransaction>.Instance => _dbTransaction;
    }
}
