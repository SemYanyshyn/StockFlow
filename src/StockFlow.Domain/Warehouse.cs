using System;

namespace StockFlow.Domain
{
    public class Warehouse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public int? Capacity { get; set; }

        public bool Validate() =>
            !string.IsNullOrWhiteSpace(Name) &&
            (!Capacity.HasValue || Capacity.Value > 0);

        public bool MatchesSearch(string query) =>
            string.IsNullOrWhiteSpace(query) ||
            Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            (Address != null && Address.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
