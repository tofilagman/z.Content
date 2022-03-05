using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;

namespace z.Content
{
    public static class ContentServiceInjector
    {
        public static void AddContent(this IServiceCollection services, Action<IServiceProvider, FileSystemOption> configure = null)
        {
            services.AddScoped<IContentService, ContentService>();

            services.TryAdd(new ServiceDescriptor(typeof(FileSystemOption), provider =>
            {
                var option = new FileSystemOption();
                configure?.Invoke(provider, option);
                return option;
            }, ServiceLifetime.Singleton));

        }
    }
}
