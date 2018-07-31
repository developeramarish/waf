﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows.Controls;

namespace System.Waf.Presentation.Controls
{
    /// <summary>
    /// Provides helper methods for working with the DataGrid.
    /// </summary>
    public static class DataGridHelper
    {
        /// <summary>
        /// Handles the Sorting event raised by the DataGrid and returns the Sort function to be used on the
        /// data source.
        /// </summary>
        /// <typeparam name="T">The type of item to sort.</typeparam>
        /// <param name="e">The EventArgs provided by the Sorting event.</param>
        /// <returns>The Sort function. Can be used by the ObservableListView.</returns>
        public static Func<IEnumerable<T>, IOrderedEnumerable<T>> HandleDataGridSorting<T>(DataGridSortingEventArgs e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            e.Handled = true;
            if (e.Column.SortDirection == null)
            {
                e.Column.SortDirection = ListSortDirection.Ascending;
            }
            else if (e.Column.SortDirection == ListSortDirection.Ascending)
            {
                e.Column.SortDirection = ListSortDirection.Descending;
            }
            else
            {
                e.Column.SortDirection = null;
            }
            return GetSorting<T>(e.Column);
        }

        /// <summary>
        /// Gets a Sort function for the DataGrid column. It reads the SortMemberPath and SortDirection property of the column
        /// to create the sort.
        /// </summary>
        /// <typeparam name="T">The type of item to sort.</typeparam>
        /// <param name="column">The DataGrid column that should be sorted.</param>
        /// <returns>The Sort function. Can be used by the ObservableListView.</returns>
        public static Func<IEnumerable<T>, IOrderedEnumerable<T>> GetSorting<T>(DataGridColumn column)
        {
            if (column == null) throw new ArgumentNullException(nameof(column));
            var keySelector = GetSelector<T>(column.SortMemberPath);
            if (column.SortDirection == ListSortDirection.Ascending)
            {
                return x => x.OrderBy(keySelector);
            }
            else if (column.SortDirection == ListSortDirection.Descending)
            {
                return x => x.OrderByDescending(keySelector);
            }
            else
            {
                return null;
            }
        }

        private static Func<T, object> GetSelector<T>(string propertyPath)
        {
            return SelectorCache<T>.Functions.GetOrAdd(propertyPath, x => CreateSelector<T>(x));
        }

        private static Func<T, object> CreateSelector<T>(string propertyPath)
        {
            var parameter = Expression.Parameter(typeof(T), "item");
            if (string.IsNullOrEmpty(propertyPath))
                return Expression.Lambda<Func<T, object>>(Expression.Convert(parameter, typeof(object)), parameter).Compile();

            var propertyNames = propertyPath.Split('.');
            var propertyInfos = new PropertyInfo[propertyNames.Length];
            var type = typeof(T);
            for (int i = 0; i < propertyNames.Length; i++)
            {
                propertyInfos[i] = type.GetProperty(propertyNames[i]);
                type = propertyInfos[i].PropertyType;
            }

            var vars = new[] { parameter }.Concat(propertyInfos.Select(pi => Expression.Parameter(pi.PropertyType, pi.Name))).ToArray();
            var returnLabel = Expression.Label(typeof(object));
            var expressions = new List<Expression>();
            for (int i = 0; i < propertyInfos.Length; i++)
            {
                var propertyInfo = propertyInfos[i];
                if (GetDefault(vars[i].Type) == null)
                {
                    expressions.Add(Expression.IfThen(Expression.Equal(vars[i], Expression.Constant(null, typeof(object))),
                        Expression.Return(returnLabel, Expression.Constant(null, typeof(object)))));
                }
                expressions.Add(Expression.Assign(vars[i + 1], Expression.Property(vars[i], propertyInfo)));
            }
            expressions.Add(Expression.Return(returnLabel, Expression.Convert(vars.Last(), typeof(object))));
            expressions.Add(Expression.Label(returnLabel, Expression.Constant(null, typeof(object))));

            var block = Expression.Block(vars.Skip(1), expressions);
            return Expression.Lambda<Func<T, object>>(block, parameter).Compile();
        }

        private static object GetDefault(Type type)
        {
            return !type.IsValueType ? null : Activator.CreateInstance(type);
        }

        private static class SelectorCache<T>
        {
            public static ConcurrentDictionary<string, Func<T, object>> Functions { get; } = new ConcurrentDictionary<string, Func<T, object>>();
        }
    }
}