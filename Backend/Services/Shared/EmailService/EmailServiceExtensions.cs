using Microsoft.Extensions.DependencyInjection;

namespace Shared.EmailService
{
    public static class EmailServiceExtensions
    {
        /// <summary>
        /// Registers IEmailService with the SmtpEmailService implementation.
        /// Reads SmtpSettings section from appsettings.json.
        /// </summary>
        public static IServiceCollection AddEmailService(this IServiceCollection services)
        {
            services.AddSingleton<IEmailService, SmtpEmailService>();
            return services;
        }
    }
}
