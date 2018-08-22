// © 2017 Sitecore Corporation A/S. All rights reserved. Sitecore® is a registered trademark of Sitecore Corporation A/S.

using Microsoft.Extensions.DependencyInjection;
using Sitecore.Content.Services.Items.OData;
using Sitecore.Content.Services.Items.OData.Search;
using Sitecore.ContentSearch.Utilities;
using Sitecore.DependencyInjection;
using Sitecore.Services.Infrastructure.Sitecore.Data;
using Sitecore.Services.Infrastructure.Sitecore.DependencyInjection;
using Sitecore.Services.Infrastructure.Sitecore.Services;
using Sitecore.Content.Services;

namespace Sitecore.Support.Content.Services
{
  public class Configurator : IServicesConfigurator
  {
    public void Configure(IServiceCollection serviceCollection)
    {
      var assemblies = new[] { typeof(Sitecore.Content.Services.Configurator).Assembly };
      serviceCollection.AddWebApiControllers(assemblies);

      serviceCollection
          .AddScoped<IItemRepository, ItemRepository>()
          .AddScoped<IItemDataModelFactory, ItemDataModelFactory>()
          .AddScoped<ApiKeyHelper>()
          .AddScoped<ContentSearchManagerWrapper>()
          .AddScoped<QueryBuilder>()
          .AddScoped<Sitecore.Content.Services.Items.OData.Search.FilterBinder, Sitecore.Suport.Content.Services.Items.OData.Search.FilterBinder>()
          .AddScoped<ODataItemSearch>()
          .AddScoped<ComparisonExpressionBuilder>()
          .AddScoped<FieldNameResolver>()
          .AddScoped<OrderByBinder>()
          .AddScoped<Sitecore.Content.Services.Items.OData.Search.SearchHelper>()
          .AddSingleton<ItemServiceDescriptor>();
    }
  }
}
