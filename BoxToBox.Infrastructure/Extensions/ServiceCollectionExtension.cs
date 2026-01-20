using BoxToBox.Infrastructure.Repositories;
using BoxToBox.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace BoxToBox.Infrastructure.Extensions
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddRepositories(this IServiceCollection services, IConfiguration configuration)
        {
#if DEBUG
            services.AddDbContext<BoxToBoxDbContext>(options => options.UseInMemoryDatabase(databaseName: "B2B"));
#else
            services.AddDbContext<BoxToBoxDbContext>(options => options.UseMySql(configuration.GetConnectionString("MySql"), new MySqlServerVersion(new Version(8, 0, 11))));
#endif
            services.AddScoped<IVideoAnalysisRepository, VideoAnalysisRepository>();
            services.AddScoped<IMatchRepository, MatchRepository>();
            services.AddScoped<IPlayerRepository, PlayerRepository>();
            services.AddScoped<IPlayerStatRepository, PlayerStatRepository>();
            services.AddScoped<IEventRepository, EventRepository>();
            services.AddScoped<IAccountRepository, AccountRepository>();

            return services;
        }
    }
}
