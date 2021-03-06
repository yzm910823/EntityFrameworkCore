﻿// <auto-generated />

using System;
using System.Reflection;
using System.Resources;
using JetBrains.Annotations;

namespace Microsoft.EntityFrameworkCore.Cosmos.Internal
{
    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public static class CosmosStrings
    {
        private static readonly ResourceManager _resourceManager
            = new ResourceManager("Microsoft.EntityFrameworkCore.Cosmos.Properties.CosmosStrings", typeof(CosmosStrings).GetTypeInfo().Assembly);

        /// <summary>
        ///     Cosmos-specific methods can only be used when the context is using the Cosmos provider.
        /// </summary>
        public static string CosmosNotInUse
            => GetString("CosmosNotInUse");

        /// <summary>
        ///     The discriminator value for '{entityType1}' is '{discriminatorValue}' which is the same for '{entityType2}'. Every concrete entity type mapped to the container '{container}' needs to have a unique discriminator value.
        /// </summary>
        public static string DuplicateDiscriminatorValue([CanBeNull] object entityType1, [CanBeNull] object discriminatorValue, [CanBeNull] object entityType2, [CanBeNull] object container)
            => string.Format(
                GetString("DuplicateDiscriminatorValue", nameof(entityType1), nameof(discriminatorValue), nameof(entityType2), nameof(container)),
                entityType1, discriminatorValue, entityType2, container);

        /// <summary>
        ///     The entity type '{entityType}' is sharing the container '{container}' with other types, but does not have a discriminator property configured.
        /// </summary>
        public static string NoDiscriminatorProperty([CanBeNull] object entityType, [CanBeNull] object container)
            => string.Format(
                GetString("NoDiscriminatorProperty", nameof(entityType), nameof(container)),
                entityType, container);

        /// <summary>
        ///     The entity type '{entityType}' is sharing the container '{container}' with other types, but does not have a discriminator value configured.
        /// </summary>
        public static string NoDiscriminatorValue([CanBeNull] object entityType, [CanBeNull] object container)
            => string.Format(
                GetString("NoDiscriminatorValue", nameof(entityType), nameof(container)),
                entityType, container);

        /// <summary>
        ///     The entity type '{entityType}' does not have a partition key set, but it is mapped to the collection '{collection}' shared by entity types with partition keys.
        /// </summary>
        public static string NoPartitionKey([CanBeNull] object entityType, [CanBeNull] object collection)
            => string.Format(
                GetString("NoPartitionKey", nameof(entityType), nameof(collection)),
                entityType, collection);

        /// <summary>
        ///     The entity of type '{entityType}' is mapped as a part of the document mapped to '{missingEntityType}', but there is no tracked entity of this type with the corresponding key value. Consider using 'DbContextOptionsBuilder.EnableSensitiveDataLogging' to see the key values.
        /// </summary>
        public static string OrphanedNestedDocument([CanBeNull] object entityType, [CanBeNull] object missingEntityType)
            => string.Format(
                GetString("OrphanedNestedDocument", nameof(entityType), nameof(missingEntityType)),
                entityType, missingEntityType);

        /// <summary>
        ///     The entity of type '{entityType}' is mapped as a part of the document mapped to '{missingEntityType}', but there is no tracked entity of this type with the key value '{keyValue}'.
        /// </summary>
        public static string OrphanedNestedDocumentSensitive([CanBeNull] object entityType, [CanBeNull] object missingEntityType, [CanBeNull] object keyValue)
            => string.Format(
                GetString("OrphanedNestedDocumentSensitive", nameof(entityType), nameof(missingEntityType), nameof(keyValue)),
                entityType, missingEntityType, keyValue);

        /// <summary>
        ///     The partition key for entity type '{entityType}' is set to '{property}', but there is no property with that name.
        /// </summary>
        public static string PartitionKeyMissingProperty([CanBeNull] object entityType, [CanBeNull] object property)
            => string.Format(
                GetString("PartitionKeyMissingProperty", nameof(entityType), nameof(property)),
                entityType, property);

        /// <summary>
        ///     The partition key property '{property1}' on '{entityType1}' is mapped as '{storeName1}', but the partition key property '{property2}' on '{entityType2}' is mapped as '{storeName2}'. All partition key properties need to be mapped to the same store property.
        /// </summary>
        public static string PartitionKeyStoreNameMismatch([CanBeNull] object property1, [CanBeNull] object entityType1, [CanBeNull] object storeName1, [CanBeNull] object property2, [CanBeNull] object entityType2, [CanBeNull] object storeName2)
            => string.Format(
                GetString("PartitionKeyStoreNameMismatch", nameof(property1), nameof(entityType1), nameof(storeName1), nameof(property2), nameof(entityType2), nameof(storeName2)),
                property1, entityType1, storeName1, property2, entityType2, storeName2);

        /// <summary>
        ///     The type of the partition key property '{property1}' on '{entityType1}' is '{propertyType1}', but the type of the partition key property '{property2}' on '{entityType2}' is '{propertyType2}'. All partition key properties need to have matching types.
        /// </summary>
        public static string PartitionKeyStoreTypeMismatch([CanBeNull] object property1, [CanBeNull] object entityType1, [CanBeNull] object propertyType1, [CanBeNull] object property2, [CanBeNull] object entityType2, [CanBeNull] object propertyType2)
            => string.Format(
                GetString("PartitionKeyStoreTypeMismatch", nameof(property1), nameof(entityType1), nameof(propertyType1), nameof(property2), nameof(entityType2), nameof(propertyType2)),
                property1, entityType1, propertyType1, property2, entityType2, propertyType2);

        private static string GetString(string name, params string[] formatterNames)
        {
            var value = _resourceManager.GetString(name);
            for (var i = 0; i < formatterNames.Length; i++)
            {
                value = value.Replace("{" + formatterNames[i] + "}", "{" + i + "}");
            }

            return value;
        }
    }
}

