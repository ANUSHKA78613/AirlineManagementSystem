using Microsoft.Extensions.DependencyInjection;

namespace Shared.ImageService
{
    public static class ImageServiceExtensions
    {
        /// <summary>
        /// Registers the image service (local disk storage) in the DI container.
        /// Call this in Program.cs: builder.Services.AddImageService()
        /// </summary>
        public static IServiceCollection AddImageService(this IServiceCollection services)
        {
            services.AddScoped<IImageService, LocalImageService>();
            return services;
        }
    }
}
