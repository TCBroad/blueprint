using System;
using System.Collections.Generic;
using System.Linq;

namespace Blueprint.Utilities
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> ConcatSingle<T>(this IEnumerable<T> enumerable, T value)
        {
            return enumerable.Concat(new[] {value});
        }

        public static T[] ConcatSingle<T>(this T[] enumerable, T value)
        {
            return enumerable.Concat(new[] {value}).ToArray();
        }

        /// <summary>
        /// Converts any IEnumerable collection into a paged collection page. This will enumerate the results,
        /// so execution is not deferred.
        /// </summary>
        /// <param name="enumerable">The enumerable.</param>
        /// <param name="pageNumber">The page number.</param>
        /// <param name="pageSize">Size of the page.</param>
        /// <typeparam name="T">The type of the items within the list, inferred by the compiler.</typeparam>
        /// <returns>The requested page of the list.</returns>
        public static Page<T> ToPage<T>(this IEnumerable<T> enumerable, int pageNumber, int pageSize)
        {
            Guard.NotNull(nameof(enumerable), enumerable);
            Guard.GreaterThanOrEqual(nameof(pageNumber), pageNumber, 1);
            Guard.GreaterThanOrEqual(nameof(pageSize), pageSize, 1);

            var query = enumerable.AsQueryable();
            var totalCount = query.Count();

            if (pageNumber > Page<T>.CalculatePageCount(pageSize, totalCount) && totalCount > 0)
            {
                throw new InvalidOperationException($"The page number '{pageNumber}' specified is greater than the total number of pages.");
            }

            var items = query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            var pagedList = new Page<T>(items, pageNumber, pageSize, totalCount);

            return pagedList;
        }
    }
}
