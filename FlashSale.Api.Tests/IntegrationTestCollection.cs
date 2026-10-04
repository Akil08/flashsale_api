using Xunit;

namespace FlashSale.Api.Tests;

[CollectionDefinition("Integration Tests")]
public class IntegrationTestCollection : ICollectionFixture<FlashSaleApiFixture>
{
}
