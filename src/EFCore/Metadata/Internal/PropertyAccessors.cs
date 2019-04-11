// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Storage;

namespace Microsoft.EntityFrameworkCore.Metadata.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public sealed class PropertyAccessors
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public PropertyAccessors(
            [NotNull] Delegate currentValueGetter,
            [NotNull] Delegate convertedCurrentValueGetter,
            [NotNull] Delegate preStoreGeneratedCurrentValueGetter,
            [NotNull] Delegate convertedPreStoreGeneratedCurrentValueGetter,
            [CanBeNull] Delegate originalValueGetter,
            [CanBeNull] Delegate convertedOriginalValueGetter,
            [NotNull] Delegate relationshipSnapshotGetter,
            [NotNull] Delegate convertedRelationshipSnapshotGetter,
            [CanBeNull] Func<ValueBuffer, object> valueBufferGetter)
        {
            CurrentValueGetter = currentValueGetter;
            ConvertedCurrentValueGetter = convertedCurrentValueGetter;
            PreStoreGeneratedCurrentValueGetter = preStoreGeneratedCurrentValueGetter;
            ConvertedPreStoreGeneratedCurrentValueGetter = convertedPreStoreGeneratedCurrentValueGetter;
            OriginalValueGetter = originalValueGetter;
            ConvertedOriginalValueGetter = convertedOriginalValueGetter;
            RelationshipSnapshotGetter = relationshipSnapshotGetter;
            ConvertedRelationshipSnapshotGetter = convertedRelationshipSnapshotGetter;
            ValueBufferGetter = valueBufferGetter;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public Delegate CurrentValueGetter { get; }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public Delegate ConvertedCurrentValueGetter { get; }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public Delegate PreStoreGeneratedCurrentValueGetter { get; }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public Delegate ConvertedPreStoreGeneratedCurrentValueGetter { get; }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public Delegate OriginalValueGetter { get; }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public Delegate ConvertedOriginalValueGetter { get; }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public Delegate RelationshipSnapshotGetter { get; }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public Delegate ConvertedRelationshipSnapshotGetter { get; }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public Func<ValueBuffer, object> ValueBufferGetter { get; }
    }
}
