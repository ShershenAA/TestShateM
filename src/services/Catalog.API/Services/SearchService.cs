using Catalog.API.Models;
using Nest;

namespace Catalog.API.Services;

public interface ISearchService
{
    Task IndexPartAsync(Part part);
    Task<IEnumerable<Part>> SearchAsync(string query);
    Task DeletePartAsync(Guid id);
    Task BulkIndexAsync(IEnumerable<Part> parts);
}

public class ElasticsearchService : ISearchService
{
    private readonly IElasticClient _client;
    private readonly ILogger<ElasticsearchService> _logger;
    private const string IndexName = "parts";

    public ElasticsearchService(IElasticClient client, ILogger<ElasticsearchService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task IndexPartAsync(Part part)
    {
        var response = await _client.IndexAsync(part, i => i
            .Index(IndexName)
            .Id(part.Id.ToString()));

        if (!response.IsValid)
            _logger.LogWarning("Failed to index part {Id}: {Error}", part.Id, response.DebugInformation);
        else
            _logger.LogInformation("Part indexed in Elasticsearch: {ArticleNumber}", part.ArticleNumber);
    }

    public async Task<IEnumerable<Part>> SearchAsync(string query)
    {
        var response = await _client.SearchAsync<Part>(s => s
            .Index(IndexName)
            .Query(q => q
                .MultiMatch(m => m
                    .Fields(f => f
                        .Field(p => p.Name, boost: 3)        // имя важнее
                        .Field(p => p.ArticleNumber, boost: 2)
                        .Field(p => p.Brand)
                        .Field(p => p.Description))
                    .Query(query)
                    .Fuzziness(Fuzziness.Auto)))  // опечатки прощает
            .Size(20));

        if (!response.IsValid)
        {
            _logger.LogWarning("Search failed: {Error}", response.DebugInformation);
            return Enumerable.Empty<Part>();
        }

        return response.Documents;
    }
    
    public async Task BulkIndexAsync(IEnumerable<Part> parts)
    {
        var response = await _client.BulkAsync(b => b
            .Index(IndexName)
            .IndexMany(parts, (op, part) => op.Id(part.Id.ToString())));

        if (!response.IsValid)
            _logger.LogWarning("Bulk index failed: {Error}", response.DebugInformation);
        else
            _logger.LogInformation("Bulk indexed {Count} parts", response.Items.Count);
    }

    public async Task DeletePartAsync(Guid id)
    {
        await _client.DeleteAsync<Part>(id.ToString(), d => d.Index(IndexName));
    }
}