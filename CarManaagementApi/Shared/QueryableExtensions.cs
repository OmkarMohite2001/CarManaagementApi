namespace CarManaagementApi.Shared;

public static class QueryableExtensions
{
    public static (List<T> Items, ApiMeta Meta) Paginate<T>(this IEnumerable<T> source, int page, int pageSize)
    {
        var safePage = page <= 0 ? 1 : page;
        var safePageSize = pageSize <= 0 ? 20 : pageSize;
        var total = source.Count();
        var items = source.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToList();

        return (items, new ApiMeta
        {
            Page = safePage,
            PageSize = safePageSize,
            Total = total
        });
    }
}
