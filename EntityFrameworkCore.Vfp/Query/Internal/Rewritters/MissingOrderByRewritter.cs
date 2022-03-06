using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace EntityFrameworkCore.Vfp.Query.Internal.Rewritters {
    public class MissingOrderByRewritter : ExpressionVisitor {
        private static readonly ConstructorInfo ColumnExpressionConstuctorInfo;

        static MissingOrderByRewritter() {
            var type = typeof(ColumnExpression);
            var paramTypes = new[] { typeof(IProperty), typeof(IColumnBase), typeof(TableExpressionBase), typeof(bool) };

            ColumnExpressionConstuctorInfo = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, paramTypes, null);
        }

        protected override Expression VisitExtension(Expression expression) {
            if(expression is ShapedQueryExpression shapedQueryExpression) {
                if(shapedQueryExpression.QueryExpression is SelectExpression selectExpression &&
                    selectExpression.Limit != null &&
                    !selectExpression.Orderings.Any() &&
                    selectExpression.Tables.Count() == 1 &&
                    shapedQueryExpression.ShaperExpression is EntityShaperExpression entityShaperExpression &&
                    entityShaperExpression.EntityType.FindPrimaryKey() != null &&
                    selectExpression.Tables.Single() is TableExpression tableExpression
                ) {
                    var entityType = entityShaperExpression.EntityType;
                    var table = entityType.GetViewOrTableMappings().Single().Table;
                    var propertyExpressions = new Dictionary<IProperty, ColumnExpression>();

                    foreach(var property in entityType
                        .GetAllBaseTypes().Concat(entityType.GetDerivedTypesInclusive())
                        .SelectMany(e => e.GetDeclaredProperties())) {

                        var columnExpression = CreateColumnExpression(property, table.FindColumn(property), tableExpression, false);

                        propertyExpressions[property] = columnExpression;
                    }

                    var entityProjectionExpression = new EntityProjectionExpression(entityType, propertyExpressions, null);

                    foreach(var property in entityType.FindPrimaryKey().Properties) {
                        var columnExpression = entityProjectionExpression.BindProperty(property);

                        selectExpression.AppendOrdering(new OrderingExpression(columnExpression, true));
                    }
                }

                return shapedQueryExpression.Update(Visit(shapedQueryExpression.QueryExpression), shapedQueryExpression.ShaperExpression);
            }

            return base.VisitExtension(expression);
        }

        public static ColumnExpression CreateColumnExpression(IProperty property, IColumnBase column, TableExpressionBase table, bool nullable) {
            var paramValues = new object[] { property, column, table, nullable };

            return new MyColumnExpression(property, column, table, nullable);
        }

        internal class MyColumnExpression : ColumnExpression
        {
            public static Type UnwrapNullableType(Type type)
                => Nullable.GetUnderlyingType(type) ?? type;
            internal MyColumnExpression(IProperty property, IColumnBase column, TableExpressionBase table, bool nullable)
                : this(
                    column.Name,
                    table,
                    UnwrapNullableType(property.ClrType),
                    column.PropertyMappings.First(m => m.Property == property).TypeMapping,
                    nullable || column.IsNullable)
            {
            }

            private MyColumnExpression(string name, TableExpressionBase table, Type type, RelationalTypeMapping typeMapping, bool nullable)
                : base(type, typeMapping)
            {
                Name = name;
                Table = table;
                IsNullable = nullable;
            }

            public override string Name { get; }

            public override TableExpressionBase Table { get; }

            public override bool IsNullable { get; }

            public override string TableAlias => Table.Alias;

            protected override Expression VisitChildren(ExpressionVisitor visitor)
                => this;

            public override ColumnExpression MakeNullable()
                => new MyColumnExpression(Name, Table, Type, TypeMapping, true);
        }
    }
}
