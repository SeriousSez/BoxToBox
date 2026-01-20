using BoxToBox.ApplicationService.JwtFeatures;
using BoxToBox.ApplicationService.Services;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BoxToBox.ApplicationService.Extensions;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<JwtHandler>();
        services.AddScoped<IVideoAnalysisService, VideoAnalysisService>();
        services.AddScoped<IVideoProcessor, OnnxYolov8VideoProcessor>();
        services.AddScoped<IAccountService, AccountService>();

        // Configure video upload settings
        var videoUploadPath = configuration.GetValue<string>("VideoUploadPath")
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Uploads", "Videos");
        services.Configure<VideoUploadSettings>(options =>
        {
            options.UploadPath = videoUploadPath;
        });

        return services;
    }
}

public class VideoUploadSettings
{
    public string UploadPath { get; set; } = string.Empty;
}
