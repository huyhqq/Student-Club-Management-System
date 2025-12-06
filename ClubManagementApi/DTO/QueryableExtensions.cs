using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClubManagementApi.DTO
{
    public static class QueryableExtensions
    {
        public static IQueryable<T> OrderByDynamic<T>(this IQueryable<T> query, string sortBy, string sortOrder)
        {
            if (string.IsNullOrEmpty(sortBy)) return query.OrderBy(x => x);
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, sortBy);
            var lambda = Expression.Lambda(property, parameter);
            var method = sortOrder.ToLower() == "asc" ? "OrderBy" : "OrderByDescending";
            var expression = Expression.Call(typeof(Queryable), method, new[] { typeof(T), property.Type }, query.Expression, Expression.Quote(lambda));
            return query.Provider.CreateQuery<T>(expression);
        }
    }
}
