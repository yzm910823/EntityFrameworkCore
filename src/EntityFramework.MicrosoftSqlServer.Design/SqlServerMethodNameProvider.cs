﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Data.Entity.Scaffolding.Internal;

namespace Microsoft.Data.Entity.Scaffolding
{
    // TODO remove
    public class SqlServerMethodNameProvider : IMethodNameProvider
    {
        public virtual string DbOptionBuilderExtension { get; } = nameof(SqlServerDbContextOptionsExtensions.UseSqlServer);
    }
}