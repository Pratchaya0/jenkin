using Jenkin.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Jenkin.API.Helpers
{
    public static class QueryableExtensions
    {
        public static IQueryable<T> Paginate<T>(this IQueryable<T> queryable, PaginationDto pagination)
        {
            return queryable.Skip((pagination.Page - 1) * pagination.RecordsPerPage).Take(pagination.RecordsPerPage);
        }

        public static async Task<PaginationResultDto> PaginationResult<T>(this IQueryable<T> queryable, PaginationDto pagination)
        {
            var recordsPerPage = pagination.RecordsPerPage;
            var currentPage = pagination.Page;

            double totalAmountRecords = await queryable.CountAsync();
            double totalAmountPages = Math.Ceiling(totalAmountRecords / recordsPerPage);
            int pageIndex = currentPage - 1;

            PaginationResultDto resultDto = new PaginationResultDto()
            {
                TotalAmountRecords = totalAmountRecords,
                TotalAmountPages = totalAmountPages,
                CurrentPage = currentPage,
                RecordsPerPage = recordsPerPage,
                PageIndex = pageIndex
            };

            return resultDto;
        }

    }
}