// © 2017 Sitecore Corporation A/S. All rights reserved. Sitecore® is a registered trademark of Sitecore Corporation A/S.

using Microsoft.Extensions.DependencyInjection;
using Sitecore.DependencyInjection;
using System.Linq;

namespace Sitecore.Support.Content.Services
{
  public class Configurator : IServicesConfigurator
  {
    public void Configure(IServiceCollection serviceCollection)
    {
      var descriptor = serviceCollection.FirstOrDefault(d => d.ServiceType == typeof(Sitecore.Content.Services.Items.OData.Search.FilterBinder));
      if (descriptor != null)
      {
        serviceCollection.Remove(descriptor);
      }
      serviceCollection.AddSingleton<Sitecore.Content.Services.Items.OData.Search.FilterBinder, Sitecore.Suport.Content.Services.Items.OData.Search.FilterBinder>();
    }
  }
}

