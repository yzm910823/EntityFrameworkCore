// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Microsoft.EntityFrameworkCore.Metadata.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class PropertyAccessorsFactory
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual PropertyAccessors Create([NotNull] IPropertyBase propertyBase)
        {
            var memberInfo = propertyBase.GetIdentifyingMemberInfo() == null
                ? null
                : propertyBase.GetMemberInfo(forConstruction: false, forSet: false);

            var propertyType = propertyBase.ClrType;

            return (PropertyAccessors)_genericCreate
                .MakeGenericMethod(propertyType, memberInfo?.GetMemberType() ?? propertyType)
                .Invoke(null, new object[] { propertyBase, memberInfo });
        }

        private static readonly MethodInfo _genericCreate
            = typeof(PropertyAccessorsFactory).GetTypeInfo().GetDeclaredMethod(nameof(CreateGeneric));

        [UsedImplicitly]
        private static PropertyAccessors CreateGeneric<TProperty, TMember>(
            IPropertyBase propertyBase,
            MemberInfo memberInfo)
        {
            var currentValueGetterExpression = CreateCurrentValueGetterExpression<TMember>(
                propertyBase, memberInfo, useStoreGeneratedValues: true);
            var currentValueGetter = currentValueGetterExpression.Compile();

            var preStoreGeneratedCurrentValueGetterExpression =
                CreateCurrentValueGetterExpression<TMember>(propertyBase, memberInfo, useStoreGeneratedValues: false);
            var preStoreGeneratedCurrentValueGetter = preStoreGeneratedCurrentValueGetterExpression.Compile();

            var property = propertyBase as IProperty;
            var originalValueGetterExpression = property == null ? null : CreateOriginalValueGetterExpression<TMember>(property);
            var originalValueGetter = originalValueGetterExpression?.Compile();

            var relationshipSnapshotGetterExpression = CreateRelationshipSnapshotGetterExpression<TMember>(propertyBase);
            var relationshipSnapshotGetter = relationshipSnapshotGetterExpression.Compile();

            var valueBufferGetter = property == null ? null : CreateValueBufferGetter(property);

            if (typeof(TProperty) == typeof(TMember))
            {
                return new PropertyAccessors(
                    currentValueGetter,
                    currentValueGetter,
                    preStoreGeneratedCurrentValueGetter,
                    preStoreGeneratedCurrentValueGetter,
                    originalValueGetter,
                    originalValueGetter,
                    relationshipSnapshotGetter,
                    relationshipSnapshotGetter,
                    valueBufferGetter);
            }

            var convertedCurrentValueGetter
                = Expression.Lambda<Func<InternalEntityEntry, TProperty>>(
                        Expression.Convert(
                            currentValueGetterExpression.Body,
                            typeof(TProperty)),
                        currentValueGetterExpression.Parameters[0])
                    .Compile();

            var convertedPreStoreGeneratedCurrentValueGetter
                = Expression.Lambda<Func<InternalEntityEntry, TProperty>>(
                        Expression.Convert(
                            preStoreGeneratedCurrentValueGetterExpression.Body,
                            typeof(TProperty)),
                        preStoreGeneratedCurrentValueGetterExpression.Parameters[0])
                    .Compile();

            var convertedOriginalValueGetter
                = originalValueGetter == null
                    ? null
                    : Expression.Lambda<Func<InternalEntityEntry, TProperty>>(
                            Expression.Convert(
                                originalValueGetterExpression.Body,
                                typeof(TProperty)),
                            originalValueGetterExpression.Parameters[0])
                        .Compile();

            var convertedRelationshipSnapshotGetter
                = Expression.Lambda<Func<InternalEntityEntry, TProperty>>(
                        Expression.Convert(
                            relationshipSnapshotGetterExpression.Body,
                            typeof(TProperty)),
                        relationshipSnapshotGetterExpression.Parameters[0])
                    .Compile();

            return new PropertyAccessors(
                currentValueGetter,
                convertedCurrentValueGetter,
                preStoreGeneratedCurrentValueGetter,
                convertedPreStoreGeneratedCurrentValueGetter,
                originalValueGetter,
                convertedOriginalValueGetter,
                relationshipSnapshotGetter,
                convertedRelationshipSnapshotGetter,
                valueBufferGetter);
        }

        private static Expression<Func<InternalEntityEntry, TMember>> CreateCurrentValueGetterExpression<TMember>(
            IPropertyBase propertyBase,
            MemberInfo memberInfo,
            bool useStoreGeneratedValues)
        {
            var entityClrType = propertyBase.DeclaringType.ClrType;
            var entryParameter = Expression.Parameter(typeof(InternalEntityEntry), "entry");

            var shadowIndex = propertyBase.GetShadowIndex();
            Expression currentValueExpression;
            if (shadowIndex >= 0)
            {
                currentValueExpression = Expression.Call(
                    entryParameter,
                    InternalEntityEntry.ReadShadowValueMethod.MakeGenericMethod(typeof(TMember)),
                    Expression.Constant(shadowIndex));
            }
            else
            {
                var convertedExpression = Expression.Convert(
                    Expression.Property(entryParameter, "Entity"),
                    entityClrType);

                if (propertyBase.IsIndexedProperty())
                {
                    currentValueExpression = Expression.MakeIndex(
                        convertedExpression,
                        propertyBase.PropertyInfo,
                        new [] { Expression.Constant(propertyBase.Name) });
                }
                else
                {
                    currentValueExpression = Expression.MakeMemberAccess(
                        convertedExpression,
                        memberInfo);
                }
            }

            var storeGeneratedIndex = propertyBase.GetStoreGeneratedIndex();
            if (storeGeneratedIndex >= 0)
            {
                if (useStoreGeneratedValues)
                {
                    currentValueExpression = Expression.Condition(
                        Expression.Equal(
                            currentValueExpression,
                            Expression.Constant(default(TMember), typeof(TMember))),
                        Expression.Call(
                            entryParameter,
                            InternalEntityEntry.ReadStoreGeneratedValueMethod.MakeGenericMethod(typeof(TMember)),
                            Expression.Constant(storeGeneratedIndex)),
                        currentValueExpression);
                }

                currentValueExpression = Expression.Condition(
                    Expression.Equal(
                        currentValueExpression,
                        Expression.Constant(default(TMember), typeof(TMember))),
                    Expression.Call(
                        entryParameter,
                        InternalEntityEntry.ReadTemporaryValueMethod.MakeGenericMethod(typeof(TMember)),
                        Expression.Constant(storeGeneratedIndex)),
                    currentValueExpression);
            }

            return Expression.Lambda<Func<InternalEntityEntry, TMember>>(
                    currentValueExpression,
                    entryParameter);
        }

        private static Expression<Func<InternalEntityEntry, TMember>> CreateOriginalValueGetterExpression<TMember>(IProperty property)
        {
            var entryParameter = Expression.Parameter(typeof(InternalEntityEntry), "entry");
            var originalValuesIndex = property.GetOriginalValueIndex();

            return Expression.Lambda<Func<InternalEntityEntry, TMember>>(
                    originalValuesIndex >= 0
                        ? (Expression)Expression.Call(
                            entryParameter,
                            InternalEntityEntry.ReadOriginalValueMethod.MakeGenericMethod(typeof(TMember)),
                            Expression.Constant(property),
                            Expression.Constant(originalValuesIndex))
                        : Expression.Block(
                            Expression.Throw(
                                Expression.Constant(
                                    new InvalidOperationException(
                                        CoreStrings.OriginalValueNotTracked(property.Name, property.DeclaringEntityType.DisplayName())))),
                            Expression.Constant(default(TMember), typeof(TMember))),
                    entryParameter);
        }

        private static Expression<Func<InternalEntityEntry, TMember>> CreateRelationshipSnapshotGetterExpression<TMember>(IPropertyBase propertyBase)
        {
            var entryParameter = Expression.Parameter(typeof(InternalEntityEntry), "entry");
            var relationshipIndex = (propertyBase as IProperty)?.GetRelationshipIndex() ?? -1;

            return Expression.Lambda<Func<InternalEntityEntry, TMember>>(
                    relationshipIndex >= 0
                        ? Expression.Call(
                            entryParameter,
                            InternalEntityEntry.ReadRelationshipSnapshotValueMethod.MakeGenericMethod(typeof(TMember)),
                            Expression.Constant(propertyBase),
                            Expression.Constant(relationshipIndex))
                        : Expression.Call(
                            entryParameter,
                            InternalEntityEntry.GetBackingCurrentValueMethod.MakeGenericMethod(typeof(TMember)),
                            Expression.Constant(propertyBase)),
                    entryParameter);
        }

        private static Func<ValueBuffer, object> CreateValueBufferGetter(IProperty property)
        {
            var valueBufferParameter = Expression.Parameter(typeof(ValueBuffer), "valueBuffer");

            return Expression.Lambda<Func<ValueBuffer, object>>(
                    Expression.Call(
                        valueBufferParameter,
                        ValueBuffer.GetValueMethod,
                        Expression.Constant(property.GetIndex())),
                    valueBufferParameter)
                .Compile();
        }
    }
}
